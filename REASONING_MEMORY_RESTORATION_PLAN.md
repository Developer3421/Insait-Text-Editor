# План відновлення функціональності Reasoning і Global Memory

**Дата створення:** 20 жовтня 2025  
**Статус:** 🔴 Критична проблема - генерація не запускається при увімкненні режимів

---

## 🔴 Проблема

### Симптоми
- При увімкненні режиму Reasoning або Global Memory генерація взагалі не запускається
- Система "зависає" без видимих помилок
- Чат не реагує на повідомлення користувача

### Коренева причина

**КРИТИЧНО:** `ReasoningService` та `MemoryService` НЕ ініціалізовані в додатку!

#### Технічні деталі:

1. **У `ChatWindow.axaml.cs`** (рядки 30-31):
   ```csharp
   private readonly ReasoningService? _reasoningService;
   private readonly MemoryService? _memoryService;
   ```
   Ці поля оголошені як `nullable` і **ніколи не отримують значення**.

2. **У конструкторі `ChatWindow`** (рядок 45+):
   ```csharp
   public ChatWindow()
   {
       InitializeComponent();
       _messages = new ObservableCollection<ChatMessage>();
       _agentService = App.AgentService;
       _historyService = new ChatHistoryService(App.ChatHistoryDb);
       _instructionService = new UserInstructionService(App.DatabaseService);
       
       // ❌ _reasoningService НЕ ініціалізований!
       // ❌ _memoryService НЕ ініціалізований!
   }
   ```

3. **У `App.axaml.cs`** не існує глобальних екземплярів цих сервісів:
   - Є `ChatHistoryDb`, `MemoryDb`, `ReasoningDb` (бази даних)
   - Є `AgentService`, `MicrosoftInsaitAgent` (AI компоненти)
   - Але **НЕМАЄ** `ReasoningService` та `MemoryService`

4. **Що відбувається при генерації** (рядок 351+ у `ChatWindow.axaml.cs`):
   ```csharp
   if (reasoningEnabled && _reasoningService != null)  // ❌ ЗАВЖДИ false!
   {
       // Цей код НІКОЛИ не виконується
   }
   
   if (memoryEnabled && _memoryService != null)  // ❌ ЗАВЖДИ false!
   {
       // Цей код НІКОЛИ не виконується
   }
   ```

5. **Результат:**
   - Код перевіряє `_reasoningService != null` → `false`
   - Код перевіряє `_memoryService != null` → `false`
   - Умови не спрацьовують, але режими увімкнені
   - Система потрапляє в невизначений стан і не генерує відповідь

---

## ✅ Рішення

### Етап 1: Додати глобальні сервіси в App.axaml.cs

**Файл:** `InsaitTextEditor/App.axaml.cs`

**Дії:**

1. Додати статичні властивості:
   ```csharp
   public static ReasoningService ReasoningService { get; private set; } = null!;
   public static MemoryService MemoryService { get; private set; } = null!;
   ```

2. Ініціалізувати в методі `InitializeAiServices()` **ПІСЛЯ** створення `InferenceEngine`:
   ```csharp
   private void InitializeAiServices()
   {
       try
       {
           // ...існуючий код...
           
           InferenceEngine = new LlamaSharpInferenceEngine(GemmaConfig);
           MicrosoftAgentsAdapter = new MicrosoftAgentsAdapter(InferenceEngine, PromptBuilder);
           AgentConfig = new AgentConfig();
           
           MicrosoftInsaitAgent = new MicrosoftInsaitAgent(
               MicrosoftAgentsAdapter,
               AgentConfig,
               saveToFileTool: null
           );
           
           AgentService = new AgentService(
               MicrosoftInsaitAgent,
               GemmaModelManager, 
               ConversationStateService, 
               new ChatHistoryService(ChatHistoryDb)
           );
           
           // ✅ ДОДАТИ ЦЕ:
           ReasoningService = new ReasoningService(
               MicrosoftInsaitAgent,
               ReasoningDb
           );
           
           MemoryService = new MemoryService(
               MemoryDb,
               InferenceEngine
           );
           
           System.Console.WriteLine("[App] ✅ ReasoningService and MemoryService initialized!");
           System.Console.WriteLine("[App] Microsoft Agent Framework ready!");
       }
       catch (Exception ex)
       {
           System.Console.WriteLine($"[App] ERROR initializing AI services: {ex.Message}");
           throw;
       }
   }
   ```

**Залежності:**
- `ReasoningService` потребує: `MicrosoftInsaitAgent`, `ReasoningDb`
- `MemoryService` потребує: `MemoryDb`, `InferenceEngine`

---

### Етап 2: Ініціалізувати сервіси в ChatWindow

