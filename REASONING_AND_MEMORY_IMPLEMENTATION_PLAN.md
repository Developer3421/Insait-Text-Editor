# 🧠 План впровадження Reasoning Mode та Global Memory

**Проект**: InsaitTextEditor  
**Дата створення**: 19 жовтня 2025  
**Версія**: 1.0  
**Автор**: GitHub Copilot

---

## 📋 Зміст

1. [Огляд функціоналу](#огляд-функціоналу)
2. [Архітектура рішення](#архітектура-рішення)
3. [Структура нових файлів](#структура-нових-файлів)
4. [База даних (LiteDB)](#база-даних-litedb)
5. [Reasoning Mode](#reasoning-mode)
6. [Global Memory](#global-memory)
7. [UI Integration](#ui-integration)
8. [Етапи впровадження](#етапи-впровадження)
9. [Тестування](#тестування)

---

## 🎯 Огляд функціоналу

### Reasoning Mode (Режим міркування)

**Що це?**
- AI розбиває складні запити на кроки
- Показує процес міркування користувачу
- Використовує Chain-of-Thought (CoT) prompting
- Зберігає reasoning chains у базі даних

**Приклади використання:**
- Математичні задачі з поясненням
- Програмування з покроковою логікою
- Аналіз текстів з аргументацією
- Планування з декомпозицією

### Global Memory (Глобальна пам'ять)

**Що це?**
- Запам'ятовування фактів про користувача між сесіями
- Контекстна інформація для кращих відповідей
- Семантичний пошук по історії
- Автоматичне витягування важливої інформації

**Приклади використання:**
- "Я програміст на C#" → AI запам'ятовує для майбутніх відповідей
- "Мій проект називається Insait" → AI знає контекст
- Посилання на попередні розмови
- Персоналізовані рекомендації

---

## 🏗️ Архітектура рішення

```
┌─────────────────────────────────────────────────────────────┐
│                   InstructionEditorWindow                    │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Reasoning  │  │ Global Memory│  │  AI Language │      │
│  │   Toggle     │  │   Toggle     │  │   Dropdown   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
└─────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────┐
│                      UserInstruction                         │
│  • Content                                                   │
│  • AiLanguage                                                │
│  • ReasoningEnabled       ← NEW                             │
│  • GlobalMemoryEnabled    ← NEW                             │
└─────────────────────────────────────────────────────────────┘
                            │
        ┌───────────────────┴───────────────────┐
        │                                       │
        ▼                                       ▼
┌──────────────────┐                  ┌──────────────────┐
│ ReasoningService │                  │  MemoryService   │
│                  │                  │                  │
│ • GenerateSteps  │                  │ • SaveFact       │
│ • ExecuteStep    │                  │ • QueryMemory    │
│ • SaveChain      │                  │ • ExtractFacts   │
└──────────────────┘                  └──────────────────┘
        │                                       │
        ▼                                       ▼
┌──────────────────────────────────────────────────────────┐
│                    LiteDB Database                        │
│  ┌────────────────┐  ┌──────────────┐  ┌──────────────┐ │
│  │ reasoning_chains│  │ memory_facts │  │ memory_index │ │
│  └────────────────┘  └──────────────┘  └──────────────┘ │
└──────────────────────────────────────────────────────────┘
```

---

## 📁 Структура нових файлів

### Повна структура проекту з новими файлами

```
InsaitTextEditor/
│
├── Models/                           [ІСНУЮЧІ + НОВІ]
│   ├── UserInstruction.cs            ✏️ EDIT (додати 2 поля)
│   ├── ChatMessage.cs                ✅ існуючий
│   ├── AgentResponse.cs              ✅ існуючий
│   │
│   ├── Reasoning/                    🆕 НОВА ПАПКА
│   │   ├── ReasoningChain.cs         🆕 NEW (ланцюг міркувань)
│   │   ├── ReasoningStep.cs          🆕 NEW (один крок)
│   │   └── StepStatus.cs             🆕 NEW (enum: Pending/Running/Done)
│   │
│   └── Memory/                       🆕 НОВА ПАПКА
│       ├── MemoryFact.cs             🆕 NEW (один факт з пам'яті)
│       ├── MemoryQuery.cs            🆕 NEW (запит до пам'яті)
│       ├── FactType.cs               🆕 NEW (enum: Personal/Project/Preference)
│       └── MemorySearchResult.cs     🆕 NEW (результат пошуку)
│
├── Services/                         [ІСНУЮЧІ + НОВІ]
│   ├── DatabaseService.cs            ✅ існуючий
│   ├── ChatHistoryService.cs         ✅ існуючий
│   ├── UserInstructionService.cs     ✅ існуючий
│   │
│   ├── Reasoning/                    🆕 НОВА ПАПКА
│   │   ├── ReasoningService.cs       🆕 NEW (основний сервіс)
│   │   ├── ReasoningPromptBuilder.cs 🆕 NEW (спеціальні промпти для CoT)
│   │   └── ReasoningChainStorage.cs  🆕 NEW (збереження в LiteDB)
│   │
│   └── Memory/                       🆕 НОВА ПАПКА
│       ├── MemoryService.cs          🆕 NEW (основний сервіс)
│       ├── MemoryExtractor.cs        🆕 NEW (витягує факти з розмов)
│       ├── MemoryStorage.cs          🆕 NEW (збереження в LiteDB)
│       └── MemoryQueryEngine.cs      🆕 NEW (семантичний пошук)
│
├── AI/                               [ІСНУЮЧІ + ОНОВЛЕНІ]
│   ├── PromptBuilder.cs              ✏️ EDIT (інтеграція reasoning + memory)
│   ├── LlamaSharpInferenceEngine.cs  ✏️ EDIT (підтримка streaming для steps)
│   ├── GemmaModelManager.cs          ✅ існуючий
│   └── MicrosoftAgentsAdapter.cs     ✅ існуючий
│
├── Windows/                          [ІСНУЮЧІ + ОНОВЛЕНІ]
│   ├── InstructionEditorWindow.axaml ✏️ EDIT (додати toggles + UI)
│   ├── InstructionEditorWindow.axaml.cs ✏️ EDIT (обробка нових полів)
│   ├── ChatWindow.axaml              ✏️ EDIT (відображення reasoning steps)
│   └── ChatWindow.axaml.cs           ✏️ EDIT (інтеграція сервісів)
│
├── UI/                               [ІСНУЮЧІ + НОВІ]
│   ├── Localization/                 
│   │   ├── Strings.en.axaml          ✏️ EDIT (додати ключі)
│   │   ├── Strings.uk.axaml          ✏️ EDIT (додати ключі)
│   │   ├── Strings.de.axaml          ✏️ EDIT (додати ключі)
│   │   ├── Strings.ru.axaml          ✏️ EDIT (додати ключі)
│   │   └── Strings.tr.axaml          ✏️ EDIT (додати ключі)
│   │
│   └── Controls/                     🆕 НОВА ПАПКА (опціонально)
│       ├── ReasoningStepView.axaml   🆕 NEW (відображення одного кроку)
│       └── MemoryFactCard.axaml      🆕 NEW (картка з фактом)
│
└── Database/                         [ІСНУЮЧА ПАПКА]
    └── documents.litedb              ✏️ EXTENDED (нові колекції)
```

### Підсумок нових файлів

| Категорія | Файлів | Опис |
|-----------|---------|------|
| **Models (нові)** | 7 | Моделі даних для reasoning та memory |
| **Services (нові)** | 7 | Бізнес-логіка |
| **Existing (edit)** | 8 | Оновлення існуючих файлів |
| **UI Controls (опціонально)** | 2 | Custom controls для відображення |
| **ЗАГАЛОМ** | **24 файли** | 14 нових + 10 оновлень |

---

## 💾 База даних (LiteDB)

### Схема колекцій

#### 1. `reasoning_chains` (Ланцюги міркувань)

```csharp
{
  "Id": "guid",                        // Унікальний ID ланцюга
  "ConversationId": "guid",            // ID розмови з ChatWindow
  "UserQuery": "string",               // Початковий запит користувача
  "CreatedAt": "DateTime",
  "CompletedAt": "DateTime?",
  "Status": "enum",                    // Pending, InProgress, Completed, Failed
  "Steps": [                           // Масив кроків
    {
      "StepNumber": 1,
      "Title": "Analyze problem",
      "Content": "First, let's break down...",
      "Status": "Completed",
      "StartedAt": "DateTime",
      "CompletedAt": "DateTime"
    },
    // ... більше кроків
  ],
  "FinalAnswer": "string",             // Фінальна відповідь після всіх кроків
  "TotalTokensUsed": 1234
}
```

#### 2. `memory_facts` (Факти з пам'яті)

```csharp
{
  "Id": "guid",
  "Content": "User is a C# developer",  // Сам факт
  "FactType": "enum",                   // Personal, Project, Preference, Context
  "Source": "string",                   // З якої розмови витягнуто
  "SourceMessageId": "guid",            // ID повідомлення-джерела
  "ExtractedAt": "DateTime",
  "LastAccessedAt": "DateTime",         // Коли востаннє використовувався
  "Confidence": 0.95,                   // Наскільки AI впевнений (0-1)
  "Tags": ["programming", "csharp"],    // Для пошуку
  "IsActive": true,                     // Чи актуальний факт
  "UpdatedBy": "guid?"                  // Якщо факт було оновлено
}
```

#### 3. `memory_index` (Індекс для семантичного пошуку)

```csharp
{
  "Id": "guid",
  "FactId": "guid",                     // Посилання на memory_facts
  "Keywords": ["developer", "C#", "programming"], // Ключові слова
  "EmbeddingHash": "string",            // Спрощений хеш для швидкого пошуку
  "RelatedFactIds": ["guid1", "guid2"]  // Пов'язані факти
}
```

### Індекси для швидкого пошуку

```csharp
// В ReasoningChainStorage.cs
collection.EnsureIndex(x => x.ConversationId);
collection.EnsureIndex(x => x.CreatedAt);
collection.EnsureIndex(x => x.Status);

// В MemoryStorage.cs
collection.EnsureIndex(x => x.FactType);
collection.EnsureIndex(x => x.Tags);
collection.EnsureIndex(x => x.IsActive);
collection.EnsureIndex(x => x.LastAccessedAt);
```

---

## 🧩 Reasoning Mode

### Як це працює?

#### Крок 1: Користувач ввімкнув Reasoning Mode
```
User: "Розв'яжи рівняння: 2x + 5 = 15"
```

#### Крок 2: ReasoningService генерує план
```
Step 1: Identify equation type → Linear equation
Step 2: Isolate variable → Subtract 5 from both sides
Step 3: Solve for x → Divide by 2
Step 4: Verify solution → Substitute back
```

#### Крок 3: Виконання кожного кроку
```
[Step 1/4] 🔍 Identifying equation type...
✅ This is a linear equation in one variable

[Step 2/4] 📝 Isolating the variable...
2x + 5 = 15
2x = 15 - 5
2x = 10
✅ Variable term isolated

[Step 3/4] 🧮 Solving for x...
2x = 10
x = 10 / 2
x = 5
✅ Solution found

[Step 4/4] ✔️ Verifying solution...
2(5) + 5 = 10 + 5 = 15 ✓
✅ Solution verified

🎯 Final Answer: x = 5
```

### Код: Models/Reasoning/ReasoningChain.cs

```csharp
using System;
using System.Collections.Generic;

namespace InsaitTextEditor.Models.Reasoning;

public class ReasoningChain
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public string UserQuery { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public ChainStatus Status { get; set; } = ChainStatus.Pending;
    public List<ReasoningStep> Steps { get; set; } = new();
    public string FinalAnswer { get; set; } = string.Empty;
    public int TotalTokensUsed { get; set; }
}

public enum ChainStatus
{
    Pending,      // Ще не розпочато
    InProgress,   // Виконується
    Completed,    // Успішно завершено
    Failed        // Помилка
}
```

### Код: Models/Reasoning/ReasoningStep.cs

```csharp
using System;

namespace InsaitTextEditor.Models.Reasoning;

public class ReasoningStep
{
    public int StepNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public StepStatus Status { get; set; } = StepStatus.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum StepStatus
{
    Pending,   // Очікує виконання
    Running,   // Виконується зараз
    Completed, // Завершено
    Failed     // Помилка
}
```

### Код: Services/Reasoning/ReasoningService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Reasoning;
using InsaitTextEditor.AI;

namespace InsaitTextEditor.Services.Reasoning;

/// <summary>
/// Сервіс для виконання reasoning chains (Chain-of-Thought)
/// </summary>
public class ReasoningService
{
    private readonly LlamaSharpInferenceEngine _inferenceEngine;
    private readonly ReasoningPromptBuilder _promptBuilder;
    private readonly ReasoningChainStorage _storage;

    public ReasoningService(
        LlamaSharpInferenceEngine inferenceEngine,
        ReasoningChainStorage storage)
    {
        _inferenceEngine = inferenceEngine;
        _promptBuilder = new ReasoningPromptBuilder();
        _storage = storage;
    }

    /// <summary>
    /// Генерує reasoning chain для запиту
    /// </summary>
    public async Task<ReasoningChain> GenerateReasoningChainAsync(
        string userQuery, 
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        var chain = new ReasoningChain
        {
            UserQuery = userQuery,
            ConversationId = conversationId,
            Status = ChainStatus.InProgress
        };

        try
        {
            // Крок 1: Згенерувати план (які кроки потрібні)
            var planPrompt = _promptBuilder.BuildPlanningPrompt(userQuery);
            var planResponse = await _inferenceEngine.GenerateAsync(planPrompt, cancellationToken);
            
            var steps = _promptBuilder.ParsePlanIntoSteps(planResponse);
            chain.Steps = steps;

            // Зберегти початковий стан
            await _storage.SaveChainAsync(chain);

            // Крок 2: Виконати кожен крок
            foreach (var step in chain.Steps)
            {
                step.Status = StepStatus.Running;
                step.StartedAt = DateTime.UtcNow;
                await _storage.UpdateChainAsync(chain);

                // Генерувати відповідь для цього кроку
                var stepPrompt = _promptBuilder.BuildStepPrompt(userQuery, step, chain.Steps);
                var stepResponse = await _inferenceEngine.GenerateAsync(stepPrompt, cancellationToken);

                step.Content = stepResponse;
                step.Status = StepStatus.Completed;
                step.CompletedAt = DateTime.UtcNow;
                await _storage.UpdateChainAsync(chain);
            }

            // Крок 3: Згенерувати фінальну відповідь
            var finalPrompt = _promptBuilder.BuildFinalAnswerPrompt(userQuery, chain.Steps);
            chain.FinalAnswer = await _inferenceEngine.GenerateAsync(finalPrompt, cancellationToken);
            
            chain.Status = ChainStatus.Completed;
            chain.CompletedAt = DateTime.UtcNow;
            await _storage.SaveChainAsync(chain);

            return chain;
        }
        catch (Exception ex)
        {
            chain.Status = ChainStatus.Failed;
            await _storage.SaveChainAsync(chain);
            throw;
        }
    }

    /// <summary>
    /// Отримати історію reasoning chains для розмови
    /// </summary>
    public Task<List<ReasoningChain>> GetChainsForConversationAsync(Guid conversationId)
    {
        return _storage.GetChainsByConversationAsync(conversationId);
    }
}
```

### Код: Services/Reasoning/ReasoningPromptBuilder.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using InsaitTextEditor.Models.Reasoning;

namespace InsaitTextEditor.Services.Reasoning;

/// <summary>
/// Генерує спеціальні промпти для Chain-of-Thought reasoning
/// </summary>
public class ReasoningPromptBuilder
{
    public string BuildPlanningPrompt(string userQuery)
    {
        return $@"You are a reasoning assistant. Break down this problem into logical steps.

USER QUERY: {userQuery}

Generate a step-by-step plan to solve this. Output ONLY the steps in this format:
Step 1: [Brief title]
Step 2: [Brief title]
Step 3: [Brief title]

Maximum 5 steps. Be concise.";
    }

    public List<ReasoningStep> ParsePlanIntoSteps(string planResponse)
    {
        var steps = new List<ReasoningStep>();
        var lines = planResponse.Split('\n');
        
        int stepNumber = 1;
        foreach (var line in lines)
        {
            if (line.StartsWith("Step "))
            {
                var title = line.Substring(line.IndexOf(':') + 1).Trim();
                steps.Add(new ReasoningStep
                {
                    StepNumber = stepNumber++,
                    Title = title,
                    Status = StepStatus.Pending
                });
            }
        }

        return steps;
    }

    public string BuildStepPrompt(string userQuery, ReasoningStep currentStep, List<ReasoningStep> allSteps)
    {
        var previousSteps = allSteps
            .Where(s => s.StepNumber < currentStep.StepNumber && s.Status == StepStatus.Completed)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"ORIGINAL QUERY: {userQuery}");
        sb.AppendLine();
        sb.AppendLine($"CURRENT STEP ({currentStep.StepNumber}): {currentStep.Title}");
        sb.AppendLine();

        if (previousSteps.Any())
        {
            sb.AppendLine("PREVIOUS STEPS:");
            foreach (var prev in previousSteps)
            {
                sb.AppendLine($"Step {prev.StepNumber}: {prev.Title}");
                sb.AppendLine($"Result: {prev.Content}");
                sb.AppendLine();
            }
        }

        sb.AppendLine("Now execute THIS step and provide the result. Be clear and concise.");
        
        return sb.ToString();
    }

    public string BuildFinalAnswerPrompt(string userQuery, List<ReasoningStep> completedSteps)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ORIGINAL QUERY: {userQuery}");
        sb.AppendLine();
        sb.AppendLine("REASONING STEPS COMPLETED:");
        
        foreach (var step in completedSteps)
        {
            sb.AppendLine($"Step {step.StepNumber}: {step.Title}");
            sb.AppendLine($"Result: {step.Content}");
            sb.AppendLine();
        }

        sb.AppendLine("Now provide a clear, final answer to the original query based on the reasoning above.");
        sb.AppendLine("Be concise but complete.");
        
        return sb.ToString();
    }
}
```

### Код: Services/Reasoning/ReasoningChainStorage.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Reasoning;
using LiteDB;

namespace InsaitTextEditor.Services.Reasoning;

/// <summary>
/// Зберігає reasoning chains у LiteDB
/// </summary>
public class ReasoningChainStorage
{
    private readonly DatabaseService _dbService;
    private const string CollectionName = "reasoning_chains";

    public ReasoningChainStorage(DatabaseService dbService)
    {
        _dbService = dbService;
        InitializeIndexes();
    }

    private void InitializeIndexes()
    {
        var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
        collection.EnsureIndex(x => x.ConversationId);
        collection.EnsureIndex(x => x.CreatedAt);
        collection.EnsureIndex(x => x.Status);
    }

    public Task SaveChainAsync(ReasoningChain chain)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            collection.Upsert(chain);
        });
    }

    public Task UpdateChainAsync(ReasoningChain chain)
    {
        return SaveChainAsync(chain);
    }

    public Task<ReasoningChain?> GetChainByIdAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            return collection.FindById(id);
        });
    }

    public Task<List<ReasoningChain>> GetChainsByConversationAsync(Guid conversationId)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            return collection.Find(x => x.ConversationId == conversationId)
                .OrderBy(x => x.CreatedAt)
                .ToList();
        });
    }

    public Task<List<ReasoningChain>> GetRecentChainsAsync(int limit = 10)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<ReasoningChain>(CollectionName);
            return collection.FindAll()
                .OrderByDescending(x => x.CreatedAt)
                .Take(limit)
                .ToList();
        });
    }
}
```

---

## 💾 Global Memory

### Як це працює?

#### Сценарій 1: Збереження фактів

```
Day 1:
User: "Я працюю над проектом Insait Text Editor на C#"
AI: "Чудово! Я запам'ятаю що ви працюєте над Insait Text Editor на C#."
[Система зберігає факти:]
- FactType: Project, Content: "User works on Insait Text Editor"
- FactType: Personal, Content: "User is a C# developer"

