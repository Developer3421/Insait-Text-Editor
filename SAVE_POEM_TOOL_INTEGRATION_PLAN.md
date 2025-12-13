# 📝 План інтеграції інструмента збереження віршів у режимі агента

**Дата**: 19 жовтня 2025  
**Мета**: Автоматичне збереження віршів/текстів створених AI моделлю через стандартне вікно Windows Save Dialog

---

## 🎯 Цілі інтеграції

1. ✅ **Автоматичне виявлення** - агент розпізнає коли створив вірш/текст
2. ✅ **Режим агента** - інструмент викликається автоматично після генерації
3. ✅ **Стандартний UI** - використання Windows Save File Dialog
4. ✅ **Без ручного втручання** - користувач лише вибирає шлях збереження
5. ✅ **Відкриття у редакторі** - файл автоматично відкривається як нова вкладка

---

## 🏗️ Архітектура інтеграції

```
┌──────────────────────────────────────────────────────────┐
│  ChatWindow (UI)                                         │
│  • Користувач пише: "Напиши вірш про осінь"            │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ▼
┌──────────────────────────────────────────────────────────┐
│  AgentService                                            │
│  • GetResponseStreamAsync()                              │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ▼
┌──────────────────────────────────────────────────────────┐
│  MicrosoftInsaitAgent                                    │
│  • ProcessStreamAsync()                                  │
│  • Генерує вірш токен-за-токеном                        │
│  • Після завершення → аналізує чи це вірш              │
└────────────────────┬─────────────────────────────────────┘
                     │
                     ▼ (якщо виявлено вірш)
┌──────────────────────────────────────────────────────────┐
│  SaveToFileTool                                          │
│  • Автоматично викликається агентом                     │
│  • Показує Windows Save Dialog                          │
│  • Зберігає файл                                        │
│  • Відкриває як нову вкладку                            │
└──────────────────────────────────────────────────────────┘
```

---

## 📋 Етапи реалізації

### **Етап 1: Оновлення SaveToFileTool** ⏱️ 15 хв

**Файл**: `InsaitTextEditor/Agents/Tools/SaveToFileTool.cs`

#### 1.1 Додати автоматичне визначення типу контенту
```csharp
/// <summary>
/// Визначити тип контенту та запропонувати розширення
/// </summary>
private (string extension, string fileType) DetectContentType(string content)
{
    // Перевірка на вірш (кілька рядків, римування)
    if (IsPoem(content))
        return (".txt", "Poem");
    
    // Перевірка на markdown
    if (content.Contains("##") || content.Contains("**"))
        return (".md", "Markdown");
    
    // За замовчуванням
    return (".txt", "Text");
}

private bool IsPoem(string content)
{
    var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    
    // Вірш зазвичай має 4+ рядки
    if (lines.Length < 4) return false;
    
    // Короткі рядки (характерно для віршів)
    var avgLength = lines.Average(l => l.Length);
    if (avgLength > 80) return false;
    
    // Перевірка на римування (останні слова схожі)
    // Спрощена логіка
    return true;
}
```

#### 1.2 Додати метод для автоматичного виклику
```csharp
/// <summary>
/// Автоматичний виклик після генерації агентом (без UI блокування)
/// </summary>
public async Task<ToolResult> AutoSaveAsync(
    string content, 
    string? suggestedFileName = null,
    bool openInEditor = true)
{
    var (extension, fileType) = DetectContentType(content);
    
    if (suggestedFileName == null)
    {
        suggestedFileName = $"{fileType}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
    }
    
    return await ExecuteAsync(content, suggestedFileName);
}
```

---

### **Етап 2: Інтеграція з MicrosoftInsaitAgent** ⏱️ 20 хв

**Файл**: `InsaitTextEditor/Agents/MicrosoftInsaitAgent.cs`

