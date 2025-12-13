# План міграції на Microsoft Agent Framework

## Мета
Переробити Insait Text Editor для використання **Microsoft Agent Framework** з інтеграцією **LlamaSharp** та моделі **Gemma-3-1B** у форматі single-file додатку з кастомною інструкцією користувача та налаштуваннями продуктивності.

---

## Поточний стан

### Існуюча архітектура:
- **LlamaSharp** напряму викликається в `ChatWindow.axaml.cs`
- **Модель**: Gemma-3-1B (Q2_K_XL) та Phi-4-mini
- **Система промптів**: статична (`AssistantConfig.cs`)
- **Історія чату**: зберігається в LiteDB (`ChatHistoryService.cs`)
- **UI**: Avalonia-based чат вікно з стрімінгом відповідей

### Проблеми:
- Відсутня архітектура агентів (планування, інструменти)
- Немає можливості додавати кастомну інструкцію користувачем
- Жорстко зв'язаний код LlamaSharp з UI
- Дві моделі збільшують розмір додатку
- Немає контролю продуктивності (розмір контексту, токени генерації)

---

## Нова архітектура

### Ключові зміни:
✅ **Тільки Gemma-3-1B** (видалити Phi-4 для зменшення розміру)  
✅ **Single-file executable** (~800 MB)  
✅ **Універсальна інструкція** (англійською для кращого розуміння моделлю)  
✅ **Налаштування продуктивності**: Context Size (вхід) + Max Tokens (вихід)  
✅ **Інструмент SaveToFile**: створення текстового файлу → діалог збереження Windows → відкриття як вкладка  

### Структура компонентів

```
InsaitTextEditor/
├── Agents/
│   ├── InsaitAgent.cs                  # Головний агент на базі Microsoft.Agents
│   ├── AgentConfig.cs                  # Конфігурація агента
│   └── Tools/
│       └── SaveToFileTool.cs          # Інструмент збереження відповіді у файл
├── AI/
│   ├── LlamaSharpInferenceEngine.cs   # Адаптер LlamaSharp для Agent Framework
│   ├── GemmaModelManager.cs           # Управління Gemma-3 моделлю
│   ├── GemmaConfig.cs                 # Параметри Gemma-3
│   └── PromptBuilder.cs               # Побудова промптів з кастомною інструкцією
├── Services/
│   ├── AgentService.cs                # Сервіс взаємодії з агентом
│   ├── UserInstructionService.cs      # Управління ОДНІЄЮ інструкцією користувача
│   ├── ChatHistoryService.cs          # (існуючий, модифікувати)
│   └── ConversationStateService.cs    # Стан розмови для Agent Framework
├── Models/
│   ├── AgentMessage.cs                # Повідомлення агента (розширює ChatMessage)
│   ├── UserInstruction.cs             # ОДНА кастомна інструкція користувача
│   ├── AgentResponse.cs               # Відповідь агента з метаданими
│   └── ToolInvocation.cs              # Виклик інструменту
└── Windows/
    ├── ChatWindow.axaml.cs            # (модифікувати для AgentService)
    └── InstructionEditorWindow.axaml  # Вікно редагування ОДНІЄЇ інструкції
        └── InstructionEditorWindow.axaml.cs
```

---

## Детальний план дій

### Фаза 1: Підготовка інфраструктури
**Термін**: 1 година

#### 1.1 Оновлення NuGet пакетів
- ✅ `Microsoft.Agents.Client` v1.3.93-beta (вже встановлено)
- ✅ `Microsoft.Agents.Core` v1.3.93-beta (вже встановлено)
- ✅ `LlamaSharp` v0.25.0 (вже встановлено)
- ✅ Нова модель Gemma-3-1B додана в AiModel/
- **Видалити** Phi-4-mini модель з AiModel/

#### 1.2 Створення базових моделей
**Файли для створення:**

- **`Models/UserInstruction.cs`**
  ```csharp
  public class UserInstruction
  {
      // Інструкція АНГЛІЙСЬКОЮ для кращого розуміння моделлю
      public string Content { get; set; } = string.Empty;
      public DateTime LastModified { get; set; }
      
      // Налаштування продуктивності
      public int ContextSize { get; set; } = 4096;  // Вхідні токени (2048-8192)
      public int MaxTokens { get; set; } = 1024;    // Вихідні токени (256-2048)
  }
  ```

- **`Models/AgentMessage.cs`**
  ```csharp
  public class AgentMessage : ChatMessage
  {
      public List<ToolInvocation> ToolCalls { get; set; } = new();
      public string ModelUsed { get; set; } = "Gemma-3-1B";
      public int TokensUsed { get; set; }
  }
  ```

- **`Models/ToolInvocation.cs`**
  ```csharp
  public class ToolInvocation
  {
      public string ToolName { get; set; } = string.Empty;
      public string Parameters { get; set; } = string.Empty;
      public string Result { get; set; } = string.Empty;
      public DateTime ExecutedAt { get; set; }
  }
  ```

- **`Models/AgentResponse.cs`**
  ```csharp
  public class AgentResponse
  {
      public string Content { get; set; } = string.Empty;
      public List<ToolInvocation> ToolsUsed { get; set; } = new();
      public int TokensGenerated { get; set; }
      public TimeSpan Duration { get; set; }
  }
  ```

---

### Фаза 2: Інтеграція LlamaSharp з Agent Framework (тільки Gemma-3)
**Термін**: 2-3 години

#### 2.1 Створення AI компонентів