Day 2:
User: "Допоможи з багом"
AI: [Автоматично завантажує контекст]
    "Звісно! Чим можу допомогти з Insait Text Editor?"
```

#### Сценарій 2: Семантичний пошук

```
User: "Які мови програмування я використовую?"
[MemoryQueryEngine шукає:]
- Keywords: ["programming", "language", "code"]
- FactType: Personal, Project
→ Знаходить: "C# developer", "Insait project", "Avalonia UI"

AI: "Згідно з нашими розмовами, ви використовуєте C# для розробки 
     Insait Text Editor з Avalonia UI."
```

### Код: Models/Memory/MemoryFact.cs

```csharp
using System;
using System.Collections.Generic;

namespace InsaitTextEditor.Models.Memory;

public class MemoryFact
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Content { get; set; } = string.Empty;
    public FactType Type { get; set; }
    public string Source { get; set; } = string.Empty;  // "Chat", "User Edit", etc.
    public Guid? SourceMessageId { get; set; }
    public DateTime ExtractedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastAccessedAt { get; set; } = DateTime.UtcNow;
    public double Confidence { get; set; } = 1.0;  // 0.0 to 1.0
    public List<string> Tags { get; set; } = new();
    public bool IsActive { get; set; } = true;
    public Guid? UpdatedBy { get; set; }  // Якщо факт було оновлено іншим фактом
}