#### 2.1 Додати логіку аналізу відповіді
```csharp
/// <summary>
/// Стрімінгова обробка з автоматичним виявленням віршів
/// </summary>
public async IAsyncEnumerable<string> ProcessStreamAsync(
    string userMessage,
    List<ChatMessage> history,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var accumulatedResponse = new StringBuilder();
    var shouldAutoSave = ShouldAutoSaveResponse(userMessage);
    
    // Генерація токенів
    await foreach (var token in _adapter.SendMessageStreamAsync(...))
    {
        accumulatedResponse.Append(token);
        yield return token;
    }
    
    // ПІСЛЯ завершення стрімінгу
    if (shouldAutoSave && _saveToFileTool != null)
    {
        var finalResponse = accumulatedResponse.ToString();
        
        // Автоматично викликаємо SaveToFileTool
        await AutoInvokeSaveToolAsync(finalResponse, userMessage);
    }
}

/// <summary>
/// Визначити чи потрібно зберігати відповідь
/// </summary>
private bool ShouldAutoSaveResponse(string userMessage)
{
    var triggers = new[] 
    { 
        "напиши вірш", 
        "створи вірш",
        "скомпонуй вірш",
        "згенеруй текст",
        "напиши історію",
        "створи оповідання"
    };
    
    return triggers.Any(t => 
        userMessage.ToLower().Contains(t));
}

/// <summary>
/// Автоматичний виклик інструменту збереження
/// </summary>
private async Task AutoInvokeSaveToolAsync(string content, string userPrompt)
{
    try
    {
        LogAgent("💾 Автоматичний виклик SaveToFileTool...");
        
        // Згенерувати назву файлу з промпту
        var fileName = GenerateFileNameFromPrompt(userPrompt);
        
        var result = await _saveToFileTool.AutoSaveAsync(
            content, 
            fileName,
            openInEditor: true
        );
        
        if (result.Success)
        {
            LogAgent($"✅ Файл збережено: {result.FilePath}");
        }
        else
        {
            LogAgent($"❌ Помилка збереження: {result.Message}");
        }
    }
    catch (Exception ex)
    {
        LogAgent($"❌ Помилка auto-save: {ex.Message}");
    }
}

private string GenerateFileNameFromPrompt(string prompt)
{
    // "Напиши вірш про осінь" → "Вірш_про_осінь.txt"
    var cleaned = prompt
        .Replace("напиши ", "")
        .Replace("створи ", "")
        .Replace("вірш ", "Вірш_")
        .Replace("про ", "про_")
        .Trim();
    
    // Обрізати до 30 символів
    if (cleaned.Length > 30)
        cleaned = cleaned.Substring(0, 30);
    
    return $"{cleaned}_{DateTime.Now:HHmmss}.txt";
}
```

---

### **Етап 3: Передача Window контексту** ⏱️ 10 хв

**Файл**: `InsaitTextEditor/App.axaml.cs`

#### 3.1 Оновити ініціалізацію агента (потребує MainWindow)
```csharp
// ❌ ПРОБЛЕМА: У App.axaml.cs немає доступу до Window!
// ✅ РІШЕННЯ: Ініціалізувати SaveToFileTool пізніше в ChatWindow

// Поки що залишити null
MicrosoftInsaitAgent = new MicrosoftInsaitAgent(
    MicrosoftAgentsAdapter,
    AgentConfig,
    saveToFileTool: null  // ← буде встановлено пізніше
);
```

#### 3.2 Додати метод для встановлення інструменту
**Файл**: `InsaitTextEditor/Agents/MicrosoftInsaitAgent.cs`
```csharp
/// <summary>
/// Встановити SaveToFileTool після створення Window
/// </summary>
public void SetSaveToFileTool(SaveToFileTool tool)
{
    _saveToFileTool = tool;
    LogAgent("✅ SaveToFileTool встановлено");
}
```

---

### **Етап 4: Ініціалізація у ChatWindow** ⏱️ 15 хв

**Файл**: `InsaitTextEditor/Windows/ChatWindow.axaml.cs`

