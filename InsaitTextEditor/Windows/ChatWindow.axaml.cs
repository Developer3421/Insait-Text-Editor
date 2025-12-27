using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using InsaitTextEditor.Agents.Tools;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;
using InsaitTextEditor.Services.Reasoning;
using InsaitTextEditor.Services.Memory;
using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using System.IO;
using InsaitTextEditor.Scripts.WindowsControl;

namespace InsaitTextEditor.Windows;

public partial class ChatWindow : Window
{
    private readonly ObservableCollection<ChatMessage> _messages;
    private readonly AgentService _agentService;
    private readonly ChatHistoryService _historyService;
    private readonly UserInstructionService _instructionService;
    private readonly ReasoningService? _reasoningService;
    private readonly MemoryService? _memoryService;
    private CancellationTokenSource? _cts;
    private ChatMessage? _lastUserMessage;
    private bool _isUserScrolling = false;
    private double _lastScrollPosition = 0;

    // New: auto-scroll only when user stays near the bottom.
    private bool _autoScrollEnabled = true;
    private const double AutoScrollBottomThreshold = 48; // px

    // Styled property so XAML can bind to the window's current model name and update automatically
    public static readonly StyledProperty<string> CurrentModelDisplayNameProperty =
        AvaloniaProperty.Register<ChatWindow, string>(nameof(CurrentModelDisplayName));

    public string CurrentModelDisplayName
    {
        get => GetValue(CurrentModelDisplayNameProperty);
        set => SetValue(CurrentModelDisplayNameProperty, value);
    }

    public ChatWindow()
    {
        InitializeComponent();
        
        _messages = new ObservableCollection<ChatMessage>();
        _agentService = App.AgentService;
        _historyService = new ChatHistoryService(App.ChatHistoryDb);
        _instructionService = new UserInstructionService(App.DatabaseService);
        
        // ✅ Ініціалізація Reasoning та Memory сервісів з глобальних екземплярів
        _reasoningService = App.ReasoningService;
        _memoryService = App.MemoryService;
        
        // Підписка на зміни колекції для автоскролу та підписки на PropertyChanged для оновлень контенту
        _messages.CollectionChanged += Messages_CollectionChanged;

        var messagesList = this.FindControl<ItemsControl>("MessagesList");
        if (messagesList != null)
        {
            messagesList.ItemsSource = _messages;
            
            // Use the template selector
            var templateSelector = this.FindResource("MessageTemplateSelector") as Selectors.ChatMessageTemplateSelector;
            if (templateSelector != null)
            {
                messagesList.ItemTemplate = templateSelector;
            }
        }

        // Завантажити назву моделі асинхронно (не блокуємо UI)
        _ = LoadModelInfoAsync();

        this.Opened += ChatWindow_Opened;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        // Встановити локалізовані підписи для кнопок Send/Stop при створенні вікна
        var sendBtn = this.FindControl<Button>("SendButton");
        var stopBtn = this.FindControl<Button>("StopButton");
        if (sendBtn != null)
            sendBtn.Content = LocalizationService.GetString("Key.Send", "Send");
        if (stopBtn != null)
            stopBtn.Content = LocalizationService.GetString("Key.Stop", "Stop");

        // Встановити локалізований Watermark для поля вводу
        var inputBox = this.FindControl<TextBox>("InputTextBox");
        if (inputBox != null)
            inputBox.Watermark = LocalizationService.GetString("Key.TypeMessage", "Type your message here...");
        
        // Track scroll to avoid fighting the user, and to keep streaming updates visible.
        var scroll = this.FindControl<ScrollViewer>("MessagesScroll");
        if (scroll != null)
        {
            scroll.ScrollChanged += MessagesScroll_ScrollChanged;
        }
    }