public enum FactType
{
    Personal,      // Інформація про користувача (ім'я, професія, вподобання)
    Project,       // Інформація про проект (назва, технології, цілі)
    Preference,    // Вподобання (стиль коду, мова відповідей)
    Context        // Контекстна інформація (поточна задача, проблеми)
}
```

### Код: Services/Memory/MemoryService.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Основний сервіс для роботи з глобальною пам'яттю
/// </summary>
public class MemoryService
{
    private readonly MemoryStorage _storage;
    private readonly MemoryExtractor _extractor;
    private readonly MemoryQueryEngine _queryEngine;

    public MemoryService(DatabaseService dbService, LlamaSharpInferenceEngine inferenceEngine)
    {
        _storage = new MemoryStorage(dbService);
        _extractor = new MemoryExtractor(inferenceEngine);
        _queryEngine = new MemoryQueryEngine(_storage);
    }

    /// <summary>
    /// Витягує факти з повідомлення та зберігає їх
    /// </summary>
    public async Task ProcessMessageAsync(ChatMessage message)
    {
        if (message.Sender == "System") return;

        // Витягти факти з повідомлення користувача
        var facts = await _extractor.ExtractFactsAsync(message.Content);
        
        foreach (var fact in facts)
        {
            fact.SourceMessageId = Guid.NewGuid(); // ID повідомлення
            fact.Source = "Chat";
            await _storage.SaveFactAsync(fact);
        }
    }

    /// <summary>
    /// Шукає релевантні факти для запиту
    /// </summary>
    public async Task<List<MemoryFact>> QueryMemoryAsync(string query, int maxResults = 5)
    {
        var results = await _queryEngine.SearchAsync(query, maxResults);
        
        // Оновити LastAccessedAt для використаних фактів
        foreach (var fact in results)
        {
            fact.LastAccessedAt = DateTime.UtcNow;
            await _storage.UpdateFactAsync(fact);
        }
        
        return results;
    }

    /// <summary>
    /// Отримати всі факти певного типу
    /// </summary>
    public Task<List<MemoryFact>> GetFactsByTypeAsync(FactType type)
    {
        return _storage.GetFactsByTypeAsync(type);
    }

    /// <summary>
    /// Видалити факт
    /// </summary>
    public Task DeleteFactAsync(Guid factId)
    {
        return _storage.DeleteFactAsync(factId);
    }

    /// <summary>
    /// Отримати контекст для нової розмови (найважливіші факти)
    /// </summary>
    public async Task<string> BuildContextPromptAsync(int maxFacts = 10)
    {
        var recentFacts = await _storage.GetRecentFactsAsync(maxFacts);
        
        if (!recentFacts.Any())
            return string.Empty;

        var contextLines = new List<string>
        {
            "=== CONTEXT FROM MEMORY ===",
            "Here's what I remember about you:",
            ""
        };

        foreach (var fact in recentFacts)
        {
            contextLines.Add($"• {fact.Content}");
        }

        contextLines.Add("");
        contextLines.Add("Use this context to provide personalized responses.");
        contextLines.Add("===========================");

        return string.Join("\n", contextLines);
    }
}
```

