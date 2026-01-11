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
    // New encrypted databases
    public static DatabaseConfig DatabaseConfig { get; private set; } = new DatabaseConfig();
    public static DatabaseEncryptionManager EncryptionManager { get; private set; } = null!;
    public static ChatHistoryDatabaseService ChatHistoryDb { get; private set; } = null!;
    public static MemoryDatabaseService MemoryDb { get; private set; } = null!;
    public static ReasoningDatabaseService ReasoningDb { get; private set; } = null!;
    public static DocumentsDatabaseService DocumentsDb { get; private set; } = null!;
    public static SettingsDatabaseService SettingsDb { get; private set; } = null!;
    
    // Old DatabaseService (will be removed later)
    public static DatabaseService DatabaseService { get; private set; } = new DatabaseService();
    
    // AI components
    public static UserInstructionService UserInstructionService { get; private set; } = null!;
    public static GemmaConfig GemmaConfig { get; private set; } = null!;
    public static PromptBuilder PromptBuilder { get; private set; } = null!;
    public static GemmaModelManager GemmaModelManager { get; private set; } = null!;
    public static ConversationStateService ConversationStateService { get; private set; } = null!;
    
    // Microsoft Agent Framework - ONLY THIS!
    public static MicrosoftAgentsAdapter MicrosoftAgentsAdapter { get; private set; } = null!;
    public static LlamaSharpInferenceEngine InferenceEngine { get; private set; } = null!;
    public static AgentConfig AgentConfig { get; private set; } = null!;
    public static MicrosoftInsaitAgent MicrosoftInsaitAgent { get; private set; } = null!;
    public static AgentService AgentService { get; private set; } = null!;
    
    // ✅ Reasoning and Memory services
    public static ReasoningService ReasoningService { get; private set; } = null!;
    public static MemoryService MemoryService { get; private set; } = null!;
    
    // ✅ TabManager for access from ChatWindow and other components
    public static TabManager? TabManager { get; private set; }
    
    // ✅ Command line arguments for opening files
    public static string[]? StartupArgs { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        try
        {
            InitializeDatabases();
        }
        catch
        {
            // Keep the app alive; editor should still open.
        }

        try
        {
            InitializeAiServices();
        }
        catch
        {
            // AI features are optional for basic editor mode.
        }
    }

    private void InitializeDatabases()
    {
        var folderInitializer = new DatabaseFolderInitializer();
        folderInitializer.EnsureAllDatabaseFoldersExist();
        
        EncryptionManager = new DatabaseEncryptionManager(DatabaseConfig);
        
        ChatHistoryDb = new ChatHistoryDatabaseService(DatabaseConfig, EncryptionManager);
        MemoryDb = new MemoryDatabaseService(DatabaseConfig, EncryptionManager);
        ReasoningDb = new ReasoningDatabaseService(DatabaseConfig, EncryptionManager);
        DocumentsDb = new DocumentsDatabaseService(DatabaseConfig, EncryptionManager);
        SettingsDb = new SettingsDatabaseService(DatabaseConfig, EncryptionManager);
    }

    private void InitializeAiServices()
    {
        UserInstructionService = new UserInstructionService(DatabaseService);
        GemmaConfig = new GemmaConfig(UserInstructionService);
        PromptBuilder = new PromptBuilder(GemmaConfig, UserInstructionService);
        GemmaModelManager = new GemmaModelManager(GemmaConfig, PromptBuilder);
        ConversationStateService = new ConversationStateService();
        
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
        
        ReasoningService = new ReasoningService(
            MicrosoftInsaitAgent,
            ReasoningDb,
            UserInstructionService
        );
        
        MemoryService = new MemoryService(
            MemoryDb,
            InferenceEngine
        );
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try { SettingsService.Initialize(); } catch { /* ignore */ }

        var savedLang = SettingsService.LoadLanguage();
        LocalizationService.Initialize(savedLang);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            string? startupFilePath = null;
            if (StartupArgs != null && StartupArgs.Length > 0)
            {
                var filePath = StartupArgs[0];
                if (!string.IsNullOrWhiteSpace(filePath) && System.IO.File.Exists(filePath))
                {
                    startupFilePath = filePath;
                }
            }
            
            var mainWindow = new MainWindow(suppressInit: false, startupFilePath: startupFilePath);
            desktop.MainWindow = mainWindow;

            TabManager = mainWindow.TabManager;

            // User agreement on first run
            try
            {
                if (!SettingsService.IsUserAgreementAccepted())
                {
                    mainWindow.Opened += (_, _) =>
                    {
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
                                if (dlg.IsAccepted || SettingsService.IsUserAgreementAccepted(Windows.UserAgreementWindow.CurrentAgreementVersion))
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

                            dlg.Show(mainWindow);
                        }
                        catch
                        {
                            mainWindow.IsEnabled = true;
                        }
                    };
                }
            }
            catch
            {
                // ignore
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}