**`AI/GemmaConfig.cs`**
```csharp
public class GemmaConfig
{
    private readonly UserInstructionService _instructionService;
    
    public GemmaConfig(UserInstructionService instructionService)
    {
        _instructionService = instructionService;
    }
    
    public string ModelPath => Path.Combine(AppContext.BaseDirectory, "AiModel", "gemma-3-1b-it-UD-Q2_K_XL.gguf");
    
    // Динамічні параметри з UserInstruction
    public int ContextSize 
    {
        get
        {
            var instruction = _instructionService.GetUserInstruction();
            return instruction?.ContextSize ?? 4096;
        }
    }
    
    public int MaxTokens
    {
        get
        {
            var instruction = _instructionService.GetUserInstruction();
            return instruction?.MaxTokens ?? 1024;
        }
    }
    
    // Статичні параметри
    public int GpuLayerCount => 0;  // 0 = CPU, 33 = повна GPU
    public int BatchSize => 512;
    public int Threads => Environment.ProcessorCount / 2;
    public uint Seed => 1337;
    
    // Inference параметри
    public float Temperature => 0.7f;
    public float TopP => 0.9f;
    public int TopK => 40;
    public float RepeatPenalty => 1.1f;
    
    // Gemma-3 формат
    public string UserTurnStart => "<start_of_turn>user\n";
    public string UserTurnEnd => "<end_of_turn>\n";
    public string ModelTurnStart => "<start_of_turn>model\n";
    public string ModelTurnEnd => "<end_of_turn>\n";
}
```