### Код: Services/Memory/MemoryExtractor.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using InsaitTextEditor.AI;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Витягує факти з повідомлень користувача за допомогою AI
/// </summary>
public class MemoryExtractor
{
    private readonly LlamaSharpInferenceEngine _inferenceEngine;

    public MemoryExtractor(LlamaSharpInferenceEngine inferenceEngine)
    {
        _inferenceEngine = inferenceEngine;
    }

    public async Task<List<MemoryFact>> ExtractFactsAsync(string message)
    {
        var prompt = BuildExtractionPrompt(message);
        var response = await _inferenceEngine.GenerateAsync(prompt, default);
        
        return ParseFactsFromResponse(response);
    }

    private string BuildExtractionPrompt(string message)
    {
        return $@"Analyze this message and extract important facts about the user.
Only extract facts that are worth remembering long-term.

MESSAGE: {message}

Extract facts in this JSON format:
[
  {{""type"": ""Personal"", ""content"": ""User is a software developer"", ""tags"": [""profession"", ""developer""]}},
  {{""type"": ""Project"", ""content"": ""Working on Insait Text Editor"", ""tags"": [""project"", ""insait""]}}
]

Types: Personal, Project, Preference, Context
Output ONLY valid JSON array, or empty array [] if no facts found.";
    }

