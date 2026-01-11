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
        
        // ✅ Initialize Reasoning and Memory services from global instances
        _reasoningService = App.ReasoningService;
        _memoryService = App.MemoryService;
        
        // Subscribe to collection changes for auto-scroll and subscribe to PropertyChanged for content updates
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

        // Load model name asynchronously (do not block UI)
        _ = LoadModelInfoAsync();

        this.Opened += ChatWindow_Opened;
        LocalizationService.LanguageChanged += OnLanguageChanged;

        // Set localized labels for Send/Stop buttons when creating window
        var sendBtn = this.FindControl<Button>("SendButton");
        var stopBtn = this.FindControl<Button>("StopButton");
        if (sendBtn != null)
            sendBtn.Content = LocalizationService.GetString("Key.Send", "Send");
        if (stopBtn != null)
            stopBtn.Content = LocalizationService.GetString("Key.Stop", "Stop");

        // Set localized Watermark for input field
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
        // ✅ Initialize SaveToFileTool with this window context
        if (App.TabManager != null)
        {
            var saveToFileTool = new SaveToFileTool(
                App.TabManager,
                ownerWindow: this
            );
            
            // Set tool in agent
            App.MicrosoftInsaitAgent.SetSaveToFileTool(saveToFileTool);
            
            Console.WriteLine("[ChatWindow] ✅ SaveToFileTool підключено до агента");
        }
        else
        {
            Console.WriteLine("[ChatWindow] ⚠️ TabManager не доступний, SaveToFileTool не підключено");
        }

        // Load history
        try
        {
            var recent = _historyService.GetRecent(100);
            var orderedMessages = recent.OrderBy(m => m.Timestamp).ToList();
            
            foreach (var msg in orderedMessages)
            {
                _messages.Add(msg);
            }
            
            // ✅ Add welcome message if history is empty
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
            await ScrollToBottom();
        }
        catch (Exception ex)
        {
            AddSystemMessage($"❌ Помилка завантаже��ня історії: {ex.Message}");
        }
        
        // Background model initialization via Microsoft.Agents
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
    /// Input text validation before sending
    /// </summary>
    private bool ValidateInput(string input)
    {
        // Check for null or empty string
        if (string.IsNullOrEmpty(input))
            return false;
        
        // Check for string with only spaces, tabs and newlines
        if (string.IsNullOrWhiteSpace(input))
            return false;
        
        // Check for string with only newlines
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

        // Get text WITHOUT trim for validation
        var rawText = input.Text ?? string.Empty;
        
        // Professional validation
        if (!ValidateInput(rawText))
        {
            // Clear input field if user tried to send empty message
            input.Text = string.Empty;
            input.Focus();
            return;
        }

        // Only after validation do trim for sending
        var text = rawText.Trim();

        // Additional length check
        if (text.Length == 0)
        {
            input.Text = string.Empty;
            input.Focus();
            return;
        }

        LogUI($"📨 Відправлення повідомлення через Microsoft.Agents: {text.Length} символів");

        // Add user message
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
        await ScrollToBottom();

        // Show typing indicator
        if (typingIndicator != null)
            typingIndicator.IsVisible = true;

        // UI state
        sendBtn.IsEnabled = false;
        input.IsEnabled = false;
        if (stopBtn != null)
            stopBtn.IsVisible = true;

        // Cancel previous generation if it was running
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            // Check reasoning and memory settings
            var instruction = _instructionService.GetUserInstruction();
            var reasoningEnabled = instruction?.ReasoningEnabled ?? false;
            var memoryEnabled = instruction?.GlobalMemoryEnabled ?? false;

            LogUI($"🧠 Reasoning Mode: {reasoningEnabled}, 💾 Global Memory: {memoryEnabled}");

            // ======== GLOBAL MEMORY: Load relevant facts ========
            if (memoryEnabled && _memoryService != null)
            {
                try
                {
                    var relevantFacts = await _memoryService.QueryMemoryAsync(text, maxResults: 5);
                    if (relevantFacts.Any())
                    {
                        LogUI($"💾 Знайдено {relevantFacts.Count} релевантних фактів з пам'яті");
                        // TODO: In the future we can pass memoryContext to prompt
                    }
                }
                catch (Exception ex)
                {
                    LogUI($"⚠️ Помилка пошуку в пам'яті: {ex.Message}");
                }
            }

            // ======== REASONING MODE: Step-by-step execution with streaming ========
            if (reasoningEnabled && _reasoningService != null)
            {
                try
                {
                    LogUI("🧠 Початок reasoning chain...");
                    
                    var conversationId = Guid.NewGuid();
                    
                    // Dictionary to track step messages
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
                                    // Show status as system message
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
                                    // Create new message for step with localization
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
                                    
                                    // Remove previous status message
                                    if (statusMsg != null)
                                    {
                                        _messages.Remove(statusMsg);
                                        statusMsg = null;
                                    }
                                    break;
                                
                                case ReasoningEventType.StepContent:
                                    // Add token to step content
                                    if (stepMessages.TryGetValue(reasoningEvent.StepNumber, out var currentStepMsg))
                                    {
                                        currentStepMsg.Content += reasoningEvent.Content;
                                    }
                                    break;
                                
                                case ReasoningEventType.StepComplete:
                                    // Step complete - do nothing, just log
                                    LogUI(reasoningEvent.Message ?? "");
                                    break;
                                
                                case ReasoningEventType.FinalAnswerContent:
                                    // Create final answer message if not yet created
                                    if (finalAnswerMsg == null)
                                    {
                                        // Remove status message if present
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
                                        
                                        // Hide typing indicator
                                        if (typingIndicator != null)
                                            typingIndicator.IsVisible = false;
                                    }
                                    
                                    // Add token to final answer
                                    finalAnswerMsg.Content += reasoningEvent.Content;
                                    break;
                                
                                case ReasoningEventType.Complete:
                                    // Reasoning complete with localization
                                    LogUI(LocalizationService.GetString("Key.ReasoningComplete", "🎉 Reasoning завершено!"));
                                    
                                    // Save final answer to history
                                    if (finalAnswerMsg != null && !string.IsNullOrWhiteSpace(finalAnswerMsg.Content))
                                    {
                                        _agentService.AddAssistantMessage(finalAnswerMsg.Content);
                                    }
                                    
                                    // Remove status message if it remains
                                    if (statusMsg != null)
                                    {
                                        _messages.Remove(statusMsg);
                                    }
                                    break;
                                
                                case ReasoningEventType.Error:
                                    // Error
                                    AddSystemMessage(reasoningEvent.Message ?? "Unknown error");
                                    break;
                            }
                        });
                        
                        // Update scroll after each event
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
                    // Fallback to standard mode
                    reasoningEnabled = false;
                }
            }

            // ======== STANDARD MODE: Standard response ========
            if (!reasoningEnabled)
            {
                // Create placeholder for assistant response
                var assistantMsg = new ChatMessage
                {
                    Sender = AssistantConfig.Name,
                    Content = string.Empty,
                    Timestamp = DateTime.UtcNow
                };
                
                // Hide typing indicator before adding message
                if (typingIndicator != null)
                    typingIndicator.IsVisible = false;
                
                _messages.Add(assistantMsg);
                await ScrollToBottom();

                // Streaming response with throttling and filtering
                var responseBuilder = new StringBuilder();
                var lastUpdateTime = DateTime.UtcNow;
                const int updateIntervalMs = 50;
                var hasAnyContent = false;

                LogUI($"🚀 [Microsoft.Agents] Початок отримання відповіді");

                await foreach (var token in _agentService.GetResponseStreamAsync(text, _cts.Token))
                {
                    // Skip empty tokens and tokens with only spaces
                    if (string.IsNullOrWhiteSpace(token))
                    {
                        continue;
                    }

                    // 🛡️ Technical tokens filter
                    if (ContainsTechnicalTokens(token))
                    {
                        LogUI($"🚫 Технічний токен в UI, СТОП відображення");
                        break;
                    }

                    responseBuilder.Append(token);
                    hasAnyContent = true;
                    
                    // Throttle UI updates (every 50ms)
                    if ((DateTime.UtcNow - lastUpdateTime).TotalMilliseconds >= updateIntervalMs)
                    {
                        var currentContent = responseBuilder.ToString();
                        
                        // Check if response ended (extra spaces at the end may indicate end)
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

                // Final update with cleanup of extra spaces
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var finalContent = responseBuilder.ToString().Trim();
                    
                    // Remove multiple empty lines (leave maximum 2 newlines in a row)
                    while (finalContent.Contains("\n\n\n"))
                    {
                        finalContent = finalContent.Replace("\n\n\n", "\n\n");
                    }
                    
                    assistantMsg.Content = finalContent;
                    
                    // Guarantee scrolling after final content update
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

            // ======== GLOBAL MEMORY: Save new facts ========
            if (memoryEnabled && _memoryService != null)
            {
                try
                {
                    // Save facts from user message
                    await _memoryService.ProcessMessageAsync(userMsg);
                    
                    // Save facts from assistant response
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
            // Restore UI
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                // ✅ Guaranteed to hide typing indicator
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
    /// Check if text contains technical tokens (additional UI-level protection)
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

            // Resend last user message
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
    /// Add system message with duplicate check
    /// </summary>
    private void AddSystemMessage(string message)
    {
        // ✅ Duplicate check
        var lastMsg = _messages.LastOrDefault();
        if (lastMsg != null && 
            lastMsg.Sender == "System" && 
            lastMsg.Content == message &&
            (DateTime.UtcNow - lastMsg.Timestamp).TotalSeconds < 5)
        {
            // Don't add duplicate if last system message is the same and was less than 5 seconds ago
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
        _ = ScrollToBottom();
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
        // Subscribe to PropertyChanged for new messages, unsubscribe for removed ones
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

        // ✅ IMPROVED auto-scroll when adding/removing messages
        _ = Dispatcher.UIThread.InvokeAsync(async () =>
        {
            await ScrollToBottom();
        }, DispatcherPriority.Background);
    }

    private void ChatMessage_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // When Content updates in message, scroll down
        if (sender is ChatMessage cm && e.PropertyName == nameof(ChatMessage.Content))
        {
            // Make sure this is assistant or system message
            if (cm.Sender == AssistantConfig.Name || cm.Sender == "Assistant" || cm.Sender == "System")
            {
                _ = Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    // ✅ IMPROVED scroll: increased delay for streaming updates
                    await ScrollToBottom();
                }, DispatcherPriority.Render);
            }
        }
    }

    private async Task LoadModelInfoAsync()
    {
        try
        {
            // Try to get information via AgentService (it's a wrapper over GemmaModelManager)
            var info = App.AgentService != null ? await App.AgentService.GetModelInfoAsync() : null;
            if (info != null && !string.IsNullOrEmpty(info.Name))
            {
                // For example: "Gemma-3-1B (2bit)" or just name
                CurrentModelDisplayName = info.Name;
            }
            else if (App.GemmaConfig != null)
            {
                // Fallback: take model name from file path (without extension)
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
            // Nothing critical — leave empty
            CurrentModelDisplayName = string.Empty;
        }
    }

    private void OnLanguageChanged()
    {
        foreach (var msg in _messages)
        {
            msg.RefreshLocalized();
        }

        // Update localized labels for buttons when language changes
        var sendBtn = this.FindControl<Button>("SendButton");
        var stopBtn = this.FindControl<Button>("StopButton");
        if (sendBtn != null)
            sendBtn.Content = LocalizationService.GetString("Key.Send", "Send");
        if (stopBtn != null)
            stopBtn.Content = LocalizationService.GetString("Key.Stop", "Stop");

        // Update input field Watermark
        var inputBox = this.FindControl<TextBox>("InputTextBox");
        if (inputBox != null)
            inputBox.Watermark = LocalizationService.GetString("Key.TypeMessage", "Type your message here...");
    }

    protected override void OnClosed(EventArgs e)
    {
        // Unsubscribe from event when window closes
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
