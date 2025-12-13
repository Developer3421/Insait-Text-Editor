# План покращень для чату з AI

**Дата створення:** 21 жовтня 2025

## Мета
Покращити взаємодію користувача з AI чатом шляхом:
1. Фільтрації технічних символів у відповідях AI (нормальний і reasoning режими)
2. Додавання мовних інструкцій в reasoning промпти
3. Автоматичного скролу до останнього повідомлення в чаті

## Поточний стан

### 1. Фільтрація символів
- ✅ **Нормальний режим**: Вже реалізовано фільтрацію в `ChatWindow.axaml.cs`:
  - Метод `ContainsTechnicalTokens()` перевіряє токени
  - Фільтруються: `<end_of_turn>`, `<start_of_turn>`, `<eos>`, `<bos>`, `<pad>`, `<|`, `|>`
- ❌ **Reasoning режим**: Фільтрація відсутня в `ReasoningService.cs`
  - Використовується `_agent.GenerateReasoningStepStreamAsync()`
  - Потрібно додати таку ж фільтрацію

### 2. Мовні інструкції
- ✅ **Нормальний режим**: Реалізовано в `PromptBuilder.cs`:
  - Метод `BuildSystemPrompt()` додає мовні інструкції
  - Підтримка: українська, англійська, німецька, російська, турецька, авто-режим
- ❌ **Reasoning режим**: Відсутні мовні інструкції в `ReasoningPromptBuilder.cs`:
  - `BuildPlanningPrompt()` - без мовних інструкцій
  - `BuildStepPrompt()` - без мовних інструкцій
  - `BuildFinalAnswerPrompt()` - без мовних інструкцій

### 3. Автоматичний скрол
- ⚠️ **Частково реалізовано**:
  - Є метод `ScrollToBottom()` в `ChatWindow.axaml.cs`
  - Підписка на `Messages_CollectionChanged` для автоскролу
  - Підписка на `ChatMessage_PropertyChanged` для оновлення контенту
  - **Проблема**: Не завжди спрацьовує коректно при швидких оновленнях

## План реалізації

### Завдання 1: Фільтрація технічних символів у reasoning режимі
**Файли для зміни:**
- `InsaitTextEditor/Services/Reasoning/ReasoningService.cs`

**Що зробити:**
1. Додати метод `ContainsTechnicalTokens()` в `ReasoningService`
2. Застосувати фільтрацію в трьох місцях:
   - При генерації плану (`GenerateReasoningChainStreamInternalAsync` - планування)
   - При виконанні кроків (streaming контенту кроку)
   - При генерації фінальної відповіді

**Псевдокод:**
```csharp
private bool ContainsTechnicalTokens(string text)
{
    var patterns = new[] { "<end_of_turn>", "<start_of_turn>", "<eos>", "<bos>", "<pad>", "<|", "|>" };
    return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
}
```

### Завдання 2: Додати мовні інструкції в reasoning промпти
**Файли для зміни:**
- `InsaitTextEditor/Services/Reasoning/ReasoningPromptBuilder.cs`

**Що зробити:**
1. Додати залежність `UserInstructionService` в конструктор
2. Створити метод `GetLanguageInstruction()` для отримання мовних інструкцій
3. Додати мовні інструкції в усі промпт-методи:
   - `BuildPlanningPrompt()` - додати інструкції про мову відповіді
   - `BuildStepPrompt()` - додати інструкції про мову відповіді
   - `BuildFinalAnswerPrompt()` - додати інструкції про мову відповіді

**Приклад інструкції:**
```
🇺🇦 ВАЖЛИВО: Відповідай УКРАЇНСЬКОЮ МОВОЮ!
🇬🇧 IMPORTANT: Respond in ENGLISH!
(залежно від налаштувань користувача)
```

### Завдання 3: Покращити автоматичний скрол
**Файли для зміни:**
- `InsaitTextEditor/Windows/ChatWindow.axaml.cs`

**Що зробити:**
1. Покращити метод `ScrollToBottom()`:
   - Додати затримку для повного рендерингу UI
   - Використовувати `Dispatcher.UIThread.Post` замість `InvokeAsync` для більшої надійності
   - Додати перевірку, чи елементи вже виміряні

2. Додати примусовий скрол після кожного оновлення контенту:
   - В `Messages_CollectionChanged` - збільшити затримку до 50ms
   - В `ChatMessage_PropertyChanged` - гарантувати скрол після кожного оновлення
   - В reasoning режимі - скролити після кожної події

3. Додати "sticky scroll" - утримання скролу внизу:
   - Перевіряти чи користувач прокрутив вгору
   - Якщо ні - автоматично тримати внизу
   - Якщо так - не переривати читання

**Псевдокод:**
```csharp
private bool _isUserScrolling = false;
private double _lastScrollPosition = 0;

private async Task ScrollToBottomIfNeeded()
{
    if (!_isUserScrolling)
    {
        await ScrollToBottom();
    }
}
```

### Завдання 4: Оновити ReasoningService для використання UserInstructionService
**Файли для зміни:**
- `InsaitTextEditor/Services/Reasoning/ReasoningService.cs`
- `InsaitTextEditor/Program.cs` або `App.axaml.cs` (для DI)

**Що зробити:**
1. Передати `UserInstructionService` в конструктор `ReasoningService`
2. Передати сервіс далі в `ReasoningPromptBuilder`
3. Оновити місця створення `ReasoningService`

## Очікувані результати

### Після реалізації:
✅ Технічні токени (`<end_of_turn>`, тощо) не відображаються в reasoning режимі  
✅ Reasoning відповіді завжди в обраній користувачем мові  
✅ Чат автоматично прокручується до останнього повідомлення  
✅ Користувач завжди бачить повне останнє повідомлення  
✅ Плавна робота при швидкому streaming  

## Технічні деталі

### Залежності між компонентами:
```
ChatWindow
  ├─> ReasoningService
  │     ├─> ReasoningPromptBuilder (потребує UserInstructionService)
  │     └─> MicrosoftInsaitAgent
  └─> UserInstructionService
```

### Порядок виконання:
1. Спочатку Завдання 1 (фільтрація) - незалежне
2. Потім Завдання 4 (DI) - потрібно для Завдання 2
3. Потім Завдання 2 (мовні інструкції) - залежить від Завдання 4
4. Нарешті Завдання 3 (скрол) - незалежне

## Тестування

### Тест-кейси:
1. **Фільтрація**: Надіслати запит в reasoning режимі → перевірити відсутність технічних токенів
2. **Мова**: Змінити мову AI на українську → перевірити reasoning українською
3. **Скрол**: Надіслати довге повідомлення → перевірити що видно останній рядок
4. **Streaming**: Швидкий streaming → перевірити плавність скролу

## Ризики та обмеження
- Можлива затримка UI при дуже швидкому streaming (мітигація: throttling)
- Автоскрол може бути назирливим якщо користувач читає історію (мітигація: sticky scroll)

---

**Статус:** Готовий до реалізації  
**Пріоритет:** Високий  
**Час виконання:** ~1-2 години