    private List<MemoryFact> ParseFactsFromResponse(string response)
    {
        try
        {
            // Спроба парсити JSON
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var factDtos = JsonSerializer.Deserialize<List<FactDto>>(jsonStr);
                
                return factDtos?.Select(dto => new MemoryFact
                {
                    Content = dto.Content,
                    Type = ParseFactType(dto.Type),
                    Tags = dto.Tags ?? new List<string>(),
                    Confidence = 0.8 // AI-extracted facts мають нижчу впевненість
                }).ToList() ?? new List<MemoryFact>();
            }
        }
        catch
        {
            // Якщо парсинг не вдався, повертаємо пустий список
        }

        return new List<MemoryFact>();
    }

    private FactType ParseFactType(string type)
    {
        return type switch
        {
            "Personal" => FactType.Personal,
            "Project" => FactType.Project,
            "Preference" => FactType.Preference,
            "Context" => FactType.Context,
            _ => FactType.Context
        };
    }

    private class FactDto
    {
        public string Type { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public List<string>? Tags { get; set; }
    }
}
```

### Код: Services/Memory/MemoryStorage.cs

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;
using LiteDB;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Зберігає факти пам'яті у LiteDB
/// </summary>
public class MemoryStorage
{
    private readonly DatabaseService _dbService;
    private const string FactsCollection = "memory_facts";
    private const string IndexCollection = "memory_index";

    public MemoryStorage(DatabaseService dbService)
    {
        _dbService = dbService;
        InitializeIndexes();
    }

    private void InitializeIndexes()
    {
        var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
        collection.EnsureIndex(x => x.Type);
        collection.EnsureIndex(x => x.Tags);
        collection.EnsureIndex(x => x.IsActive);
        collection.EnsureIndex(x => x.LastAccessedAt);
        collection.EnsureIndex(x => x.ExtractedAt);
    }

    public Task SaveFactAsync(MemoryFact fact)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            collection.Upsert(fact);
        });
    }

    public Task UpdateFactAsync(MemoryFact fact)
    {
        return SaveFactAsync(fact);
    }

    public Task<MemoryFact?> GetFactByIdAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.FindById(id);
        });
    }

    public Task<List<MemoryFact>> GetFactsByTypeAsync(FactType type)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Find(x => x.Type == type && x.IsActive)
                .OrderByDescending(x => x.LastAccessedAt)
                .ToList();
        });
    }

    public Task<List<MemoryFact>> GetRecentFactsAsync(int limit = 10)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Find(x => x.IsActive)
                .OrderByDescending(x => x.LastAccessedAt)
                .ThenByDescending(x => x.Confidence)
                .Take(limit)
                .ToList();
        });
    }

    public Task<List<MemoryFact>> SearchByTagsAsync(List<string> tags)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Find(x => x.IsActive && x.Tags.Any(t => tags.Contains(t)))
                .OrderByDescending(x => x.Confidence)
                .ToList();
        });
    }

    public Task DeleteFactAsync(Guid id)
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            collection.Delete(id);
        });
    }

    public Task<int> GetTotalFactsCountAsync()
    {
        return Task.Run(() =>
        {
            var collection = _dbService.GetCollection<MemoryFact>(FactsCollection);
            return collection.Count(x => x.IsActive);
        });
    }
}
```

