# Виправлення проблеми з кареткою при створенні нової вкладки та відкритті файлу

## Дата: 27 грудня 2025

## Проблема

При роботі з вкладками виникали наступні проблеми:

1. **При створенні нової вкладки** - каретка не встановлювалася на початок (позиція 0)
2. **При відкритті файлу** - каретка залишалася в старій позиції або не ініціалізувалася правильно
3. **При перемиканні між вкладками** - каретка могла залишатися в неправильній позиції
4. **Виліт додатку** - через неправильну ініціалізацію каретки

## Причина проблеми

При створенні нової вкладки або відкритті файлу текст встановлювався через binding `Text="{Binding CurrentText, Mode=TwoWay}"`, але:

1. Властивості `SelectionStart` та `SelectionEnd` в `LinedTextInput` не скидалися
2. Каретка залишалася в позиції, де була в попередній вкладці
3. Якщо позиція каретки була більша за довжину нового тексту, це призводило до помилок

## Рішення

Додано систему подій для скидання каретки на початок документу:

### 1. TabManager.cs - Додано подію `ResetCaretRequested`

```csharp
public event EventHandler? ResetCaretRequested; // Нова подія для скидання каретки
```

Ця подія викликається в наступних ситуаціях:

#### При створенні нової вкладки (`CreateNewTab`)
```csharp
SetActiveTab(id);
TabAdded?.Invoke(this, vm);

// Save initial state to DB
_databaseService.SaveDocumentAsync(id, ws.Text);

// Request caret reset to position 0
ResetCaretRequested?.Invoke(this, EventArgs.Empty);
```

#### При перемиканні між вкладками (`SetActiveTab`)
```csharp
OnPropertyChanged(nameof(CurrentText));

// Request caret reset when switching tabs
ResetCaretRequested?.Invoke(this, EventArgs.Empty);
```

Також після завантаження з бази даних:
```csharp
_databaseService.LoadDocumentAsync(id).ContinueWith(task =>
{
    if (task.IsCompletedSuccessfully && task.Result is not null)
    {
        ws.Text = task.Result;
        activeVm.DocumentText = task.Result;
        OnPropertyChanged(nameof(CurrentText));
        // Reset caret when loading from DB
        ResetCaretRequested?.Invoke(this, EventArgs.Empty);
    }
}, TaskScheduler.FromCurrentSynchronizationContext());
```

#### При відкритті файлу (`OpenFileAsync`)
```csharp
_filePaths[id] = filePath;

// Notify UI to update text
OnPropertyChanged(nameof(CurrentText));

// Reset caret to the beginning of the file
ResetCaretRequested?.Invoke(this, EventArgs.Empty);
```

### 2. LinedTextInput.axaml.cs - Додано метод `ResetCaret()`

```csharp
// Public method to reset caret to the beginning
public void ResetCaret()
{
    UpdateSelection(0, 0);
    ScrollToLine(0);
}
```

Цей метод:
- Встановлює `SelectionStart = 0` та `SelectionEnd = 0`
- Прокручує документ до початку (рядок 0)

### 3. MainWindow.axaml.cs - Підписка на подію

```csharp
// Subscribe to ResetCaretRequested event
_tabManager.ResetCaretRequested += (_, _) =>
{
    var editor = this.FindControl<LinedTextInput>("LinedEditorHost");
    if (editor != null)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            editor.ResetCaret();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }
};
```

Використовуємо `Dispatcher.UIThread.Post` з пріоритетом `Background` для:
- Забезпечення виконання в UI потоці
- Надання часу для завершення оновлення тексту перед скиданням каретки

## Змінені файли

1. **`TabManager.cs`**
   - Додано подію `ResetCaretRequested`
   - Виклик події в `CreateNewTab()`, `SetActiveTab()`, `OpenFileAsync()`

2. **`LinedTextInput.axaml.cs`**
   - Додано метод `ResetCaret()` для скидання каретки на початок

3. **`MainWindow.axaml.cs`**
   - Підписка на подію `ResetCaretRequested`
   - Виклик `editor.ResetCaret()` при спрацюванні події

## Результат

✅ При створенні нової вкладки каретка встановлюється на початок (позиція 0)
✅ При відкритті файлу каретка встановлюється на початок документу
✅ При перемиканні між вкладками каретка скидається на початок
✅ Додаток не вилітає через неправильну позицію каретки
✅ Прокрутка автоматично повертається на початок документу

## Тестування

Перевірте на наступних сценаріях:

1. ✅ Створіть нову вкладку - каретка має бути на початку
2. ✅ Відкрийте файл - каретка має бути на початку файлу
3. ✅ Створіть декілька вкладок і перемикайтеся між ними - каретка завжди на початку
4. ✅ Введіть текст у вкладці, створіть нову - каретка нової вкладки на початку
5. ✅ Відкрийте великий файл - каретка на початку, прокрутка зверху

## Технічні деталі

### Чому використовується подія, а не пряме встановлення?

1. **Розділення відповідальності**: `TabManager` не повинен знати про `LinedTextInput`
2. **Гнучкість**: Можна підписати кілька обробників
3. **Асинхронність**: Дозволяє виконати скидання після оновлення UI

### Чому Dispatcher.UIThread.Post?

1. **UI Thread**: Всі зміни UI мають виконуватися в UI потоці
2. **Background Priority**: Надає час для завершення оновлення тексту перед скиданням каретки
3. **Безпека**: Уникає race conditions та взаємних блокувань

### Порядок виконання при відкритті файлу

1. `OpenFileAsync()` - завантаження тексту з файлу
2. `CreateNewTab()` - створення нової вкладки
3. Встановлення `ws.Text = text` - оновлення тексту workspace
4. `OnPropertyChanged(nameof(CurrentText))` - оповіщення про зміну
5. Binding оновлює `LinedTextInput.Text`
6. `ResetCaretRequested` спрацьовує
7. `Dispatcher.UIThread.Post` - планування виконання
8. `editor.ResetCaret()` - скидання каретки після оновлення UI

## Висновок

Ця зміна гарантує, що каретка завжди встановлюється в правильну позицію (початок документу) при створенні нових вкладок, відкритті файлів або перемиканні між вкладками, що усуває проблему з вильотом додатку та покращує користувацький досвід.

