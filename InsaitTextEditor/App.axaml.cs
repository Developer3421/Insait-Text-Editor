using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using InsaitTextEditor.AI;
using InsaitTextEditor.Agents;
using InsaitTextEditor.Services;
using InsaitTextEditor.Services.Database.Core;
using InsaitTextEditor.Services.Database.Specialized;
using InsaitTextEditor.Services.Reasoning;
using InsaitTextEditor.Services.Memory;

namespace InsaitTextEditor;

public partial class App : Application
{
    // Нові зашифровані бази даних
    public static DatabaseConfig DatabaseConfig { get; private set; } = new DatabaseConfig();
    public static DatabaseEncryptionManager EncryptionManager { get; private set; } = null!;
    public static ChatHistoryDatabaseService ChatHistoryDb { get; private set; } = null!;
    public static MemoryDatabaseService MemoryDb { get; private set; } = null!;
    public static ReasoningDatabaseService ReasoningDb { get; private set; } = null!;
    public static DocumentsDatabaseService DocumentsDb { get; private set; } = null!;
    public static SettingsDatabaseService SettingsDb { get; private set; } = null!;
    
    // Старий DatabaseService (буде видалено пізніше)
    public static DatabaseService DatabaseService { get; private set; } = new DatabaseService();
    
    // AI компоненти
    public static UserInstructionService UserInstructionService { get; private set; } = null!;
    public static GemmaConfig GemmaConfig { get; private set; } = null!;
    public static PromptBuilder PromptBuilder { get; private set; } = null!;
    public static GemmaModelManager GemmaModelManager { get; private set; } = null!;
    public static ConversationStateService ConversationStateService { get; private set; } = null!;
    
    // Microsoft Agent Framework - ТІЛЬКИ ЦЕ!
    public static MicrosoftAgentsAdapter MicrosoftAgentsAdapter { get; private set; } = null!;
    public static LlamaSharpInferenceEngine InferenceEngine { get; private set; } = null!;
    public static AgentConfig AgentConfig { get; private set; } = null!;
    public static MicrosoftInsaitAgent MicrosoftInsaitAgent { get; private set; } = null!;
    public static AgentService AgentService { get; private set; } = null!;
    
    // ✅ Reasoning та Memory сервіси
    public static ReasoningService ReasoningService { get; private set; } = null!;
    public static MemoryService MemoryService { get; private set; } = null!;
    
    // ✅ TabManager для доступу з ChatWindow та інших компонентів
    public static TabManager? TabManager { get; private set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        InitializeDatabases();
        InitializeAiServices();
    }

    private void InitializeDatabases()
    {
        try
        {
            System.Console.WriteLine("[App] Initializing database folders...");
            
            // Створення всіх необхідних папок для БД
            var folderInitializer = new DatabaseFolderInitializer();
            if (!folderInitializer.EnsureAllDatabaseFoldersExist())
            {
                System.Console.WriteLine("[App] ⚠️ Warning: Some database folders could not be created");
                // Продовжуємо виконання - можливо папки вже існують
            }
            
            System.Console.WriteLine("[App] Initializing encrypted databases...");
            
            // Ініціалізація шифрування
            EncryptionManager = new DatabaseEncryptionManager(DatabaseConfig);
            System.Console.WriteLine("[App] Encryption manager created");
            
            // Створення зашифрованих баз даних (БЕЗ ініціалізації тут!)
            ChatHistoryDb = new ChatHistoryDatabaseService(DatabaseConfig, EncryptionManager);
            System.Console.WriteLine("[App] ChatHistory service created");
            
            MemoryDb = new MemoryDatabaseService(DatabaseConfig, EncryptionManager);
            System.Console.WriteLine("[App] Memory service created");
            
            ReasoningDb = new ReasoningDatabaseService(DatabaseConfig, EncryptionManager);
            System.Console.WriteLine("[App] Reasoning service created");
            
            DocumentsDb = new DocumentsDatabaseService(DatabaseConfig, EncryptionManager);
            System.Console.WriteLine("[App] Documents service created");
            
            SettingsDb = new SettingsDatabaseService(DatabaseConfig, EncryptionManager);
            System.Console.WriteLine("[App] Settings service created");
            
            System.Console.WriteLine("[App] All database services created successfully!");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[App] CRITICAL ERROR creating databases: {ex.Message}");
            System.Console.WriteLine($"[App] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private void InitializeAiServices()
    {
        try
        {
            System.Console.WriteLine("[App] Initializing Microsoft Agent Framework...");
            
            // Базові сервіси
            UserInstructionService = new UserInstructionService(DatabaseService);
            GemmaConfig = new GemmaConfig(UserInstructionService);
            PromptBuilder = new PromptBuilder(GemmaConfig, UserInstructionService);
            GemmaModelManager = new GemmaModelManager(GemmaConfig, PromptBuilder);
            ConversationStateService = new ConversationStateService();
            
            // Microsoft Agent Framework компоненти
            InferenceEngine = new LlamaSharpInferenceEngine(GemmaConfig);
            MicrosoftAgentsAdapter = new MicrosoftAgentsAdapter(InferenceEngine, PromptBuilder);
            AgentConfig = new AgentConfig();
            
            // ТІЛЬКИ Microsoft.Agents агент (без legacy!)
            MicrosoftInsaitAgent = new MicrosoftInsaitAgent(
                MicrosoftAgentsAdapter,
                AgentConfig,
                saveToFileTool: null
            );
            
            // AgentService тільки з Microsoft.Agents з новим ChatHistoryService
            AgentService = new AgentService(
                MicrosoftInsaitAgent,
                GemmaModelManager, 
                ConversationStateService, 
                new ChatHistoryService(ChatHistoryDb)
            );
            
            // ✅ Ініціалізація Reasoning та Memory сервісів
            ReasoningService = new ReasoningService(
                MicrosoftInsaitAgent,
                ReasoningDb,
                UserInstructionService  // ✅ Додаємо UserInstructionService для мовних інструкцій
            );
            System.Console.WriteLine("[App] ✅ ReasoningService initialized!");
            
            MemoryService = new MemoryService(
                MemoryDb,
                InferenceEngine
            );
            System.Console.WriteLine("[App] ✅ MemoryService initialized!");
            
            System.Console.WriteLine("[App] Microsoft Agent Framework ready!");
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[App] ERROR initializing AI services: {ex.Message}");
            System.Console.WriteLine($"[App] Stack trace: {ex.StackTrace}");
            throw;
        }
    }

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
                TabManager = mainWindow.TabManager;
                System.Console.WriteLine("[App] ✅ TabManager експортовано з MainWindow");
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}