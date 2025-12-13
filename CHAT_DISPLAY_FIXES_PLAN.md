# 🛠️ План виправлення відображення чату та локалізації

**Дата**: 19 жовтня 2025  
**Статус**: 🔴 КРИТИЧНІ ПРОБЛЕМИ ВИЯВЛЕНО

---

## 🔍 Виявлені проблеми

### ❌ Проблема 1: Відсутня локалізація міток "User" та "Assistant"
**Поточний стан**:
- У `ChatWindow.axaml` hardcoded текст "Ві" (українською) для користувача
- Назва асистента береться з `AssistantConfig.Name` (завжди "Insait Assistant")
- **НЕ змінюється** при зміні мови інтерфейсу

**Локація проблеми**:
```xml
<!-- ChatWindow.axaml, рядок ~66 -->
<TextBlock Text="Ви"  <!-- ❌ Hardcoded українською -->
           FontWeight="SemiBold" 
           FontSize="13"
           Foreground="{StaticResource AppGreenBrush}"
           Margin="0,0,0,8"/>

<!-- ChatWindow.axaml, рядок ~102 -->
<TextBlock Text="{x:Static svc:AssistantConfig.Name}"  <!-- ❌ Завжди англійською -->
           FontWeight="SemiBold" 
           FontSize="13"
           Foreground="{StaticResource AppPurpleBrush}"
           DockPanel.Dock="Left"/>
```

**Наслідок**: Користувачі з англійським інтерфейсом бачать "Ві", що неправильно.

---

### ❌ Проблема 2: AI модель НЕ дотримується вибраної мови з InstructionEditorWindow

**Поточний стан**:
- `UserInstruction.AiLanguage` зберігається в БД ✅
- `InstructionEditorWindow` дозволяє вибрати мову AI ✅
- **АЛЕ**: `PromptBuilder.BuildSystemPrompt()` **НЕ використовує** `AiLanguage` ❌

**Локація проблеми**:
```csharp
// PromptBuilder.cs, рядок 19-41
public string BuildSystemPrompt()
{
    var basePrompt = 
        "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
        "CRITICAL RULES:\n" +
        "1. ALWAYS respond in the SAME LANGUAGE as the user's message\n" +  // ❌ Ігнорує AiLanguage!
        "2. If user writes in Ukrainian - respond in Ukrainian\n" +
        // ...
    
    var userInstruction = _instructionService.GetUserInstruction();
    
    // ❌ НЕ ПЕРЕВІРЯЄ userInstruction.AiLanguage!
    if (!string.IsNullOrWhiteSpace(userInstruction?.Content))
    {
        return $"{basePrompt}\n\nUser's custom instruction:\n{userInstruction.Content}";
    }
    
    return basePrompt;
}
```

**Наслідок**: Якщо користувач вибере "Завжди відповідати українською" в налаштуваннях, модель все одно може відповідати англійською.

---

### ❌ Проблема 3: Потенційні проблеми з відображенням повідомлень

**Виявлено у ChatWindow.axaml.cs**:
1. ✅ Фільтрація порожніх токенів працює
2. ✅ Throttling оновлень UI працює (50ms)
3. ⚠️ **Відсутня перевірка на дублікати повідомлень**
4. ⚠️ **Typing indicator може залишитися видимим** при помилках

---

## 🎯 Рішення

### ✅ Рішення 1: Додати локалізацію міток User/Assistant

#### Крок 1.1: Додати ключі до файлів локалізації

**Файли для редагування**:
- `Strings.en.axaml` - англійська
- `Strings.uk.axaml` - українська
- `Strings.de.axaml` - німецька
- `Strings.ru.axaml` - російська
- `Strings.tr.axaml` - турецька

**Нові ключі**:
```xml
<!-- Для всіх мов -->
<x:String x:Key="Key.ChatUser">User</x:String>           <!-- en: User, uk: Ви, de: Benutzer, ru: Пользователь, tr: Kullanıcı -->
<x:String x:Key="Key.ChatAssistant">Assistant</x:String> <!-- en: Assistant, uk: Асистент, de: Assistent, ru: Ассистент, tr: Asistan -->
```

#### Крок 1.2: Оновити ChatWindow.axaml

**Замінити hardcoded текст на динамічний**:
```xml
<!-- Було -->
<TextBlock Text="Ви" ... />

<!-- Стане -->
<TextBlock Text="{DynamicResource Key.ChatUser}" ... />

<!-- Було -->
<TextBlock Text="{x:Static svc:AssistantConfig.Name}" ... />

<!-- Стане -->
<TextBlock Text="{DynamicResource Key.ChatAssistant}" ... />
```

---

### ✅ Рішення 2: Інтегрувати AiLanguage в системний промпт

#### Крок 2.1: Оновити PromptBuilder.BuildSystemPrompt()

**Логіка**:
1. Отримати `UserInstruction.AiLanguage`
2. Якщо встановлено конкретну мову → додати **ОБОВ'ЯЗКОВУ інструкцію** на початку промпту
3. Якщо null/auto → залишити поточну поведінку (відповідати мовою користувача)