### Код: Services/Memory/MemoryQueryEngine.cs

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InsaitTextEditor.Models.Memory;

namespace InsaitTextEditor.Services.Memory;

/// <summary>
/// Шукає релевантні факти за запитом (спрощений семантичний пошук)
/// </summary>
public class MemoryQueryEngine
{
    private readonly MemoryStorage _storage;

    public MemoryQueryEngine(MemoryStorage storage)
    {
        _storage = storage;
    }

    public async Task<List<MemoryFact>> SearchAsync(string query, int maxResults = 5)
    {
        // Витягти ключові слова з запиту
        var keywords = ExtractKeywords(query);
        
        // Шукати по тегах
        var factsByTags = await _storage.SearchByTagsAsync(keywords);
        
        // Якщо нічого не знайдено, повернути останні факти
        if (!factsByTags.Any())
        {
            return await _storage.GetRecentFactsAsync(maxResults);
        }

        // Ранжувати за релевантністю
        var rankedFacts = factsByTags
            .Select(fact => new
            {
                Fact = fact,
                Score = CalculateRelevanceScore(fact, keywords)
            })
            .OrderByDescending(x => x.Score)
            .Take(maxResults)
            .Select(x => x.Fact)
            .ToList();

        return rankedFacts;
    }

    private List<string> ExtractKeywords(string query)
    {
        // Спрощене витягування ключових слів
        var words = query.ToLower()
            .Split(' ', ',', '.', '!', '?')
            .Where(w => w.Length > 3) // Ігноруємо короткі слова
            .Distinct()
            .ToList();

        return words;
    }

    private double CalculateRelevanceScore(MemoryFact fact, List<string> keywords)
    {
        double score = 0;

        // +1 за кожне співпадіння тегу
        var matchingTags = fact.Tags.Intersect(keywords).Count();
        score += matchingTags * 2.0;

        // +0.5 якщо ключове слово є в content
        foreach (var keyword in keywords)
        {
            if (fact.Content.ToLower().Contains(keyword))
                score += 0.5;
        }

        // Бонус за впевненість
        score *= fact.Confidence;

        // Бонус за свіжість (нещодавно використовувані факти)
        var daysSinceAccess = (System.DateTime.UtcNow - fact.LastAccessedAt).TotalDays;
        if (daysSinceAccess < 7)
            score *= 1.2;

        return score;
    }
}
```

---

## 🎨 UI Integration

### InstructionEditorWindow.axaml - Додаткові контроли

```xml
<!-- Після секції AI Language -->
<StackPanel Grid.Row="2" Margin="0,0,0,15">
  
  <!-- ... існуючі контроли (Context Size, Max Tokens, AI Language) ... -->

  <!-- Reasoning Mode Toggle -->
  <Grid Margin="0,15,0,10">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    
    <StackPanel Grid.Column="0">
      <TextBlock Text="{DynamicResource Key.ReasoningMode}"
                 Foreground="White"
                 FontSize="13"
                 FontWeight="SemiBold"/>
      <TextBlock Text="{DynamicResource Key.ReasoningModeDescription}"
                 Foreground="#AAAAAA"
                 FontSize="11"
                 TextWrapping="Wrap"
                 Margin="0,4,0,0"/>
    </StackPanel>
    
    <ToggleSwitch x:Name="ReasoningModeToggle"
                  Grid.Column="1"
                  VerticalAlignment="Center"
                  OnContent="✓"
                  OffContent="✗"/>
  </Grid>

  <!-- Global Memory Toggle -->
  <Grid Margin="0,0,0,10">
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="Auto"/>
    </Grid.ColumnDefinitions>
    
    <StackPanel Grid.Column="0">
      <TextBlock Text="{DynamicResource Key.GlobalMemory}"
                 Foreground="White"
                 FontSize="13"
                 FontWeight="SemiBold"/>
      <TextBlock Text="{DynamicResource Key.GlobalMemoryDescription}"
                 Foreground="#AAAAAA"
                 FontSize="11"
                 TextWrapping="Wrap"
                 Margin="0,4,0,0"/>
    </StackPanel>
    
    <ToggleSwitch x:Name="GlobalMemoryToggle"
                  Grid.Column="1"
                  VerticalAlignment="Center"
                  OnContent="✓"
                  OffContent="✗"/>
  </Grid>

  <!-- Memory Stats (якщо Global Memory ввімкнено) -->
  <Border Background="#2D2D30"
          BorderBrush="#3F3F46"
          BorderThickness="1"
          CornerRadius="4"
          Padding="10"
          Margin="0,5,0,0"
          IsVisible="{Binding #GlobalMemoryToggle.IsChecked}">
    <StackPanel>
      <TextBlock x:Name="MemoryStatsText"
                 Text="Loading memory stats..."
                 Foreground="#888888"
                 FontSize="11"/>
    </StackPanel>
  </Border>