    private async void ChatWindow_Opened(object? sender, EventArgs e)
    {
        // ✅ Ініціалізувати SaveToFileTool з контекстом цього вікна
        if (App.TabManager != null)
        {
            var saveToFileTool = new SaveToFileTool(
                App.TabManager,
                ownerWindow: this
            );
            
            // Встановити інструмент в агента
            App.MicrosoftInsaitAgent.SetSaveToFileTool(saveToFileTool);
            
            Console.WriteLine("[ChatWindow] ✅ SaveToFileTool підключено до агента");
        }
        else
        {
            Console.WriteLine("[ChatWindow] ⚠️ TabManager не доступний, SaveToFileTool не підключено");
        }

        // Завантажити історію
        try
        {
            var recent = _historyService.GetRecent(100);
            var orderedMessages = recent.OrderBy(m => m.Timestamp).ToList();
            
            foreach (var msg in orderedMessages)
            {
                _messages.Add(msg);
            }
            
            // ✅ Додати welcome message якщо історія порожня
            if (_messages.Count == 0)
            {
                var welcomeMsg = new ChatMessage
                {
                    Sender = AssistantConfig.Name,
                    Content = LocalizationService.GetString("Key.WelcomeMessage", 
                        "Welcome! I'm Insait Creative Assistant 🤖\n\n" +
                        "I can help you with:\n" +
                        "• Answering questions\n" +
                        "• Editing text\n" +
                        "• Generating content\n" +
                        "• **Creating poems** (automatically saved!)\n\n" +
                        "Just type your question! (Ctrl+Enter to send)"),
                    Timestamp = DateTime.UtcNow
                };
                _messages.Add(welcomeMsg);
                _historyService.AddMessage(welcomeMsg);
            }
            
            await Task.Delay(100);
            ScrollToBottom();
        }
        catch (Exception ex)
        {
            AddSystemMessage($"❌ Помилка завантаже��ня історії: {ex.Message}");
        }
        
        // Фонова ініціалізація моделі через Microsoft.Agents
        _ = Task.Run(async () =>
        {
            try
            {
                Console.WriteLine("[ChatWindow] 🔄 Ініціалізація Microsoft Agent Framework...");
                await _agentService.InitializeAsync();
                Console.WriteLine("[ChatWindow] ✅ Microsoft Agent Framework готовий");
            }
            catch (Exception ex)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    AddSystemMessage($"❌ Помилка ініціалізації моделі: {ex.Message}");
                });
            }
        });
    }

    // ==== Window Controls === =
    
    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void MaxRestore_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;

    private void Close_Click(object? sender, RoutedEventArgs e)
        => Close();

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed)
            return;

        var buttonsPanel = this.FindControl<StackPanel>("TitleButtonsPanel");
        if (buttonsPanel != null)
        {
            var pos = e.GetPosition(buttonsPanel);
            if (pos.X >= 0 && pos.X <= buttonsPanel.Bounds.Width &&
                pos.Y >= 0 && pos.Y <= buttonsPanel.Bounds.Height)
                return;
        }

        if (e.ClickCount == 2)
        {
            MaxRestore_Click(null, new RoutedEventArgs());
            return;
        }

        if (WindowState != WindowState.Maximized)
            BeginMoveDrag(e);
    }

    // ==== Resize Handlers === =
    
    private void Resize_Top_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.North, e);
    
    private void Resize_Bottom_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.South, e);
    
    private void Resize_Left_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.West, e);
    
    private void Resize_Right_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.East, e);
    
    private void Resize_TopLeft_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.NorthWest, e);
    
    private void Resize_TopRight_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.NorthEast, e);
    
    private void Resize_BottomLeft_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.SouthWest, e);
    
    private void Resize_BottomRight_PointerPressed(object? sender, PointerPressedEventArgs e)
        => BeginResizeDragIfNotMaximized(WindowEdge.SouthEast, e);
    
    private void BeginResizeDragIfNotMaximized(WindowEdge edge, PointerPressedEventArgs e)
    {
        if (WindowState != WindowState.Maximized)
            BeginResizeDrag(edge, e);
    }

    // ==== Chat Actions === =

    private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            Send_Click(null, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    /// <summary>
    /// Валідація вхідного тексту перед відправкою
    /// </summary>
    private bool ValidateInput(string input)
    {
        // Перевірка на null або порожній рядок
        if (string.IsNullOrEmpty(input))
            return false;
        
        // Перевірка на рядок лише з пробілами, табуляціями та переносами
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        // Перевірка на рядок лише з переносами рядків
        var cleanText = input.Replace("\r", "").Replace("\n", "").Trim();
        if (string.IsNullOrEmpty(cleanText))
            return false;
        
        return true;
    }

    private async void Send_Click(object? sender, RoutedEventArgs e)
    {
        var input = this.FindControl<TextBox>("InputTextBox");
        var sendBtn = this.FindControl<Button>("SendButton");
        var stopBtn = this.FindControl<Button>("StopButton");
        var typingIndicator = this.FindControl<ContentControl>("TypingIndicator");
        
        if (input == null || sendBtn == null) return;

        // Отримати текст БЕЗ trim для валідації
        var rawText = input.Text ?? string.Empty;
        
        // Професійна валідація
        if (!ValidateInput(rawText))
        {
            // Очистити поле вводу якщо користувач намагався відправити порожнє повідомлення
            input.Text = string.Empty;
            input.Focus();
            return;
        }

        // Тільки після валідації робимо trim для відправки
        var text = rawText.Trim();

        // Додаткова перевірка на довжину
        if (text.Length == 0)
        {
            input.Text = string.Empty;
            input.Focus();
            return;
        }

        LogUI($"📨 Відправлення повідомлення через Microsoft.Agents: {text.Length} символів");

        // Додати повідомленн�� користувача
        var userMsg = new ChatMessage
        {
            Sender = "User",
            Content = text,
            Timestamp = DateTime.UtcNow
        };
        _messages.Add(userMsg);
        _agentService.AddUserMessage(text);
        _lastUserMessage = userMsg;
        
        input.Text = string.Empty;
        ScrollToBottom();

        // Показати typing indicator
        if (typingIndicator != null)
            typingIndicator.IsVisible = true;

        // UI стан
        sendBtn.IsEnabled = false;
        input.IsEnabled = false;
        if (stopBtn != null)
            stopBtn.IsVisible = true;

        // Скасувати попередню генерацію якщо вона була
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            // Перевірити налаштування reasoning та memory
            var instruction = _instructionService.GetUserInstruction();
            var reasoningEnabled = instruction?.ReasoningEnabled ?? false;
            var memoryEnabled = instruction?.GlobalMemoryEnabled ?? false;

            LogUI($"🧠 Reasoning Mode: {reasoningEnabled}, 💾 Global Memory: {memoryEnabled}");

            // ======== GLOBAL MEMORY: Завантажити релевантні факти ========
            if (memoryEnabled && _memoryService != null)
            {
                try
                {
                    var relevantFacts = await _memoryService.QueryMemoryAsync(text, maxResults: 5);
                    if (relevantFacts.Any())
                    {
                        LogUI($"💾 Знайдено {relevantFacts.Count} релевантних фактів з пам'яті");
                        // TODO: В майбутньому можна передати memoryContext в промпт
                    }
                }
                catch (Exception ex)
                {
                    LogUI($"⚠️ Помилка пошуку в пам'яті: {ex.Message}");
                }
            }

            // ======== REASONING MODE: Покрокове виконання зі streaming ========
            if (reasoningEnabled && _reasoningService != null)
            {
                try
                {
                    LogUI("🧠 Початок reasoning chain...");
                    
                    var conversationId = Guid.NewGuid();
                    
                    // Словник для відстеження повідомлень кроків
                    var stepMessages = new Dictionary<int, ChatMessage>();
                    ChatMessage? statusMsg = null;
                    ChatMessage? finalAnswerMsg = null;
                    
                    await foreach (var reasoningEvent in _reasoningService.GenerateReasoningChainStreamAsync(
                        text, conversationId, _cts.Token))
                    {
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            switch (reasoningEvent.Type)
                            {
                                case ReasoningEventType.StatusUpdate:
                                    // Показати статус як системне повідомлення
                                    if (statusMsg != null)
                                    {
                                        _messages.Remove(statusMsg);
                                    }
                                    statusMsg = new ChatMessage
                                    {
                                        Sender = "System",
                                        Content = reasoningEvent.Message ?? "",
                                        Timestamp = DateTime.UtcNow
                                    };
                                    _messages.Add(statusMsg);
                                    break;
                                
                                case ReasoningEventType.StepStart:
                                    // Створити нове повідомлення для кроку з локалізацією
                                    var stepTitle = string.Format(
                                        LocalizationService.GetString("Key.ReasoningStep", "Крок {0}"),
                                        reasoningEvent.StepNumber
                                    );
                                    var stepMsg = new ChatMessage
                                    {
                                        Sender = "System",
                                        Content = $"🧠 **{stepTitle}**: {reasoningEvent.StepTitle}\n\n",
                                        Timestamp = DateTime.UtcNow
                                    };
                                    _messages.Add(stepMsg);
                                    stepMessages[reasoningEvent.StepNumber] = stepMsg;
                                    
                                    // Видалити попереднє статусне повідомлення
                                    if (statusMsg != null)
                                    {
                                        _messages.Remove(statusMsg);
                                        statusMsg = null;
                                    }
                                    break;
                                
                                case ReasoningEventType.StepContent:
                                    // Додати токен до контенту кроку
                                    if (stepMessages.TryGetValue(reasoningEvent.StepNumber, out var currentStepMsg))
                                    {
                                        currentStepMsg.Content += reasoningEvent.Content;
                                    }
                                    break;
                                
                                case ReasoningEventType.StepComplete:
                                    // Крок завершено - нічого не робимо, просто логуємо
                                    LogUI(reasoningEvent.Message ?? "");
                                    break;
                                
                                case ReasoningEventType.FinalAnswerContent:
                                    // Створити повідомлення фінальної відповіді якщо ще немає
                                    if (finalAnswerMsg == null)
                                    {
                                        // Видалити статусне повідомлення якщо є
                                        if (statusMsg != null)
                                        {
                                            _messages.Remove(statusMsg);
                                            statusMsg = null;
                                        }
                                        
                                        finalAnswerMsg = new ChatMessage
                                        {
                                            Sender = AssistantConfig.Name,
                                            Content = "",
                                            Timestamp = DateTime.UtcNow
                                        };
                                        _messages.Add(finalAnswerMsg);
                                        
                                        // Приховати typing indicator
                                        if (typingIndicator != null)
                                            typingIndicator.IsVisible = false;
                                    }
                                    
                                    // Додати токен до фінальної відповіді
                                    finalAnswerMsg.Content += reasoningEvent.Content;
                                    break;
                                
                                case ReasoningEventType.Complete:
                                    // Reasoning завершено з локалізацією
                                    LogUI(LocalizationService.GetString("Key.ReasoningComplete", "🎉 Reasoning завершено!"));
                                    
                                    // Зберегти фінальну відповідь в історію
                                    if (finalAnswerMsg != null && !string.IsNullOrWhiteSpace(finalAnswerMsg.Content))
                                    {
                                        _agentService.AddAssistantMessage(finalAnswerMsg.Content);
                                    }
                                    
                                    // Видалити статусне повідомлення якщо залишилось
                                    if (statusMsg != null)
                                    {
                                        _messages.Remove(statusMsg);
                                    }
                                    break;
                                
                                case ReasoningEventType.Error:
                                    // Помилка
                                    AddSystemMessage(reasoningEvent.Message ?? "Unknown error");
                                    break;
                            }
                        });
                        
                        // Оновлювати скрол після кожної події
                        await ScrollToBottom();
                    }
                }
                catch (Exception ex)
                {
                    LogUI($"❌ Помилка reasoning: {ex.Message}");
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        AddSystemMessage($"❌ Помилка reasoning: {ex.Message}");
                    });
                    // Fallback до звичайного режиму
                    reasoningEnabled = false;
                }
            }

            // ======== STANDARD MODE: Звичайна відповідь ========
            if (!reasoningEnabled)
            {
                // Створити placeholder для відповіді асистента
                var assistantMsg = new ChatMessage
                {
                    Sender = AssistantConfig.Name,
                    Content = string.Empty,
                    Timestamp = DateTime.UtcNow
                };
                
                // Приховати typing indicator перед додаванням по��ідомлення
                if (typingIndicator != null)
                    typingIndicator.IsVisible = false;
                
                _messages.Add(assistantMsg);
                await ScrollToBottom();

                // Streaming відповідь з throttling та фільтрацією
                var responseBuilder = new StringBuilder();
                var lastUpdateTime = DateTime.UtcNow;
                const int updateIntervalMs = 50;
                var hasAnyContent = false;

                LogUI($"🚀 [Microsoft.Agents] Початок отримання відповіді");

                await foreach (var token in _agentService.GetResponseStreamAsync(text, _cts.Token))
                {
                    // Пропускаємо пусті токени та токени тільки з пробілами
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    // 🛡️ Фільтр технічних токенів
                    if (ContainsTechnicalTokens(token))
                    {
                        LogUI($"🚫 Технічний токен в UI, СТОП відображення");
                        break;
                    }

                    responseBuilder.Append(token);
                    hasAnyContent = true;
                    
                    // Throttle UI updates (кожні 50ms)
                    if ((DateTime.UtcNow - lastUpdateTime).TotalMilliseconds >= updateIntervalMs)
                    {
                        var currentContent = responseBuilder.ToString();
                        
                        // Перевірка чи не закінчилась відповідь (зайві пробіли в кінці можуть означати кінець)
                        if (currentContent.EndsWith("  ") || currentContent.EndsWith("\n\n\n"))
                        {
                            LogUI($"🛑 Виявлено кінець відповіді (множинні пробіли/переноси)");
                            break;
                        }
                        
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            assistantMsg.Content = currentContent;
                        });
                        lastUpdateTime = DateTime.UtcNow;
                        await ScrollToBottom();
                    }
                }

                // Фінальне оновлення з очищенням зайвих пробілів
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var finalContent = responseBuilder.ToString().Trim();
                    
                    // Видалити множинні порожні рядки (залишити максимум 2 переноси підряд)
                    while (finalContent.Contains("\n\n\n"))
                    {
                        finalContent = finalContent.Replace("\n\n\n", "\n\n");
                    }
                    
                    assistantMsg.Content = finalContent;
                    
                    // Гарантувати скролінг після фінального оновлення контенту
                    var scrollViewer = this.FindControl<ScrollViewer>("MessagesScroll");
                    scrollViewer?.ScrollToEnd();
                });

                LogUI($"✅ Відповідь отримана: {responseBuilder.Length} символів");

                if (hasAnyContent && !string.IsNullOrWhiteSpace(assistantMsg.Content))
                {
                    _agentService.AddAssistantMessage(assistantMsg.Content);
                }
                else
                {
                    _messages.Remove(assistantMsg);
                    AddSystemMessage("No response generated. Please try again.");
                }
            }

            // ======== GLOBAL MEMORY: Зберегти нові факти ========
            if (memoryEnabled && _memoryService != null)
            {
                try
                {
                    // Зберегти факти з повідомлення користувача
                    await _memoryService.ProcessMessageAsync(userMsg);
                    
                    // Зберегти факти з відповіді асистента
                    var lastAssistantMsg = _messages.LastOrDefault(m => m.Sender == AssistantConfig.Name);
                    if (lastAssistantMsg != null)
                    {
                        await _memoryService.ProcessMessageAsync(lastAssistantMsg);
                    }
                    
                    LogUI("💾 Факти збережені в глобальну пам'ять");
                }
                catch (Exception ex)
                {
                    LogUI($"⚠️ Помилка збереження в пам'ять: {ex.Message}");
                }
            }
        }
        catch (OperationCanceledException)
        {
            LogUI($"⏹️ Зупинено користувачем");
            var lastMsg = _messages.LastOrDefault();
            if (lastMsg != null && lastMsg.Sender == AssistantConfig.Name)
            {
                if (!string.IsNullOrWhiteSpace(lastMsg.Content))
                {
                    _agentService.AddAssistantMessage(lastMsg.Content);
                    AddSystemMessage("Generation stopped by user");
                }
                else
                {
                    _messages.Remove(lastMsg);
                }
            }
        }
        catch (Exception ex)
        {
            LogUI($"❌ Помилка: {ex.Message}");
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                AddSystemMessage($"Error: {ex.Message}");
            });
        }
        finally
        {
            // Відновити UI
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // ✅ Гарантовано приховати typing indicator
                if (typingIndicator != null)
                    typingIndicator.IsVisible = false;
                
                sendBtn.IsEnabled = true;
                input.IsEnabled = true;
                input.Focus();
                if (stopBtn != null)
                    stopBtn.IsVisible = false;
            });
        }
    }

    /// <summary>
    /// Перевірка чи текст містить технічні токени (додатковий захист на UI рівні)
    /// </summary>
    private bool ContainsTechnicalTokens(string text)
    {
        var patterns = new[] 
        { 
            "<end_of_turn>", 
            "<start_of_turn>", 
            "<eos>", 
            "<|", 
            "|>",
            "<bos>",
            "<pad>"
        };
        
        return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private void LogUI(string message)
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [ChatWindow] {message}");
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void ClearHistory_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _cts?.Cancel();
            _agentService.ClearHistory();
            _messages.Clear();
            _lastUserMessage = null;
            
            AddSystemMessage("History cleared");
        }
        catch (Exception ex)
        {
            AddSystemMessage($"Failed to clear history: {ex.Message}");
        }
    }

    private void EditInstruction_Click(object? sender, RoutedEventArgs e)
    {
        var instructionWindow = new InstructionEditorWindow
        {
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        instructionWindow.ShowDialog(this);
    }

    // ==== Context Menu Actions === =

    private void CopyMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: ChatMessage message })
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            clipboard?.SetTextAsync(message.Content);
        }
    }

    private async void SaveMessageToFile_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: ChatMessage message })
        {
            try
            {
                var storageProvider = StorageProvider;
                if (storageProvider == null)
                {
                    AddSystemMessage("Storage provider not available");
                    return;
                }

                var file = await storageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
                {
                    Title = "Save Message to File",
                    DefaultExtension = "txt",
                    SuggestedFileName = "message.txt",
                    FileTypeChoices = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("Markdown Files") { Patterns = new[] { "*.md" } },
                        new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*" } }
                    }
                });

                if (file != null)
                {
                    var path = file.Path.LocalPath;
                    await System.IO.File.WriteAllTextAsync(path, message.Content);
                    AddSystemMessage($"Saved to {System.IO.Path.GetFileName(path)}");
                }
            }
            catch (Exception ex)
            {
                AddSystemMessage($"Failed to save: {ex.Message}");
            }
        }
    }

    private void RegenerateMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (_lastUserMessage != null)
        {
            // Видалити останнє повідомлення асистента
            var lastAssistant = _messages.LastOrDefault(m => m.Sender == AssistantConfig.Name);
            if (lastAssistant != null)
                _messages.Remove(lastAssistant);

            // Повторно відправити останнє повідомлення корис��увача
            var input = this.FindControl<TextBox>("InputTextBox");
            if (input != null)
            {
                input.Text = _lastUserMessage.Content;
                Send_Click(null, new RoutedEventArgs());
            }
        }
    }

    private void DeleteMessage_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: ChatMessage message })
        {
            _messages.Remove(message);
        }
    }

    // ==== Helper Methods === =

    /// <summary>
    /// Додати системне повідомлення з перевіркою на дублікати
    /// </summary>
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
            LogUI($"⚠️ Запобігли дублікату системного повідомлення: {message}");
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

    private void MessagesScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (sender is not ScrollViewer sv)
            return;

        // Consider "at bottom" if within threshold, so small layout changes don't disable autoscroll.
        var distanceToBottom = sv.Extent.Height - sv.Viewport.Height - sv.Offset.Y;
        var isNearBottom = distanceToBottom <= AutoScrollBottomThreshold;

        // If user scrolls up -> disable auto-scroll; if they return to bottom -> enable it again.
        _autoScrollEnabled = isNearBottom;

        // Keep existing fields updated (used elsewhere/logging)
        _lastScrollPosition = sv.Offset.Y;
        _isUserScrolling = !isNearBottom;
    }

    private async Task ScrollToBottom()
    {
        // Only scroll if user hasn't scrolled away from the bottom.
        if (!_autoScrollEnabled)
            return;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessagesScroll");
            scrollViewer?.ScrollToEnd();
        });

        // Ensure we scroll after layout is updated (streaming can change message height).
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MessagesScroll");
            scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Render);
    }

    private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Підписуємося на PropertyChanged для нових повідомлень, відписуємось для видалених
        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is ChatMessage cm)
                {
                    cm.PropertyChanged += ChatMessage_PropertyChanged;
                }
            }
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems)
            {
                if (item is ChatMessage cm)
                {
                    cm.PropertyChanged -= ChatMessage_PropertyChanged;
                }
            }
        }

        // ✅ ПОКРАЩЕНИЙ автоскрол при додаванні/видаленні повідомлень
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await ScrollToBottom();
        }, DispatcherPriority.Background);
    }

    private void ChatMessage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Коли оновлюється Content у повідомленні, прокручуємо вниз
        if (sender is ChatMessage cm && e.PropertyName == nameof(ChatMessage.Content))
        {
            // Переконаємось, що це повідомлення асистента або системи
            if (cm.Sender == AssistantConfig.Name || cm.Sender == "Assistant" || cm.Sender == "System")
            {
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // ✅ ПОКРАЩЕНИЙ скрол: збільшена затримка для streaming оновлень
                    await ScrollToBottom();
                }, DispatcherPriority.Render);
            }
        }
    }

    private async Task LoadModelInfoAsync()
    {
        try
        {
            // Попробувати отримати інформацію через AgentService (він обгортка над GemmaModelManager)
            var info = App.AgentService != null ? await App.AgentService.GetModelInfoAsync() : null;
            if (info != null && !string.IsNullOrEmpty(info.Name))
            {
                // Наприклад: "Gemma-3-1B (2bit)" або просто ім'я
                CurrentModelDisplayName = info.Name;
            }
            else if (App.GemmaConfig != null)
            {
                // Fallback: взяти ім'я моделі з шляху файлу (без розширення)
                var path = App.GemmaConfig.ModelPath ?? string.Empty;
                CurrentModelDisplayName = string.IsNullOrEmpty(path) ? string.Empty : Path.GetFileNameWithoutExtension(path);
            }
            else
            {
                CurrentModelDisplayName = string.Empty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ChatWindow] ⚠️ Не вдалося отримати ModelInfo: {ex.Message}");
            // Нічого критичного — залишити порожнім
            CurrentModelDisplayName = string.Empty;
        }
    }

    private void OnLanguageChanged()
    {
        foreach (var msg in _messages)
        {
            msg.RefreshLocalized();
        }

        // Оновити локалізовані підписи кнопок при зміні мови
        var sendBtn = this.FindControl<Button>("SendButton");
        var stopBtn = this.FindControl<Button>("StopButton");
        if (sendBtn != null)
            sendBtn.Content = LocalizationService.GetString("Key.Send", "Send");
        if (stopBtn != null)
            stopBtn.Content = LocalizationService.GetString("Key.Stop", "Stop");

        // Оновити Watermark поля вводу
        var inputBox = this.FindControl<TextBox>("InputTextBox");
        if (inputBox != null)
            inputBox.Watermark = LocalizationService.GetString("Key.TypeMessage", "Type your message here...");
    }

    protected override void OnClosed(EventArgs e)
    {
        // Відписатися від події при закритті вікна
        LocalizationService.LanguageChanged -= OnLanguageChanged;
        base.OnClosed(e);
    }

    private void ComplaintAboutAnswer_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi)
            return;

        if (mi.Tag is not ChatMessage msg)
            return;

        // Only makes sense for assistant answers, but we don't hard-block
        var w = WindowManager.ShowSingleton<ComplaintWindow>(this, factory: () => new ComplaintWindow(msg.Content));
        w.Activate();
    }
}