**Код**:
```csharp
public string BuildSystemPrompt()
{
    var userInstruction = _instructionService.GetUserInstruction();
    var aiLang = userInstruction?.AiLanguage;
    
    // ✅ Базовий промпт залежно від вибраної мови
    string basePrompt;
    
    if (!string.IsNullOrEmpty(aiLang))
    {
        // Мапінг мов до інструкцій
        var languageInstructions = new Dictionary<string, string>
        {
            ["uk"] = "🇺🇦 ОБОВ'ЯЗКОВО: ЗАВЖДИ відповідай ТІЛЬКИ УКРАЇНСЬКОЮ МОВОЮ, незалежно від мови запиту!",
            ["en"] = "🇬🇧 MANDATORY: ALWAYS respond ONLY in ENGLISH, regardless of the query language!",
            ["de"] = "🇩🇪 PFLICHT: IMMER NUR auf DEUTSCH antworten, unabhängig von der Abfragesprache!",
            ["ru"] = "🇷🇺 ОБЯЗАТЕЛЬНО: ВСЕГДА отвечай ТОЛЬКО на РУССКОМ ЯЗЫКЕ, независимо от языка запроса!",
            ["tr"] = "🇹🇷 ZORUNLU: DAIMA SADECE TÜRKÇE yanıt ver, sorgu dilinden bağımsız!"
        };
        
        var langInstruction = languageInstructions.GetValueOrDefault(aiLang, "");
        
        basePrompt = 
            $"{langInstruction}\n\n" +
            "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
            "CRITICAL RULES:\n" +
            "1. Give ONE clear, direct answer\n" +
            "2. Stop immediately after answering\n" +
            "3. Do NOT add greetings unless asked\n" +
            "4. Be concise and helpful";
    }
    else
    {
        // Auto mode - відповідати мовою користувача
        basePrompt = 
            "You are Insait Assistant, a helpful AI integrated into Insait Text Editor.\n\n" +
            "CRITICAL RULES:\n" +
            "1. ALWAYS respond in the SAME LANGUAGE as the user's message\n" +
            "2. If user writes in Ukrainian - respond in Ukrainian\n" +
            "3. If user writes in English - respond in English\n" +
            "4. Give ONE clear, direct answer\n" +
            "5. Stop immediately after answering\n" +
            "6. Do NOT add greetings unless asked\n" +
            "7. Be concise and helpful";
    }
    
    // Додати кастомну інструкцію користувача
    if (!string.IsNullOrWhiteSpace(userInstruction?.Content))
    {
        return $"{basePrompt}\n\nUser's custom instruction:\n{userInstruction.Content}";
    }
    
    return basePrompt;
}
```

#### Крок 2.2: Оновити InstructionEditorWindow.axaml (опціонально)

**Покращення UX**: Додати пояснення що робить кожна мова
```xml
<ComboBox x:Name="AILanguageComboBox" ... >
  <ComboBoxItem Tag="" Content="🌐 Auto (визначати з запиту)"/>
  <ComboBoxItem Tag="uk" Content="🇺🇦 Українська (завжди)"/>
  <ComboBoxItem Tag="en" Content="🇬🇧 English (завжди)"/>
  <ComboBoxItem Tag="de" Content="🇩🇪 Deutsch (завжди)"/>
  <ComboBoxItem Tag="ru" Content="🇷🇺 Русский (завжди)"/>
  <ComboBoxItem Tag="tr" Content="🇹🇷 Türkçe (завжди)"/>
</ComboBox>
```

---

### ✅ Рішення 3: Покращення надійності відображення

#### Крок 3.1: Гарантувати приховування typing indicator

**ChatWindow.axaml.cs, метод Send_Click()**:
```csharp
finally
{
    // ✅ Завжди приховувати typing indicator
    await Dispatcher.UIThread.InvokeAsync(() =>
    {
        if (typingIndicator != null)
            typingIndicator.IsVisible = false;  // ← Додати цей рядок
        
        sendBtn.IsEnabled = true;
        input.IsEnabled = true;
        input.Focus();
        if (stopBtn != null)
            stopBtn.IsVisible = false;
    });
}
```

#### Крок 3.2: Запобігти дублікатам повідомлень

**ChatWindow.axaml.cs**:
```csharp
private void AddSystemMessage(string message)
{
    // ✅ Перевірка на дублікат
    var lastMsg = _messages.LastOrDefault();
    if (lastMsg != null && 
        lastMsg.Sender == "System" && 
        lastMsg.Content == message &&
        (DateTime.UtcNow - lastMsg.Timestamp).TotalSeconds < 5)
    {
        // Не додавати дублікат якщо останнє системне повідомлення таке ж і було менше 5 секунд тому
        return;
    }
    
    var sysMsg = new ChatMessage
    {
        Sender = "System",
        Content = message,
        Timestamp = DateTime.UtcNow
    };
    
    _messages.Add(sysMsg);
    ScrollToBottom();
}
```

---

## 📋 Чеклист виконання