</StackPanel>
```

### Локалізація (додати в усі 5 мов)

**Strings.uk.axaml:**
```xml
<system:String x:Key="Key.ReasoningMode">🧠 Режим міркування</system:String>
<system:String x:Key="Key.ReasoningModeDescription">AI показуватиме покрокове розв'язання складних задач</system:String>
<system:String x:Key="Key.GlobalMemory">💾 Глобальна пам'ять</system:String>
<system:String x:Key="Key.GlobalMemoryDescription">AI запам'ятовуватиме факти між сесіями для кращого контексту</system:String>
<system:String x:Key="Key.MemoryStats">Збережено фактів: {0} | Останнє оновлення: {1}</system:String>
```

**Strings.en.axaml:**
```xml
<system:String x:Key="Key.ReasoningMode">🧠 Reasoning Mode</system:String>
<system:String x:Key="Key.ReasoningModeDescription">AI will show step-by-step solution for complex problems</system:String>
<system:String x:Key="Key.GlobalMemory">💾 Global Memory</system:String>
<system:String x:Key="Key.GlobalMemoryDescription">AI will remember facts across sessions for better context</system:String>
<system:String x:Key="Key.MemoryStats">Facts saved: {0} | Last update: {1}</system:String>
```

*(Аналогічно для de, ru, tr)*

### ChatWindow - Відображення Reasoning Steps

```xml
<!-- В ChatWindow.axaml, в ItemTemplate для повідомлень -->
<DataTemplate DataType="models:ChatMessage">
  <Border ...>
    <!-- Існуючий контент повідомлення -->
    
    <!-- Reasoning Steps (якщо є) -->
    <ItemsControl Items="{Binding ReasoningSteps}"
                  IsVisible="{Binding HasReasoningSteps}"
                  Margin="0,10,0,0">
      <ItemsControl.ItemTemplate>
        <DataTemplate>
          <Border Background="#1E1E1E"
                  BorderBrush="#3F3F46"
                  BorderThickness="1"
                  CornerRadius="4"
                  Padding="10"
                  Margin="0,5,0,5">
            <StackPanel>
              <TextBlock Text="{Binding Title}"
                         Foreground="#4EC9B0"
                         FontWeight="SemiBold"
                         FontSize="12"/>
              <TextBlock Text="{Binding Content}"
                         Foreground="#D4D4D4"
                         FontSize="11"
                         TextWrapping="Wrap"
                         Margin="0,5,0,0"/>
              <TextBlock Text="{Binding Status}"
                         Foreground="#808080"
                         FontSize="10"
                         HorizontalAlignment="Right"
                         Margin="0,3,0,0"/>
            </StackPanel>
          </Border>
        </DataTemplate>
      </ItemsControl.ItemTemplate>
    </ItemsControl>
  </Border>
</DataTemplate>
```

---

## 🚀 Етапи впровадження

### Етап 1: Підготовка інфраструктури (1-2 дні)

**Завдання:**
1. ✅ Створити папки Models/Reasoning та Models/Memory
2. ✅ Створити папки Services/Reasoning та Services/Memory
3. ✅ Додати нові поля до UserInstruction.cs
4. ✅ Оновити DatabaseService для нових колекцій

**Файли для створення:**
- Models/Reasoning/ReasoningChain.cs
- Models/Reasoning/ReasoningStep.cs
- Models/Memory/MemoryFact.cs
- Models/Memory/FactType.cs

**Файли для оновлення:**
- Models/UserInstruction.cs (додати ReasoningEnabled, GlobalMemoryEnabled)

### Етап 2: Reasoning Mode (2-3 дні)

**Завдання:**
1. ✅ Створити ReasoningService
2. ✅ Створити ReasoningPromptBuilder
3. ✅ Створити ReasoningChainStorage
4. ✅ Інтегрувати в ChatWindow

**Файли для створення:**
- Services/Reasoning/ReasoningService.cs
- Services/Reasoning/ReasoningPromptBuilder.cs
- Services/Reasoning/ReasoningChainStorage.cs

**Файли для оновлення:**
- Windows/ChatWindow.axaml.cs (інтеграція ReasoningService)
- AI/PromptBuilder.cs (додати режим reasoning)

### Етап 3: Global Memory (2-3 дні)

**Завдання:**
1. ✅ Створити MemoryService
2. ✅ Створити MemoryExtractor
3. ✅ Створити MemoryStorage
4. ✅ Створити MemoryQueryEngine
5. ✅ Інтегрувати в ChatWindow

**Файли для створення:**
- Services/Memory/MemoryService.cs
- Services/Memory/MemoryExtractor.cs
- Services/Memory/MemoryStorage.cs
- Services/Memory/MemoryQueryEngine.cs

**Файли для оновлення:**
- Windows/ChatWindow.axaml.cs (витягування та використання фактів)
- AI/PromptBuilder.cs (додати контекст з пам'яті)

### Етап 4: UI Integration (1-2 дні)

**Завдання:**
1. ✅ Оновити InstructionEditorWindow.axaml (додати toggles)
2. ✅ Оновити InstructionEditorWindow.axaml.cs (обробка toggles)
3. ✅ Оновити ChatWindow.axaml (відображення reasoning steps)
4. ✅ Додати локалізацію (5 мов)

**Файли для оновлення:**
- Windows/InstructionEditorWindow.axaml
- Windows/InstructionEditorWindow.axaml.cs
- Windows/ChatWindow.axaml
- UI/Localization/Strings.*.axaml (всі 5 мов)

### Етап 5: Тестування та оптимізація (2 дні)

**Завдання:**
1. ✅ Unit-тести для ReasoningService
2. ✅ Unit-тести для MemoryService
3. ✅ Інтеграційні тести
4. ✅ Тестування UI
5. ✅ Оптимізація продуктивності

---

## 🧪 Тестування

### Test Case 1: Reasoning Mode - Математична задача

```
Preconditions:
- Reasoning Mode = ✓ Enabled
- Global Memory = ✗ Disabled

