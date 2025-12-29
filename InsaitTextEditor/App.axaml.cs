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
    
    // ✅ Аргументи командного рядка для відкриття файлів
    public static string[]? StartupArgs { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Store certification/sandbox environments can be stricter about file system, DPAPI, and native DLL loading.
        // Startup must never hard-crash; we'll fall back to a degraded mode if initialization fails.
        try
        {
            InitializeDatabases();
        }
        catch (Exception ex)
        {
            // Keep the app alive; editor should still open.
            System.Console.WriteLine($"[App] ⚠️ Database init failed, continuing without persistence: {ex}");
        }

        try
        {
            InitializeAiServices();
        }
        catch (Exception ex)
        {
            // AI features are optional for basic editor mode.
            System.Console.WriteLine($"[App] ⚠️ AI init failed, continuing without AI: {ex}");
        }
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
        // Best-effort diagnostics (never throws): shows where settings DB is and what language got resolved.
        try { SettingsService.LogStartupLanguageDiagnostics(); } catch { /* ignore */ }

        var savedLang = SettingsService.LoadLanguage();
        LocalizationService.Initialize(savedLang);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // ✅ Перевіряємо чи є файл для відкриття з аргументів командного рядка
            string? startupFilePath = null;
            if (StartupArgs != null && StartupArgs.Length > 0)
            {
                var filePath = StartupArgs[0];
                if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
                {
                    startupFilePath = filePath;
                    System.Console.WriteLine($"[App] ✅ Знайдено файл для відкриття: {startupFilePath}");
                }
            }
            
            var mainWindow = new MainWindow(suppressInit: false, startupFilePath: startupFilePath);
            desktop.MainWindow = mainWindow;

            // ✅ Отримати TabManager з MainWindow
            TabManager = mainWindow.TabManager;
            System.Console.WriteLine("[App] ✅ TabManager експортовано з MainWindow");

            // ✅ GDPR/User agreement (offline + local storage) on first run
            try
            {
                if (!SettingsService.IsUserAgreementAccepted())
                {
                    mainWindow.Opened += (_, _) =>
                    {
                        // double-check in case something set it earlier
                        if (SettingsService.IsUserAgreementAccepted())
                            return;

                        try
                        {
                            mainWindow.IsEnabled = false;

                            var dlg = new Windows.UserAgreementWindow
                            {
                                WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
                            };

                            dlg.Closed += (_, _) =>
                            {
                                // If user accepted, persist; otherwise exit.
                                if (SettingsService.IsUserAgreementAccepted(Windows.UserAgreementWindow.CurrentAgreementVersion))
                                {
                                    mainWindow.IsEnabled = true;
                                    return;
                                }

                                try
                                {
                                    mainWindow.Close();
                                    desktop.Shutdown();
                                }
                                catch
                                {
                                    // ignore
                                }
                            };

                            // Ensure owner is set for correct z-order and input behavior.
                            dlg.Show(mainWindow);
                        }
                        catch
                        {
                            // Never leave the app in a disabled state.
                            mainWindow.IsEnabled = true;
                        }
                    };
                }
            }
            catch
            {
                // If anything goes wrong, don't block app startup.
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}