#### 4.1 Додати ініціалізацію SaveToFileTool
```csharp
private async void ChatWindow_Opened(object? sender, EventArgs e)
{
    // ...існуючий код...
    
    // ✅ Ініціалізувати SaveToFileTool з контекстом цього вікна
    var saveToFileTool = new SaveToFileTool(
        App.TabManager,  // ← потрібно додати TabManager в App
        ownerWindow: this
    );
    
    // Встановити інструмент в агента
    App.MicrosoftInsaitAgent.SetSaveToFileTool(saveToFileTool);
    
    Console.WriteLine("[ChatWindow] ✅ SaveToFileTool підключено до агента");
    
    // ...решта коду...
}
```

---

### **Етап 5: Експозиція TabManager** ⏱️ 5 хв

**Файл**: `InsaitTextEditor/App.axaml.cs`

#### 5.1 Додати TabManager як публічну властивість
```csharp
public static TabManager TabManager { get; private set; } = null!;
```

#### 5.2 Ініціалізувати у OnFrameworkInitializationCompleted
```csharp
public override void OnFrameworkInitializationCompleted()
{
    var savedLang = SettingsService.LoadLanguage();
    LocalizationService.Initialize(savedLang);

    if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
    {
        desktop.MainWindow = new MainWindow();
        
        // ✅ Отримати TabManager з MainWindow
        if (desktop.MainWindow is MainWindow mainWindow)
        {
            TabManager = mainWindow.TabManager; // ← потрібно зробити публічним
        }
    }

    base.OnFrameworkInitializationCompleted();
}
```

---

### **Етап 6: Оновлення MainWindow** ⏱️ 5 хв

**Файл**: `InsaitTextEditor/MainWindow.axaml.cs`

#### 6.1 Зробити TabManager публічним
```csharp
public class MainWindow : Window
{
    // ✅ Зробити публічним для доступу з App
    public TabManager TabManager { get; private set; }
    
    // ...решта коду...
}
```

---

## 🔄 Потік виконання (User Flow)

### Сценарій 1: Користувач просить вірш

```
1. Користувач пише: "Напиши вірш про осінь"
   └─> ChatWindow.Send_Click()

2. AgentService.GetResponseStreamAsync()
   └─> MicrosoftInsaitAgent.ProcessStreamAsync()
       • ShouldAutoSaveResponse() → TRUE ✅
       • Генерація токенів...
       
3. Streaming відповіді у ChatWindow
   "Листя жовте падає..."
   "Дощ осінній плаче..."
   ...
   
4. ПІСЛЯ завершення стрімінгу:
   MicrosoftInsaitAgent.AutoInvokeSaveToolAsync()
   └─> SaveToFileTool.AutoSaveAsync()
       • Визначає тип: IsPoem() → TRUE
       • Назва: "Вірш_про_осінь_153045.txt"
       • Показує Windows Save Dialog 💾
       
5. Користувач вибирає шлях: "C:\MyPoems\Autumn.txt"
   
6. SaveToFileTool:
   • Зберігає файл ✅
   • TabManager.OpenFileAsync("C:\MyPoems\Autumn.txt") ✅
   
7. Користувач бачить:
   • Вірш у чаті ✅
   • Новий таб "Autumn.txt" відкритий ✅
   • Може редагувати далі ✅
```

### Сценарій 2: Звичайне питання (не вірш)

```
1. Користувач: "Яка погода?"
   └─> ShouldAutoSaveResponse() → FALSE ❌
   
2. Генерація відповіді без виклику SaveToFileTool
   
3. Відповідь просто показується у чаті
```

---

## ⚙️ Конфігурація AgentConfig

**Файл**: `InsaitTextEditor/Agents/AgentConfig.cs`

```csharp
public class AgentConfig
{
    public string AgentName => "Insait Creative Assistant";
    public string AgentDescription => 
        "Асистент для створення текстів та віршів з автоматичним збереженням";
    
    public List<string> AvailableTools => ["save_to_file"];
    
    // Збільшити для генерації віршів
    public int MaxIterations => 5;
    public bool EnableToolUse => true;
    
    // ✅ НОВИЙ параметр
    public bool AutoSaveCreativeContent => true;
    
    // Тригери для автоматичного збереження
    public List<string> AutoSaveTriggers => new()
    {
        "напиши вірш",
        "створи вірш",
        "скомпонуй вірш",
        "напиши історію",
        "створи оповідання",
        "згенеруй текст",
        "напиши есе"
    };
}
```