**`AI/GemmaModelManager.cs`**
- Завантаження Gemma-3 моделі при старті додатку
- Singleton pattern (одна модель в пам'яті)
- Lazy initialization
- Dispose при закритті додатку
- Управління контекстом та сесією

**`AI/LlamaSharpInferenceEngine.cs`**
- Реалізація інтерфейсу для Agent Framework
- Адаптер між `LLamaContext` та Agent API
- Підтримка streaming відповідей
- Обробка Gemma-3 stop tokens
- Підрахунок токенів

**`AI/PromptBuilder.cs`**
```csharp
public class PromptBuilder
{
    private readonly GemmaConfig _config;
    private readonly UserInstructionService _instructionService;
    
    public string BuildSystemPrompt()
    {
        // Базовий промпт АНГЛІЙСЬКОЮ
        var basePrompt = "You are Insait Assistant, an intelligent helper integrated into Insait Text Editor. " +
                        "Respond concisely in the user's language. If asked to save content to a file, " +
                        "use the save_to_file tool.";
        
        var userInstruction = _instructionService.GetUserInstruction();
        
        // Додати кастомну інструкцію користувача (АНГЛІЙСЬКОЮ)
        if (!string.IsNullOrWhiteSpace(userInstruction?.Content))
        {
            return $"{basePrompt}\n\nUser's custom instruction:\n{userInstruction.Content}";
        }
        
        return basePrompt;
    }
    
    public string FormatMessage(string role, string content)
    {
        // Gemma-3 формат: <start_of_turn>user\n...<end_of_turn>\n
        if (role == "user")
            return $"{_config.UserTurnStart}{content}{_config.UserTurnEnd}";
        else
            return $"{_config.ModelTurnStart}{content}{_config.ModelTurnEnd}";
    }
}
```

---

### Фаза 3: Створення Agent Framework компонентів
**Термін**: 3-4 години

#### 3.1 Агент та інструмент

**`Agents/AgentConfig.cs`**
```csharp
public class AgentConfig
{
    public string AgentName => "Insait Assistant";
    public string AgentDescription => "Асистент текстового редактора з можливістю збереження відповідей у файли";
    public List<string> AvailableTools => ["save_to_file"];
    public int MaxIterations => 3;
    public bool EnableReasoning => true;
}
```

**`Agents/InsaitAgent.cs`**
- Головний клас агента на базі Microsoft.Agents.Core
- Інтеграція з `LlamaSharpInferenceEngine`
- Планування дій (коли використовувати інструмент)
- Виклик `SaveToFileTool` при необхідності
- Управління станом розмови

**`Agents/Tools/SaveToFileTool.cs`**
```csharp
public class SaveToFileTool : ITool
{
    public string Name => "save_to_file";
    public string Description => "Зберігає відповідь асистента у текстовий файл, відкриває діалог збереження Windows, і відкриває файл як нову вкладку";
    
    public async Task<string> ExecuteAsync(string content)
    {
        // 1. Створити тимчасовий файл з content
        // 2. Відкрити Windows SaveFileDialog (через Avalonia)
        // 3. Якщо користувач зберіг - копіювати туди
        // 4. Викликати TabManager.OpenFile(savedPath)
        // 5. Повернути статус виконання
    }
    
    // Параметри:
    // - content: текст для збереження
    // - suggestedFileName: пропоноване ім'я файлу (опційно)
}
```

**Workflow інструменту SaveToFileTool:**
1. Агент генерує відповідь
2. Агент вирішує, що треба зберегти у файл
3. Виклик: `save_to_file(content="...", suggestedFileName="response.txt")`
4. Інструмент відкриває діалог збереження Windows (`SaveFileDialog`)
5. Користувач обирає місце і ім'я файлу
6. Файл зберігається
7. `TabManager.OpenFile(path)` відкриває файл як нову вкладку
8. Інструмент повертає результат агенту
9. Агент інформує користувача про успіх

---

### Фаза 4: Сервіси
**Термін**: 2 години

#### 4.1 Сервіси управління

**`Services/UserInstructionService.cs`**
```csharp
public class UserInstructionService
{
    private readonly SettingsService _settings;
    private const string INSTRUCTION_KEY = "UserInstruction";
    
    public UserInstruction? GetUserInstruction()
    {
        var instruction = _settings.GetSetting<UserInstruction>(INSTRUCTION_KEY);
        
        // Якщо немає - створити з дефолтними значеннями
        if (instruction == null)
        {
            instruction = new UserInstruction
            {
                Content = string.Empty,
                ContextSize = 4096,  // Дефолт: 4K вхідних токенів
                MaxTokens = 1024,    // Дефолт: 1K вихідних токенів
                LastModified = DateTime.UtcNow
            };
        }
        
        return instruction;
    }
    
    public void SaveUserInstruction(UserInstruction instruction)
    {
        instruction.LastModified = DateTime.UtcNow;
        _settings.SaveSetting(INSTRUCTION_KEY, instruction);
    }
    
    public void ClearUserInstruction()
    {
        var defaultInstruction = new UserInstruction
        {
            Content = string.Empty,
            ContextSize = 4096,
            MaxTokens = 1024,
            LastModified = DateTime.UtcNow
        };
        _settings.SaveSetting(INSTRUCTION_KEY, defaultInstruction);
    }
    
    // Метод для перезавантаження моделі з новими параметрами
    public event EventHandler? InstructionChanged;
    
    public void NotifyInstructionChanged()
    {
        InstructionChanged?.Invoke(this, EventArgs.Empty);
    }
}
```

**`Services/AgentService.cs`**
```csharp
public class AgentService
{
    private readonly InsaitAgent _agent;
    private readonly ConversationStateService _conversationState;
    private readonly TabManager _tabManager;
    
    public async Task<AgentResponse> SendMessageAsync(string userMessage, CancellationToken ct)
    {
        // Відправити повідомлення агенту
        // Агент може викликати SaveToFileTool
        // Повернути відповідь з метаданими
    }
    
    public async IAsyncEnumerable<string> StreamResponseAsync(string userMessage, CancellationToken ct)
    {
        // Streaming відповідь для UI
        await foreach (var token in _agent.StreamAsync(userMessage, ct))
        {
            yield return token;
        }
    }
}
```

**`Services/ConversationStateService.cs`**
```csharp
public class ConversationStateService
{
    private List<AgentMessage> _conversationHistory = new();
    
    public void AddMessage(AgentMessage message)
    {
        _conversationHistory.Add(message);
        // Обмежити історію до останніх N повідомлень для контексту
        if (_conversationHistory.Count > 20)
            _conversationHistory.RemoveAt(0);
    }
    
    public List<AgentMessage> GetRecentHistory(int count = 10)
    {
        return _conversationHistory.TakeLast(count).ToList();
    }
    
    public void Clear()
    {
        _conversationHistory.Clear();
    }
}
```

#### 4.2 Модифікація існуючих сервісів

**`Services/ChatHistoryService.cs`** (модифікувати)
```csharp
// Додати підтримку AgentMessage
public void AddAgentMessage(AgentMessage message)
{
    using var db = new LiteDatabase(_dbPath);
    var col = db.GetCollection<AgentMessage>("agent_messages");
    col.EnsureIndex(x => x.Timestamp);
    col.Insert(message);
}

// Зберігати інформацію про використані інструменти
```

**`Services/SettingsService.cs`** (модифікувати)
```csharp
// Додати збереження UserInstruction
public void SaveSetting(string key, object value)
{
    // JSON серіалізація та збереження
}

public T? GetSetting<T>(string key)
{
    // Завантаження та десеріалізація
}
```

---

### Фаза 5: UI компоненти
**Термін**: 2-3 години

#### 5.1 Модернізація дизайну ChatWindow (сучасний UI)

**Ключові покращення дизайну:**

1. **Сучасна структура повідомлень** (як в ChatGPT, Claude):
   - Альтернативні фони для User/Assistant
   - Аватари/іконки для кожного учасника
   - Rounded corners з тінями
   - Responsive width (обмеження ширини для читабельності)

2. **Typing indicator** (індикатор набору):
   - Анімовані три крапки під час генерації
   - Skeleton loader для placeholder

3. **Покращена область вводу**:
   - Auto-resize TextBox (розширюється при багаторядковому вводі)
   - Floating action buttons (Send, Stop, Attach)
   - Character/token counter
   - Placeholder з підказками

4. **Smooth animations**:
   - Fade-in для нових повідомлень
   - Slide-up для інпуту
   - Smooth scroll до останнього повідомлення

5. **Контекстне меню для повідомлень**:
   - Copy (копіювати)
   - Save to file (зберегти у файл)
   - Regenerate (перегенерувати відповідь)

**`Windows/ChatWindow.axaml`** (ПОВНА модернізація UI)
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:models="clr-namespace:InsaitTextEditor.Models"
        x:Class="InsaitTextEditor.Windows.ChatWindow"
        Title="Insait Assistant"
        Width="1000" Height="720"
        MinWidth="800" MinHeight="600"
        WindowStartupLocation="CenterOwner"
        SystemDecorations="None"
        Background="#F0F0F0">

  <Window.Resources>
    <!-- Існуючі кольори зберігаються -->
    <SolidColorBrush x:Key="AppGreenBrush" Color="#00C853"/>
    <SolidColorBrush x:Key="AppPurpleBrush" Color="#6A0DAD"/>
    <SolidColorBrush x:Key="AppGreyTopBrush" Color="#A9A9A9"/>
    <SolidColorBrush x:Key="AppDarkOrangeBrush" Color="#CC5500"/>
    
    <!-- Нові кольори для сучасного дизайну -->
    <SolidColorBrush x:Key="UserMessageBg" Color="#E8F5E9"/>      <!-- Світло-зелений -->
    <SolidColorBrush x:Key="AssistantMessageBg" Color="#F3E5F5"/> <!-- Світло-фіолетовий -->
    <SolidColorBrush x:Key="InputAreaBg" Color="#FFFFFF"/>
    <SolidColorBrush x:Key="ShadowColor" Color="#40000000"/>

    <!-- DataTemplate для повідомлення користувача -->
    <DataTemplate x:Key="UserMessageTemplate" DataType="models:ChatMessage">
      <Border Margin="8,4,8,4" HorizontalAlignment="Right" MaxWidth="650">
        <Border Background="{StaticResource UserMessageBg}"
                CornerRadius="16,16,4,16"
                Padding="16,12"
                BoxShadow="0 2 8 0 #20000000">
          <Grid ColumnDefinitions="Auto,*">
            <!-- Аватар користувача -->
            <Border Grid.Column="0"
                    Width="32" Height="32"
                    Background="{StaticResource AppGreenBrush}"
                    CornerRadius="16"
                    Margin="0,0,12,0"
                    VerticalAlignment="Top">
              <TextBlock Text="👤" 
                         FontSize="18"
                         HorizontalAlignment="Center"
                         VerticalAlignment="Center"/>
            </Border>
            
            <!-- Контент повідомлення -->
            <StackPanel Grid.Column="1">
              <TextBlock Text="You" 
                         FontWeight="SemiBold" 
                         FontSize="13"
                         Foreground="#555"
                         Margin="0,0,0,6"/>
              <TextBlock Text="{Binding Content}"
                         TextWrapping="Wrap"
                         FontSize="14"
                         LineHeight="22"/>
              <TextBlock Text="{Binding Timestamp, StringFormat='{}{0:HH:mm}'}"
                         FontSize="11"
                         Foreground="#888"
                         Margin="0,6,0,0"
                         HorizontalAlignment="Right"/>
            </StackPanel>
          </Grid>
        </Border>
      </Border>
    </DataTemplate>

    <!-- DataTemplate для повідомлення асистента -->
    <DataTemplate x:Key="AssistantMessageTemplate" DataType="models:ChatMessage">
      <Border Margin="8,4,8,4" HorizontalAlignment="Left" MaxWidth="650">
        <Border Background="{StaticResource AssistantMessageBg}"
                CornerRadius="16,16,16,4"
                Padding="16,12"
                BoxShadow="0 2 8 0 #20000000">
          <Grid ColumnDefinitions="Auto,*">
            <!-- Аватар асистента -->
            <Border Grid.Column="0"
                    Width="32" Height="32"
                    Background="{StaticResource AppPurpleBrush}"
                    CornerRadius="16"
                    Margin="0,0,12,0"
                    VerticalAlignment="Top">
              <TextBlock Text="🤖" 
                         FontSize="18"
                         HorizontalAlignment="Center"
                         VerticalAlignment="Center"/>
            </Border>
            
            <!-- Контент повідомлення -->
            <StackPanel Grid.Column="1">
              <TextBlock Text="Insait Assistant" 
                         FontWeight="SemiBold" 
                         FontSize="13"
                         Foreground="#555"
                         Margin="0,0,0,6"/>
              <TextBlock Text="{Binding Content}"
                         TextWrapping="Wrap"
                         FontSize="14"
                         LineHeight="22"/>
              
              <!-- Показувати використані інструменти -->
              <Border x:Name="ToolCallsIndicator"
                      Background="#FFF3E0"
                      CornerRadius="8"
                      Padding="8,6"
                      Margin="0,8,0,0"
                      IsVisible="{Binding ToolCalls.Count, Converter={StaticResource GreaterThanZero}}">
                <StackPanel Orientation="Horizontal" Spacing="6">
                  <TextBlock Text="🛠️" FontSize="14"/>
                  <TextBlock Text="Використано інструмент: save_to_file" 
                             FontSize="12"
                             Foreground="#F57C00"/>
                </StackPanel>
              </Border>
              
              <TextBlock Text="{Binding Timestamp, StringFormat='{}{0:HH:mm}'}"
                         FontSize="11"
                         Foreground="#888"
                         Margin="0,6,0,0"/>
            </StackPanel>
          </Grid>
        </Border>
      </Border>
    </DataTemplate>

    <!-- MessageTemplateSelector -->
    <conv:MessageTemplateSelector x:Key="MessageSelector"
                                   UserTemplate="{StaticResource UserMessageTemplate}"
                                   AssistantTemplate="{StaticResource AssistantMessageTemplate}"/>
  </Window.Resources>

  <Grid>
    <Grid.RowDefinitions>
      <RowDefinition Height="56"/>  <!-- Title bar -->
      <RowDefinition Height="*"/>   <!-- Messages area -->
      <RowDefinition Height="Auto"/> <!-- Input area -->
    </Grid.RowDefinitions>

    <!-- Title bar (зберігаємо існуючий дизайн) -->
    <Border Grid.Row="0"
            Background="{StaticResource AppGreyTopBrush}"
            PointerPressed="TitleBar_PointerPressed">
      <DockPanel Margin="16,0">
        <StackPanel Orientation="Horizontal" Spacing="12" VerticalAlignment="Center">
          <TextBlock Text="💬" FontSize="24"/>
          <TextBlock Text="Insait Assistant" 
                     VerticalAlignment="Center" 
                     FontSize="16"
                     FontWeight="SemiBold"/>
        </StackPanel>
        
        <!-- Window controls -->
        <StackPanel x:Name="TitleButtonsPanel"
                    DockPanel.Dock="Right"
                    Orientation="Horizontal"
                    Spacing="8"
                    VerticalAlignment="Center">
          <!-- Settings button -->
          <Button Width="40" Height="40"
                  Background="Transparent"
                  BorderThickness="0"
                  ToolTip.Tip="Settings"
                  Click="OpenInstructionEditor_Click">
            <TextBlock Text="⚙️" FontSize="18"/>
          </Button>
          
          <!-- Clear history button -->
          <Button Width="40" Height="40"
                  Background="Transparent"
                  BorderThickness="0"
                  ToolTip.Tip="Clear History"
                  Click="ClearHistory_Click">
            <TextBlock Text="🗑️" FontSize="18"/>
          </Button>
          
          <Border Width="1" Height="24" Background="#888" Margin="4,0"/>
          
          <!-- Minimize -->
          <Button Width="40" Height="40"
                  Background="Transparent"
                  Click="Minimize_Click">
            <TextBlock Text="―" FontSize="16"/>
          </Button>
          <!-- Maximize -->
          <Button Width="40" Height="40"
                  Background="Transparent"
                  Click="MaxRestore_Click">
            <TextBlock Text="□" FontSize="16"/>
          </Button>
          <!-- Close -->
          <Button Width="40" Height="40"
                  Background="Transparent"
                  Click="Close_Click">
            <TextBlock Text="✕" FontSize="16"/>
          </Button>
        </StackPanel>
      </DockPanel>
    </Border>

    <!-- Messages area -->
    <Border Grid.Row="1" Background="#FAFAFA">
      <ScrollViewer x:Name="MessagesScrollViewer"
                    VerticalScrollBarVisibility="Auto"
                    Padding="16,8">
        <ItemsControl x:Name="MessagesList"
                      ItemTemplateSelector="{StaticResource MessageSelector}">
          <ItemsControl.ItemsPanel>
            <ItemsPanelTemplate>
              <StackPanel Spacing="8"/>
            </ItemsPanelTemplate>
          </ItemsControl.ItemsPanel>
        </ItemsControl>
      </ScrollViewer>
    </Border>

    <!-- Input area (сучасний дизайн) -->
    <Border Grid.Row="2"
            Background="{StaticResource InputAreaBg}"
            BorderBrush="#E0E0E0"
            BorderThickness="0,1,0,0"
            Padding="16"
            BoxShadow="0 -2 12 0 #10000000">
      <Grid RowDefinitions="Auto,Auto">
        <!-- Typing indicator (показується під час генерації) -->
        <Border x:Name="TypingIndicator"
                Grid.Row="0"
                IsVisible="False"
                Background="#F5F5F5"
                CornerRadius="12"
                Padding="12,8"
                Margin="0,0,0,8">
          <StackPanel Orientation="Horizontal" Spacing="6">
            <TextBlock Text="💭" FontSize="14"/>
            <TextBlock Text="Insait Assistant is typing" 
                       FontSize="13"
                       Foreground="#666"/>
            <TextBlock x:Name="TypingDots" Text="..." FontSize="13" Foreground="#666"/>
          </StackPanel>
        </Border>

        <!-- Input box -->
        <Grid Grid.Row="1" ColumnDefinitions="*,Auto">
          <Border Grid.Column="0"
                  Background="#F8F8F8"
                  CornerRadius="24"
                  BorderBrush="#D0D0D0"
                  BorderThickness="1">
            <TextBox x:Name="InputTextBox"
                     AcceptsReturn="True"
                     TextWrapping="Wrap"
                     Watermark="Type your message... (Shift+Enter for new line)"
                     BorderThickness="0"
                     Background="Transparent"
                     Padding="16,12"
                     MinHeight="48"
                     MaxHeight="200"
                     FontSize="14"
                     VerticalContentAlignment="Center">
              <TextBox.KeyBindings>
                <KeyBinding Gesture="Enter" Command="{Binding SendCommand}"/>
              </TextBox.KeyBindings>
            </TextBox>
          </Border>

          <!-- Action buttons -->
          <StackPanel Grid.Column="1"
                      Orientation="Horizontal"
                      Spacing="8"
                      Margin="12,0,0,0"
                      VerticalAlignment="Center">
            <!-- Send button -->
            <Button x:Name="SendButton"
                    Width="48" Height="48"
                    Background="{StaticResource AppGreenBrush}"
                    CornerRadius="24"
                    BorderThickness="0"
                    ToolTip.Tip="Send message (Enter)"
                    Click="Send_Click">
              <TextBlock Text="📤" FontSize="20"/>
            </Button>
            
            <!-- Stop button (показується під час генерації) -->
            <Button x:Name="StopButton"
                    Width="48" Height="48"
                    Background="{StaticResource AppDarkOrangeBrush}"
                    CornerRadius="24"
                    BorderThickness="0"
                    IsVisible="False"
                    ToolTip.Tip="Stop generation"
                    Click="Stop_Click">
              <TextBlock Text="⏹️" FontSize="20"/>
            </Button>
          </StackPanel>
        </Grid>

        <!-- Character/Token counter -->
        <TextBlock x:Name="CharCounter"
                   Grid.Row="1"
                   Text="0 characters"
                   FontSize="11"
                   Foreground="#999"
                   Margin="16,4,0,0"
                   HorizontalAlignment="Left"
                   VerticalAlignment="Bottom"/>
      </Grid>
    </Border>

    <!-- Resize grips (зберігаємо існуючу логіку) -->
    <Border Grid.Row="0" Grid.RowSpan="3" Background="Transparent" 
            Width="5" HorizontalAlignment="Left" Cursor="SizeWest"
            PointerPressed="Resize_Left_PointerPressed"/>
    <Border Grid.Row="0" Grid.RowSpan="3" Background="Transparent" 
            Width="5" HorizontalAlignment="Right" Cursor="SizeEast"
            PointerPressed="Resize_Right_PointerPressed"/>
    <Border Grid.Row="0" Grid.RowSpan="3" Background="Transparent" 
            Height="5" VerticalAlignment="Top" Cursor="SizeNorth"
            PointerPressed="Resize_Top_PointerPressed"/>
    <Border Grid.Row="0" Grid.RowSpan="3" Background="Transparent" 
            Height="5" VerticalAlignment="Bottom" Cursor="SizeSouth"
            PointerPressed="Resize_Bottom_PointerPressed"/>
  </Grid>
</Window>
```

**Додатково створити `MessageTemplateSelector.cs`:**
```csharp
// Scripts/Converters/MessageTemplateSelector.cs
public class MessageTemplateSelector : IDataTemplate
{
    public IDataTemplate? UserTemplate { get; set; }
    public IDataTemplate? AssistantTemplate { get; set; }

    public Control? Build(object? param)
    {
        if (param is ChatMessage msg)
        {
            var template = msg.Sender == "User" ? UserTemplate : AssistantTemplate;
            return template?.Build(param);
        }
        return null;
    }

    public bool Match(object? data) => data is ChatMessage;
}
```

**Додати анімації в ChatWindow.axaml.cs:**
```csharp
private async void AddMessageWithAnimation(ChatMessage message)
{
    _messages.Add(message);
    
    // Scroll to bottom з smooth animation
    await Task.Delay(50);
    MessagesScrollViewer.ScrollToEnd();
}

private void ShowTypingIndicator(bool show)
{
    TypingIndicator.IsVisible = show;
    SendButton.IsVisible = !show;
    StopButton.IsVisible = show;
}

private void InputTextBox_TextChanged(object? sender, TextChangedEventArgs e)
{
    var text = InputTextBox.Text ?? "";
    CharCounter.Text = $"{text.Length} characters";
}
```

#### 5.2 Вікно редагування інструкції

**`Windows/InstructionEditorWindow.axaml`**
```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="InsaitTextEditor.Windows.InstructionEditorWindow"
        Title="Налаштування асистента" 
        Width="700" 
        Height="550"
        WindowStartupLocation="CenterOwner">
    <DockPanel>
        <!-- Заголовок -->
        <StackPanel DockPanel.Dock="Top" Margin="20,20,20,0">
            <TextBlock Text="Кастомна інструкція для асистента" 
                       FontSize="18" 
                       FontWeight="Bold"/>
            <TextBlock Text="Опишіть поведінку асистента англійською мовою для кращого розуміння" 
                       Foreground="#666" 
                       Margin="0,4,0,16"
                       TextWrapping="Wrap"/>
        </StackPanel>
        
        <!-- Кнопки -->
        <StackPanel DockPanel.Dock="Bottom" 
                    Orientation="Horizontal" 
                    HorizontalAlignment="Right" 
                    Margin="20">
            <Button Content="Зберегти" 
                    Click="Save_Click" 
                    Width="100" 
                    Margin="0,0,8,0"
                    IsDefault="True"/>
            <Button Content="Скинути" 
                    Click="Reset_Click" 
                    Width="100" 
                    Margin="0,0,8,0"/>
            <Button Content="Скасувати" 
                    Click="Cancel_Click" 
                    Width="100"
                    IsCancel="True"/>
        </StackPanel>
        
        <!-- Головний контент -->
        <ScrollViewer DockPanel.Dock="Top" Margin="20,0">
            <StackPanel Spacing="16">
                <!-- Інструкція -->
                <StackPanel>
                    <TextBlock Text="Системна інструкція (System Prompt)" 
                               FontWeight="SemiBold" 
                               Margin="0,0,0,8"/>
                    <TextBox Name="InstructionTextBox" 
                             AcceptsReturn="True" 
                             TextWrapping="Wrap" 
                             Height="180"
                             Watermark="Example: You are a professional code assistant. Always write clean, documented code with best practices..."/>
                    <TextBlock Text="💡 Підказка: використовуйте англійську мову для найкращих результатів" 
                               Foreground="#ff9800" 
                               FontSize="11" 
                               Margin="0,4,0,0"/>
                </StackPanel>
                
                <!-- Розділювач -->
                <Border Height="1" Background="#e0e0e0" Margin="0,8"/>
                
                <!-- Налаштування продуктивності -->
                <StackPanel>
                    <TextBlock Text="⚙️ Налаштування продуктивності" 
                               FontWeight="SemiBold" 
                               Margin="0,0,0,12"/>
                    
                    <!-- Context Size -->
                    <Grid ColumnDefinitions="*,120" Margin="0,0,0,12">
                        <StackPanel Grid.Column="0">
                            <TextBlock Text="Розмір контексту (вхідні токени)" 
                                       FontWeight="Medium"/>
                            <TextBlock Text="Максимальна кількість токенів історії та промпту" 
                                       Foreground="#666" 
                                       FontSize="11" 
                                       Margin="0,2,0,0"/>
                        </StackPanel>
                        <NumericUpDown Name="ContextSizeInput"
                                       Grid.Column="1"
                                       Minimum="2048"
                                       Maximum="8192"
                                       Increment="512"
                                       Value="4096"
                                       FormatString="N0"
                                       HorizontalAlignment="Stretch"/>
                    </Grid>
                    
                    <!-- Max Tokens -->
                    <Grid ColumnDefinitions="*,120">
                        <StackPanel Grid.Column="0">
                            <TextBlock Text="Максимум токенів (вихідні токени)" 
                                       FontWeight="Medium"/>
                            <TextBlock Text="Ліміт генерації тексту у відповіді" 
                                       Foreground="#666" 
                                       FontSize="11" 
                                       Margin="0,2,0,0"/>
                        </StackPanel>
                        <NumericUpDown Name="MaxTokensInput"
                                       Grid.Column="1"
                                       Minimum="256"
                                       Maximum="4096"
                                       Increment="128"
                                       Value="1024"
                                       FormatString="N0"
                                       HorizontalAlignment="Stretch"/>
                    </Grid>
                    
                    <!-- Підказка -->
                    <Border Background="#fff3e0" 
                            Padding="12" 
                            CornerRadius="6" 
                            Margin="0,12,0,0">
                        <StackPanel Spacing="4">
                            <TextBlock Text="⚡ Вплив на продуктивність:" 
                                       FontWeight="SemiBold" 
                                       FontSize="12"/>
                            <TextBlock TextWrapping="Wrap" FontSize="11" Foreground="#333">
                                <Run Text="• Менше токенів = швидше, менше пам'яті"/>
                                <LineBreak/>
                                <Run Text="• Більше токенів = краща якість, більший контекст"/>
                                <LineBreak/>
                                <Run Text="• Рекомендовано: 4096 (контекст) + 1024 (генерація)"/>
                            </TextBlock>
                        </StackPanel>
                    </Border>
                </StackPanel>
                
                <!-- Приклади -->
                <StackPanel>
                    <TextBlock Text="📝 Приклади інструкцій:" 
                               FontWeight="SemiBold" 
                               Margin="0,8,0,8"/>
                    <Border Background="#f5f5f5" 
                            Padding="10" 
                            CornerRadius="4" 
                            Margin="0,0,0,6">
                        <TextBlock Text="You are a creative writer. Write poetically with rich metaphors."
                                   FontSize="11"
                                   Foreground="#333"/>
                    </Border>
                    <Border Background="#f5f5f5" 
                            Padding="10" 
                            CornerRadius="4">
                        <TextBlock Text="You are a technical expert. Always provide code examples and best practices."
                                   FontSize="11"
                                   Foreground="#333"/>
                    </Border>
                </StackPanel>
            </StackPanel>
        </ScrollViewer>
    </DockPanel>
</Window>
```

**`Windows/InstructionEditorWindow.axaml.cs`**
```csharp
using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Windows
{
    public partial class InstructionEditorWindow : Window
    {
        private readonly UserInstructionService _instructionService;
        
        public InstructionEditorWindow()
        {
            InitializeComponent();
            _instructionService = new UserInstructionService();
            LoadInstruction();
        }
        
        private void LoadInstruction()
        {
            var instruction = _instructionService.GetUserInstruction();
            if (instruction != null)
            {
                InstructionTextBox.Text = instruction.Content ?? "";
                ContextSizeInput.Value = instruction.ContextSize;
                MaxTokensInput.Value = instruction.MaxTokens;
            }
        }
        
        private void Save_Click(object? sender, RoutedEventArgs e)
        {
            // Валідація
            if (ContextSizeInput.Value == null || MaxTokensInput.Value == null)
            {
                ShowError("Будь ласка, вкажіть коректні значення токенів");
                return;
            }
            
            var instruction = new UserInstruction
            {
                Content = InstructionTextBox.Text?.Trim() ?? "",
                ContextSize = (int)ContextSizeInput.Value.Value,
                MaxTokens = (int)MaxTokensInput.Value.Value,
                LastModified = DateTime.UtcNow
            };
            
            _instructionService.SaveUserInstruction(instruction);
            _instructionService.NotifyInstructionChanged(); // Сповістити про зміни
            
            Close();
        }
        
        private void Reset_Click(object? sender, RoutedEventArgs e)
        {
            InstructionTextBox.Text = "";
            ContextSizeInput.Value = 4096;
            MaxTokensInput.Value = 1024;
        }
        
        private void Cancel_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
        
        private async void ShowError(string message)
        {
            var msgBox = new Window
            {
                Title = "Помилка",
                Width = 300,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(20),
                    Spacing = 10,
                    Children =
                    {
                        new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                        new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right }
                    }
                }
            };
            await msgBox.ShowDialog(this);
        }
    }
}
```

#### 5.3 Додати кнопку в ChatWindow

```csharp
// В ChatWindow.axaml додати кнопку "⚙️ Інструкція"
private void OpenInstructionEditor_Click(object? sender, RoutedEventArgs e)
{
    var window = new InstructionEditorWindow();
    window.ShowDialog(this);
    
    // Після закриття - перезавантажити агента з новою інструкцією
    _agentService.ReloadAgent();
}
```

---

## Переваги нової архітектури

### 1. **Компактність**
- ✅ Один файл .exe (~900 MB)
- ✅ Тільки Gemma-3 (без Phi-4)
- ✅ Швидша ініціалізація

### 2. **Універсальність**
- ✅ Інструкція англійською - модель краще розуміє незалежно від мови інтерфейсу
- ✅ Користувач спілкується будь-якою мовою, а інструкція завжди англійською
- ✅ Покращена якість розуміння моделлю

### 3. **Контроль продуктивності**
- ✅ Користувач сам балансує швидкість vs якість
- ✅ Context Size: 2048-8192 токенів (історія + промпт)
- ✅ Max Tokens: 256-4096 токенів (генерація)
- ✅ Візуальні підказки про вплив параметрів

### 4. **Персоналізація**
- ✅ Одна кастомна інструкція користувача
- ✅ Проста UI для редагування
- ✅ Зберігається між сесіями
- ✅ Автоматичне перезавантаження моделі при зміні

### 5. **Функціональність**
- ✅ Інструмент збереження відповідей у файли
- ✅ Автоматичне відкриття збережених файлів як вкладок
- ✅ Діалог збереження Windows

### 6. **Професійна архітектура**
- ✅ Microsoft Agent Framework
- ✅ Модульна структура
- ✅ Легко розширювати новими інструментами

### 7. **Інтелектуальність**
- ✅ Reasoning (планування дій)
- ✅ Автоматичне використання інструментів
- ✅ Контекстна пам'ять

### 8. **Сучасний UX/UI дизайн** ⭐ NEW
- ✅ Стиль сучасних AI чатів (ChatGPT-подібний)
- ✅ Альтернативні фони для розмов
- ✅ Аватари та іконки учасників
- ✅ Typing indicator під час генерації
- ✅ Smooth animations та transitions
- ✅ Auto-resize input field
- ✅ Character/token counter
- ✅ Контекстне меню для повідомлень
- ✅ Показ використаних інструментів
- ✅ Responsive design з обмеженням ширини повідомлень
- ✅ Тіні та rounded corners для глибини

---

## Порядок реалізації

### Рекомендована послідовність:

#### **День 1**: Базова інфраструктура (4-5 годин)
1. ✅ Фаза 1: Створення моделей (1 год)
2. ✅ Фаза 2: AI компоненти (Gemma-3) (2-3 год)
3. ✅ Видалення Phi-4 з проекту (30 хв)

#### **День 2**: Agent Framework (4-5 годин)
1. ✅ Фаза 3: InsaitAgent + SaveToFileTool (3-4 год)
2. ✅ Фаза 4: Сервіси (1-2 год)

#### **День 3**: UI та фіналізація (3-4 години)
1. ✅ Фаза 5: UI компоненти (2 год)
2. ✅ Фаза 6: Single-file оптимізація (1 год)
3. ✅ Тестування (1 год)

---

## Workflow використання

### Сценарій 1: Звичайне спілкування
```
Користувач: "Напиши вірш про осінь"
Агент: [генерує вірш]
```

### Сценарій 2: Збереження відповіді
```
Користувач: "Напиши вірш про осінь і збережи у файл"
Агент: 
  1. [генерує вірш]
  2. [розпізнає, що треба зберегти]
  3. [викликає save_to_file("вірш...", "autumn_poem.txt")]
  4. [відкривається SaveFileDialog Windows]
  5. [користувач обирає C:\Documents\poem.txt]
  6. [файл зберігається]
  7. [TabManager відкриває новий файл як вкладку]