Steps:
1. User: "Розв'яжи рівняння: 3x + 7 = 22"
2. Перевірити що AI генерує кроки:
   - Step 1: Identify equation
   - Step 2: Isolate variable
   - Step 3: Solve
   - Step 4: Verify
3. Перевірити що кожен крок відображається в UI
4. Перевірити що є фінальна відповідь

Expected Result:
✅ Reasoning chain збережено в БД
✅ Всі кроки відображені
✅ Фінальна відповідь: x = 5
```

### Test Case 2: Global Memory - Запам'ятовування фактів

```
Preconditions:
- Reasoning Mode = ✗ Disabled
- Global Memory = ✓ Enabled

Steps:
1. User: "Я програміст на C# і працюю над Insait Editor"
2. AI: "Чудово! Я запам'ятаю..."
3. Перевірити БД:
   SELECT * FROM memory_facts WHERE isActive = true
4. Закрити чат
5. Відкрити новий чат
6. User: "Яку мову програмування я використовую?"
7. AI повинен згадати: "C#"

Expected Result:
✅ Факти збережені в memory_facts
✅ AI використовує контекст з пам'яті
✅ Відповідь містить "C#"
```

### Test Case 3: Reasoning + Memory (комбінований режим)

```
Preconditions:
- Reasoning Mode = ✓ Enabled
- Global Memory = ✓ Enabled

Steps:
1. Day 1:
   User: "Мені потрібно оптимізувати алгоритм сортування для 10 млн елементів"
   
2. AI з reasoning:
   - Step 1: Identify problem (large dataset sorting)
   - Step 2: Consider algorithms (Quick, Merge, Heap)
   - Step 3: Recommend (Quick Sort with optimizations)
   
3. Memory зберігає:
   - "User works with large datasets"
   - "User interested in algorithm optimization"

4. Day 2:
   User: "Поясни складність алгоритму"
   
5. AI автоматично знає контекст:
   "Для Quick Sort, який ми обговорювали для ваших 10 млн елементів..."

Expected Result:
✅ Reasoning steps працюють
✅ Факти збережені
✅ Наступна розмова використовує контекст
```

---

## 📊 Метрики успіху

| Метрика | Ціль | Вимірювання |
|---------|------|-------------|
| **Reasoning Accuracy** | >85% | Коректність розв'язань математичних задач |
| **Memory Recall** | >90% | AI згадує факти через 24 години |
| **Response Time** | <5 сек | З reasoning mode |
| **Memory Storage** | <10 MB | Для 1000 фактів |
| **UI Responsiveness** | <200ms | Відображення reasoning steps |
| **Database Performance** | <50ms | Запит до memory_facts |

---

## 🔒 Конфіденційність та безпека

### Принципи

1. **Local-First**: Вся пам'ять зберігається локально у LiteDB
2. **User Control**: Користувач може видаляти факти
3. **Transparency**: Показувати які факти зберігаються
4. **Expiration**: Старі факти (>90 днів без використання) позначаються як неактивні

### Додаткові фічі (опціонально)

```csharp
// В MemoryService
public Task ClearAllMemoryAsync()
{
    // Видалити всі факти (GDPR compliance)
}

public Task ExportMemoryAsync(string filePath)
{
    // Експорт пам'яті в JSON
}

public Task ImportMemoryAsync(string filePath)
{
    // Імпорт пам'яті з JSON
}
```

---

## 🎯 Підсумок

### Що буде реалізовано:

✅ **Reasoning Mode**
- Chain-of-Thought міркування
- Покрокове відображення
- Збереження в LiteDB

✅ **Global Memory**
- Автоматичне витягування фактів
- Семантичний пошук
- Персоналізовані відповіді

✅ **UI Integration**
- Toggles в InstructionEditorWindow
- Відображення reasoning steps
- Memory stats

✅ **Database**
- 3 нові колекції в LiteDB
- Індекси для швидкого пошуку
- Оптимізована структура

### Загальна оцінка:

- **Файлів нових**: 14
- **Файлів для оновлення**: 10
- **Час розробки**: 8-12 днів
- **Складність**: ⭐⭐⭐⭐ (4/5)

---

**Створено**: GitHub Copilot  
**Проект**: InsaitTextEditor  
**Версія плану**: 1.0  
**Дата**: 19 жовтня 2025

🚀 **Ready to implement!**