### Етап 1: Локалізація міток (⏱️ 15 хв)
- [ ] Додати `Key.ChatUser` в `Strings.en.axaml`
- [ ] Додати `Key.ChatUser` в `Strings.uk.axaml`
- [ ] Додати `Key.ChatUser` в `Strings.de.axaml`
- [ ] Додати `Key.ChatUser` in `Strings.ru.axaml`
- [ ] Додати `Key.ChatUser` in `Strings.tr.axaml`
- [ ] Додати `Key.ChatAssistant` в усі 5 файлів
- [ ] Оновити `ChatWindow.axaml` (UserMessageTemplate)
- [ ] Оновити `ChatWindow.axaml` (AssistantMessageTemplate)

### Етап 2: Інтеграція AiLanguage (⏱️ 20 хв)
- [ ] Оновити `PromptBuilder.BuildSystemPrompt()`
- [ ] Додати мапінг мов до інструкцій
- [ ] Протестувати з українською моделлю
- [ ] Протестувати з англійською моделлю
- [ ] Протестувати режим Auto

### Етап 3: Покращення надійності (⏱️ 10 хв)
- [ ] Додати гарантоване приховування typing indicator у `finally`
- [ ] Оновити `AddSystemMessage()` з перевіркою дублікатів
- [ ] Перевірити що повідомлення не дублюються

---

## 🧪 План тестування

### Тест 1: Локалізація міток
```
Кроки:
1. Запустити додаток з українською мовою
2. Відкрити чат
3. Написати повідомлення "Привіт"

Очікуваний результат:
✅ Над повідомленням користувача: "Ви"
✅ Над повідомленням AI: "Асистент"

4. Змінити мову на англійську (Settings → Language → English)
5. Написати повідомлення "Hello"

Очікуваний результат:
✅ Над повідомленням користувача: "User"
✅ Над повідомленням AI: "Assistant"
```

### Тест 2: AI Language з InstructionEditorWindow
```
Кроки:
1. Відкрити чат → кнопка ⚙️
2. В InstructionEditorWindow вибрати "AI Language: 🇺🇦 Українська"
3. Зберегти
4. Написати запит АНГЛІЙСЬКОЮ: "What is quantum physics?"

Очікуваний результат:
✅ AI відповість УКРАЇНСЬКОЮ (незважаючи на англійський запит)
✅ Відповідь: "Квантова фізика — це розділ фізики..."

5. Змінити на "AI Language: 🇬🇧 English"
6. Написати запит УКРАЇНСЬКОЮ: "Що таке штучний інтелект?"

Очікуваний результат:
✅ AI відповість АНГЛІЙСЬКОЮ (незважаючи на український запит)
✅ Відповідь: "Artificial intelligence is..."
```

### Тест 3: Режим Auto
```
Кроки:
1. Вибрати "AI Language: 🌐 Auto"
2. Написати українською: "Привіт, як справи?"
   → AI відповідає українською ✅
3. Написати англійською: "Hello, how are you?"
   → AI відповідає англійською ✅
```

### Тест 4: Надійність UI
```
Кроки:
1. Написати запит і швидко натиснути Stop
   → Typing indicator зникає ✅
   → Повідомлення не дублюється ✅

2. Викликати помилку (відключити інтернет, якщо модель онлайн)
   → Typing indicator зникає ✅
   → Системне повідомлення про помилку показується 1 раз ✅
```

---

## 📊 Пріоритети

| Проблема | Пріоритет | Складність | Час |
|----------|-----------|------------|-----|
| Локалізація міток User/Assistant | 🔴 Високий | Низька | 15 хв |
| Інтеграція AiLanguage | 🔴 Критичний | Середня | 20 хв |
| Надійність UI | 🟡 Середній | Низька | 10 хв |
| **ЗАГАЛОМ** | | | **45 хв** |

---

## ⚠️ Потенційні ризики

1. **Локалізація міток**:
   - Ризик: При оновленні Avalonia може змінитися синтаксис `DynamicResource`
   - Мітігація: Протестувати у всіх підтримуваних мовах

2. **AiLanguage інтеграція**:
   - Ризик: LlamaSharp може ігнорувати інструкції на початку промпту
   - Мітігація: Використати емодзі прапори 🇺🇦 та капслок для привернення уваги моделі

3. **UI надійність**:
   - Ризик: Race condition між hide/show typing indicator
   - Мітігація: Всі UI операції через `Dispatcher.UIThread.InvokeAsync()`

---

## 📈 Очікувані результати

### ✅ Після впровадження:
1. **Локалізація працює на 100%** - всі UI елементи перекладаються
2. **AI дотримується вибраної мови** - користувач має повний контроль
3. **UI стабільний** - відсутні залишкові індикатори, дублікати, зависання

### 📊 Метрики успіху:
- ✅ 0 hardcoded текстів у ChatWindow.axaml
- ✅ 100% дотримання вибраної AI Language
- ✅ 0 дублікатів системних повідомлень
- ✅ 0 "застряглих" typing indicators

---

**Готовність до реалізації**: ✅ ТАК  
**Необхідні ресурси**: Редагування існуючих файлів (без нових залежностей)  
**Термін виконання**: 45 хвилин