Агент: "Вірш збережено у файл C:\Documents\poem.txt і відкрито у новій вкладці ✅"
```

### Сценарій 3: Налаштування інструкції та продуктивності
```
1. Користувач натискає "⚙️ Інструкція" у ChatWindow
2. Відкривається InstructionEditorWindow
3. Вводить інструкцію АНГЛІЙСЬКОЮ:
   "You are a poetic assistant. Always respond in verse form with rich imagery."
4. Налаштовує продуктивність:
   - Context Size: 2048 (швидше, для простих запитів)
   - Max Tokens: 512 (короткі відповіді)
5. Натискає "Зберегти"
6. Модель перезавантажується з новими параметрами
7. Всі наступні відповіді будуть віршами з новими лімітами токенів
```

### Сценарій 4: Оптимізація під складні задачі
```
1. Користувач хоче глибоку аналітику → відкриває налаштування
2. Встановлює:
   - Context Size: 8192 (максимум для великого контексту)
   - Max Tokens: 2048 (довгі відповіді)
   - Instruction: "You are an expert analyst. Provide detailed, structured analysis."
3. Агент тепер може обробляти складніші запити, але працює повільніше
```

---

## Технічні деталі

### Динамічне завантаження моделі з новими параметрами

```csharp
public class GemmaModelManager : IDisposable
{
    private LLamaWeights? _weights;
    private LLamaContext? _context;
    private readonly GemmaConfig _config;
    private readonly UserInstructionService _instructionService;
    