**Файл:** `InsaitTextEditor/Windows/ChatWindow.axaml.cs`

**Дії:**

1. Змінити конструктор:
   ```csharp
   public ChatWindow()
   {
       InitializeComponent();
       
       _messages = new ObservableCollection<ChatMessage>();
       _agentService = App.AgentService;
       _historyService = new ChatHistoryService(App.ChatHistoryDb);
       _instructionService = new UserInstructionService(App.DatabaseService);
       
       // ✅ ДОДАТИ ЦЕ:
       _reasoningService = App.ReasoningService;
       _memoryService = App.MemoryService;
       
       // ...решта коду...
   }
   ```

**Примітка:** Тепер перевірки `!= null` будуть працювати коректно.

---

### Етап 3: Ініціалізація баз даних

**ВАЖЛИВО:** Бази даних створюються в `InitializeDatabases()`, але не ініціалізуються асинхронно!

**Проблема в коді:**
```csharp
// У ReasoningService.cs (рядок 33):
// Removed blocking initialization to avoid UI-thread deadlock
// _reasoningDb.InitializeAsync().GetAwaiter().GetResult();
```

**Рішення:**

Бази даних мають ініціалізуватися **ДО** використання, але не в конструкторі.

**Варіанти:**

#### Варіант А (Рекомендований): Lazy ініціалізація в сервісах
1. Перший виклик методу сервісу перевіряє чи БД ініціалізована
2. Якщо н�� - викликає `InitializeAsync()`
3. Це безпечно для UI thread

#### Варіант Б: Ініціалізація при старті додатку
1. Додати асинхронну ініціалізацію в `App.OnFrameworkInitializationCompleted()`
2. Показати splash screen поки ініціалізується
3. Потенційно повільний старт

**Рекомендація:** Використати Варіант А для кожного сервісу.

---

### Етап 4: Додати безпечну ініціалізацію БД

**Файл:** `InsaitTextEditor/Services/Reasoning/ReasoningService.cs`

**Дії:**

```csharp
public class ReasoningService
{
    private readonly MicrosoftInsaitAgent _agent;
    private readonly ReasoningPromptBuilder _promptBuilder;
    private readonly ReasoningDatabaseService _reasoningDb;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
    
    public ReasoningService(
        MicrosoftInsaitAgent agent,
        ReasoningDatabaseService reasoningDb)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _promptBuilder = new ReasoningPromptBuilder();
        _reasoningDb = reasoningDb ?? throw new ArgumentNullException(nameof(reasoningDb));
    }
    
    // ✅ ДОДАТИ ЦЕЙ МЕТОД:
    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        
        await _initLock.WaitAsync();
        try
        {
            if (!_isInitialized)
            {
                await _reasoningDb.InitializeAsync();
                _isInitialized = true;
                Console.WriteLine("[ReasoningService] ✅ Database initialized");
            }
        }
        finally
        {
            _initLock.Release();
        }
    }
    
    public async IAsyncEnumerable<ReasoningStreamEvent> GenerateReasoningChainStreamAsync(
        string userQuery, 
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ✅ ДОДАТИ ЦЕ НА ПОЧАТКУ:
        await EnsureInitializedAsync();
        
        var chain = new ReasoningChain
        {
            UserQuery = userQuery,
            ConversationId = conversationId,
            Status = ChainStatus.InProgress
        };
        
        // ...решта коду...
    }
}
```

**Файл:** `InsaitTextEditor/Services/Memory/MemoryService.cs`

**Аналогічно додати:**
```csharp
private bool _isInitialized = false;
private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

private async Task EnsureInitializedAsync()
{
    if (_isInitialized) return;
    
    await _initLock.WaitAsync();
    try
    {
        if (!_isInitialized)
        {
            await _memoryDb.InitializeAsync();
            _isInitialized = true;
            Console.WriteLine("[MemoryService] ✅ Database initialized");
        }
    }
    finally
    {
        _initLock.Release();
    }
}

public async Task<List<MemoryFact>> QueryMemoryAsync(string query, int maxResults = 5)
{
    await EnsureInitializedAsync();
    // ...решта коду...
}

public async Task ProcessMessageAsync(ChatMessage message)
{
    await EnsureInitializedAsync();
    // ...решта коду...
}
```

---

### Етап 5: Тестування

**Порядок тестування:**

1. **Базовий тест:**
   - Запустити додаток
   - Перевірити логи: `[App] ✅ ReasoningService and MemoryService initialized!`
   - Відкрити ChatWindow
   - Надіслати просте повідомлення БЕЗ reasoning/memory
   - ✅ Має працювати як раніше