---

## 🧪 Тестування

### Тест 1: Генерація вірша
```
Input: "Напиши короткий вірш про зиму"
Expected:
1. ✅ Вірш генерується токен-за-токеном
2. ✅ Після завершення → Windows Save Dialog
3. ✅ Файл зберігається (напр. "Вірш_про_зиму_154523.txt")
4. ✅ Новий таб відкривається
```

### Тест 2: Звичайне питання
```
Input: "Поясни квантову механіку"
Expected:
1. ✅ Відповідь генерується
2. ❌ Save Dialog НЕ відкривається
3. ✅ Відповідь у чаті
```

### Тест 3: Скасування збереження
```
Input: "Напиши вірш про море"
Expected:
1. ✅ Вірш генерується
2. ✅ Save Dialog відкривається
3. ❌ Користувач натискає Cancel
4. ✅ ToolResult.Success = false
5. ✅ Вірш залишається у чаті (не втрачено)
```

### Тест 4: Довгий текст
```
Input: "Напиши історію про дракона на 500 слів"
Expected:
1. ✅ Генерація з MaxResponseTokens обмеженням
2. ✅ Автоматичне збереження як .txt
3. ✅ Відкриття у редакторі
```

---

## 📊 Переваги рішення

### ✅ Автоматизація
- Користувач не думає про збереження
- Агент сам розуміє коли потрібно зберегти

### ✅ Гнучкість
- Можна вимкнути через `AutoSaveCreativeContent = false`
- Кастомізація тригерів через `AutoSaveTriggers`

### ✅ UX
- Стандартний Windows Save Dialog (знайомий інтерфейс)
- Автоматичне відкриття файлу для редагування
- Збережений контент не втрачається навіть якщо закрити чат

### ✅ Розширюваність
- Легко додати нові типи контенту (код, markdown, JSON)
- Можна інтегрувати з хмарними сховищами (OneDrive, Google Drive)

---

## ⏱️ Загальний час реалізації

| Етап | Опис | Час |
|------|------|-----|
| 1 | Оновлення SaveToFileTool | 15 хв |
| 2 | Інтеграція з MicrosoftInsaitAgent | 20 хв |
| 3 | Передача Window контексту | 10 хв |
| 4 | Ініціалізація у ChatWindow | 15 хв |
| 5 | Експозиція TabManager | 5 хв |
| 6 | Оновлення MainWindow | 5 хв |
| **РАЗОМ** | | **70 хв (1 год 10 хв)** |

---

## 🚀 Наступні кроки після базової інтеграції

### Фаза 2 (опціонально):
1. **Шаблони назв файлів** - користувач може налаштувати паттерни
2. **Автоматична категоризація** - вірші → папка "Poems", історії → "Stories"
3. **Cloud sync** - автоматичне резервне копіювання
4. **Версіювання** - зберігати кілька версій одного вірша
5. **Метадані** - додавати дату, автора (AI), промпт у файл

---

## 📝 Приклад фінальної взаємодії

```
[Користувач]
Напиши вірш про українську мову

[Агент - streaming]
Мова солов'їна, мелодійна,
В ній душа народу, пісня вільна...
(генерується токен за токеном)

[Агент - після завершення]
💾 Автоматично викликаю збереження...

[Windows Save Dialog]
┌─────────────────────────────────────┐
│ Зберегти як                         │
│ Назва: Вірш_про_українську_мову.txt │
│ Тип: Text Files (*.txt)             │
│ Шлях: C:\Users\...\Documents\       │
│                                     │
│ [Зберегти]  [Скасувати]            │
└─────────────────────────────────────┘

[Після збереження]
✅ Файл збережено: C:\Users\...\Documents\Вірш_про_українську_мову.txt
📂 Відкрито нову вкладку у редакторі
```

---

**Статус**: 📋 ПЛАН ГОТОВИЙ  
**Готовність до реалізації**: ✅ ТАК  
**Необхідні ресурси**: Microsoft.Agents (вже є), Avalonia (вже є), TabManager (вже є)