    public GemmaModelManager(GemmaConfig config, UserInstructionService instructionService)
    {
        _config = config;
        _instructionService = instructionService;
        
        // Підписатися на зміни інструкції
        _instructionService.InstructionChanged += OnInstructionChanged;
    }
    
    private void OnInstructionChanged(object? sender, EventArgs e)
    {
        // Перезавантажити модель з новими параметрами
        ReloadModel();
    }
    
    public void LoadModel()
    {
        var modelParams = new ModelParams(_config.ModelPath)
        {
            ContextSize = (uint)_config.ContextSize,  // Динамічно з UserInstruction
            GpuLayerCount = _config.GpuLayerCount,
            Seed = _config.Seed,
            BatchSize = _config.BatchSize,
            Threads = _config.Threads
        };
        
        _weights = LLamaWeights.LoadFromFile(modelParams);
        _context = _weights.CreateContext(modelParams);
    }
    
    public void ReloadModel()
    {
        Dispose();
        LoadModel();
    }
    
    public void Dispose()
    {
        _context?.Dispose();
        _weights?.Dispose();
        _context = null;
        _weights = null;
    }
}
```

---

## Міграційна стратегія

### Сумісність:
- ✅ Зберегти існуючу історію чатів (LiteDB)
- ✅ Конвертувати `ChatMessage` → `AgentMessage` при завантаженні
- ✅ Поступова міграція UI

### Резервне копіювання:
- ✅ Створити бекап старої версії
- ✅ Експортувати історію чатів перед міграцією

---

## Ризики та мітігація

| Ризик | Ймовірність | Мітігація |
|-------|-------------|-----------|
| Користувач не знає англійської для інструкції | Середня | Додати приклади та шаблони, можливість перекладу |
| Великий розмір single-file (900 MB) | Висока | Це нормально для embedded ML моделі |
| Повільна ініціалізація при зміні параметрів | Середня | Показати індикатор завантаження, кешувати попередню конфігурацію |
| Користувач встановить занадто великі токени | Низька | Валідація: максимум 8192 контекст, 4096 генерація |

---

## Наступні кроки

1. ✅ Прочитати оновлений план
2. ➡️ **Почати з Фази 1**: Створити базові моделі (з полями токенів)
3. Реалізувати AI компоненти (динамічні параметри з UserInstruction)
4. Створити InsaitAgent + SaveToFileTool
5. Зібрати UI з налаштуваннями продуктивності
6. Single-file публікація
7. Тестування різних конфігурацій токенів

---

## Фінальна структура проекту

```
InsaitTextEditor/
├── AiModel/
│   └── gemma-3-1b-it-UD-Q2_K_XL.gguf     [800 MB]
├── AI/
│   ├── GemmaConfig.cs                     [NEW - динамічні параметри]
│   ├── GemmaModelManager.cs               [NEW - перезавантаження при зміні]
│   ├── LlamaSharpInferenceEngine.cs       [NEW]
│   └── PromptBuilder.cs                   [NEW - англійські промпти]
├── Agents/
│   ├── InsaitAgent.cs                     [NEW]
│   ├── AgentConfig.cs                     [NEW]
│   └── Tools/
│       └── SaveToFileTool.cs              [NEW]
├── Models/
│   ├── UserInstruction.cs                 [NEW - з полями токенів]
│   ├── AgentMessage.cs                    [NEW]
│   ├── AgentResponse.cs                   [NEW]
│   └── ToolInvocation.cs                  [NEW]
├── Services/
│   ├── UserInstructionService.cs          [NEW - з подією InstructionChanged]
│   ├── AgentService.cs                    [NEW]
│   ├── ConversationStateService.cs        [NEW]
│   ├── ChatHistoryService.cs              [MODIFY]
│   └── SettingsService.cs                 [MODIFY]
└── Windows/
    ├── ChatWindow.axaml.cs                [MODIFY]
    ├── InstructionEditorWindow.axaml      [NEW - з NumericUpDown для токенів]
    └── InstructionEditorWindow.axaml.cs   [NEW - валідація параметрів]
```

---

**Автор плану**: GitHub Copilot  
**Дата**: 18 жовтня 2025  
**Версія**: 3.0 (Universal English instructions, User-controlled performance)