2. **Тест Reasoning:**
   - Увімкнути Reasoning Mode в інструкціях
   - Надіслати запит: "Поясни як працює фотосинтез"
   - ✅ Має з'явитися: `🧠 Початок reasoning chain...`
   - ✅ Мають з'явитися кроки: `🧠 Крок 1: ...`
   - ✅ Фінальна відповідь має з'явитися

3. **Тест Global Memory:**
   - Увімкнути Global Memory в інструкціях
   - Надіслати факт: "Мене звати Олексій"
   - Надіслати запит: "Як мене звати?"
   - ✅ Має знайти факт з пам'яті: `💾 Знайдено N релевантних фактів`
   - ✅ Асистент має відповісти правильно

4. **Тест обох режимів разом:**
   - Увімкнути ОБИДВА режими
   - Надіслати складний запит
   - ✅ Має працювати reasoning + використовувати memory

5. **Тест помилок:**
   - Перевірити логи на помилки
   - Перевірити що БД ініціалізуються лише один раз
   - Перевірити що немає deadlock'ів

---

## 📋 Чеклист виконання

- [ ] **Етап 1:** Додати `ReasoningService` та `MemoryService` в `App.axaml.cs`
- [ ] **Етап 2:** Ініціалізувати сервіси в конструкторі `ChatWindow`
- [ ] **Етап 3:** Додати `EnsureInitializedAsync()` в `ReasoningService`
- [ ] **Етап 4:** Додати `EnsureInitializedAsync()` в `MemoryService`
- [ ] **Етап 5:** Зібрати проєкт без помилок
- [ ] **Тест 1:** Базова генерація без режимів
- [ ] **Тест 2:** Reasoning mode окремо
- [ ] **Тест 3:** Global Memory окремо
- [ ] **Тест 4:** Обидва режими разом
- [ ] **Тест 5:** Перевірка логів та помилок

---

## 🔍 Додаткові покращення (опціонально)

### 1. Логування стану сервісів
Додати діагностику в `ChatWindow`:
```csharp
LogUI($"🔧 Debug: ReasoningService = {(_reasoningService != null ? "✅" : "❌")}");
LogUI($"🔧 Debug: MemoryService = {(_memoryService != null ? "✅" : "❌")}");
```

### 2. Graceful fallback
Якщо сервіс не ініціалізований, показати попередження замість "зависання":
```csharp
if (reasoningEnabled && _reasoningService == null)
{
    AddSystemMessage("⚠️ Reasoning Service недоступний. Використовується звичайний режим.");
    reasoningEnabled = false;
}
```

### 3. UI індикатор стану
Показати користувачу що режими активні:
- Іконка 🧠 коли Reasoning активний
- Іконка 💾 коли Memory активна

### 4. Конфігурація через налаштування
Дозволити користувачу увімкнути/вимкнути режими навіть якщо сервіси недоступні.

---

## 📊 Очікувані результати

### До виправлення:
- ❌ Генерація не запускається з reasoning/memory
- ❌ Система "висить" без помилок
- ❌ Неможливо отримати відповідь

### Після виправлення:
- ✅ Reasoning режим працює з покроковою генерацією
- ✅ Global Memory знаходить та використовує факти
- ✅ Обидва режими працюють разом
- ✅ Чітке логування процесу
- ✅ Коректна обробка помилок

---

## 🚨 Потенційні проблеми

### 1. Async ініціалізація в конструкторі
**Проблема:** Не можна робити `await` в конструкторі  
**Рішення:** Використовуємо lazy initialization при першому виклику

### 2. Thread safety
**Проблема:** Кілька одночасних запитів можуть викликати race condition  
**Рішення:** Використовуємо `SemaphoreSlim` для синхронізації

### 3. UI thread blocking
**Проблема:** Ініціалізація БД може заблокувати UI  
**Рішення:** Всі операції асинхронні, не блокують UI thread

### 4. Помилки ініціалізації БД
**Проблема:** Якщо БД не може ініціалізуватися  
**Рішення:** Try-catch + логування + fallback до звичайного режиму

---

## 📝 Примітки

1. **Порядок ініціалізації критичний:**
   - Спочатку `InferenceEngine`
   - Потім `MicrosoftInsaitAgent`
   - Потім `ReasoningService` та `MemoryService`

2. **Залежності:**
   - `ReasoningService` → `MicrosoftInsaitAgent`, `ReasoningDb`
   - `MemoryService` → `MemoryDb`, `InferenceEngine`

3. **Тестування на .NET 10:**
   - Проєкт використовує `net10.0`
   - Переконатися що всі async/await працюють коректно

---

**Автор плану:** GitHub Copilot  
**Дата:** 20 жовтня 2025  
**Версія:** 1.0

