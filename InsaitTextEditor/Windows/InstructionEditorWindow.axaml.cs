using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using InsaitTextEditor.Models;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Windows;

public partial class InstructionEditorWindow : Window
{
    private readonly UserInstructionService _instructionService;
    
    public InstructionEditorWindow()
    {
        InitializeComponent();
        _instructionService = new UserInstructionService(App.DatabaseService);
        LoadExistingInstruction();
    }

    public InstructionEditorWindow(UserInstructionService instructionService)
    {
        InitializeComponent();
        _instructionService = instructionService;
        LoadExistingInstruction();
    }

    private void LoadExistingInstruction()
    {
        var instruction = _instructionService.GetUserInstruction();
        
        // Get control references once
        var textBox = this.FindControl<TextBox>("InstructionTextBox");
        var contextSize = this.FindControl<NumericUpDown>("ContextSizeInput");
        var maxTokens = this.FindControl<NumericUpDown>("MaxTokensInput");
        var aiLangCombo = this.FindControl<ComboBox>("AILanguageComboBox");
        var reasoningToggle = this.FindControl<ToggleSwitch>("ReasoningModeToggle");
        var memoryToggle = this.FindControl<ToggleSwitch>("GlobalMemoryToggle");
        
        if (instruction != null)
        {
            if (textBox != null)
                textBox.Text = instruction.Content;
            
            if (contextSize != null)
                contextSize.Value = instruction.ContextSize;
            
            if (maxTokens != null)
                maxTokens.Value = instruction.MaxTokens;
            
            // Load selected AI language
            if (aiLangCombo != null)
            {
                var aiLang = instruction.AiLanguage ?? "";
                for (int i = 0; i < aiLangCombo.Items.Count; i++)
                {
                    if (aiLangCombo.Items[i] is ComboBoxItem item && 
                        item.Tag?.ToString() == aiLang)
                    {
                        aiLangCombo.SelectedIndex = i;
                        break;
                    }
                }
            }
            
            // Load Reasoning Mode and Global Memory state
            if (reasoningToggle != null)
                reasoningToggle.IsChecked = instruction.ReasoningEnabled;
                
            if (memoryToggle != null)
                memoryToggle.IsChecked = instruction.GlobalMemoryEnabled;
        }
        
        // Subscribe to Global Memory toggle changes to display statistics
        if (memoryToggle != null)
        {
            memoryToggle.IsCheckedChanged += OnGlobalMemoryToggleChanged;
        }
        
        // Update memory statistics when window opens
        _ = UpdateMemoryStats();
    }

    private async void OnGlobalMemoryToggleChanged(object? sender, RoutedEventArgs e)
    {
        await UpdateMemoryStats();
    }

    private async System.Threading.Tasks.Task UpdateMemoryStats()
    {
        var memoryToggle = this.FindControl<ToggleSwitch>("GlobalMemoryToggle");
        var statsPanel = this.FindControl<Border>("MemoryStatsPanel");
        var statsText = this.FindControl<TextBlock>("MemoryStatsText");
        
        if (memoryToggle?.IsChecked == true && statsPanel != null && statsText != null)
        {
            statsPanel.IsVisible = true;
            
            try
            {
                // Get statistics from MemoryService
                var memoryService = new Services.Memory.MemoryService(
                    App.MemoryDb, 
                    App.InferenceEngine);
                
                var factsCount = await memoryService.GetTotalFactsCountAsync();
                var lastUpdate = DateTime.Now.ToString("HH:mm");
                
                statsText.Text = $"Saved facts: {factsCount} | Last update: {lastUpdate}";
            }
            catch
            {
                statsText.Text = "Memory stats unavailable";
            }
        }
        else if (statsPanel != null)
        {
            statsPanel.IsVisible = false;
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var textBox = this.FindControl<TextBox>("InstructionTextBox");
        var contextSize = this.FindControl<NumericUpDown>("ContextSizeInput");
        var maxTokens = this.FindControl<NumericUpDown>("MaxTokensInput");
        var aiLangCombo = this.FindControl<ComboBox>("AILanguageComboBox");
        var reasoningToggle = this.FindControl<ToggleSwitch>("ReasoningModeToggle");
        var memoryToggle = this.FindControl<ToggleSwitch>("GlobalMemoryToggle");

        // Get selected AI language
        string? selectedAiLang = null;
        if (aiLangCombo?.SelectedItem is ComboBoxItem selectedItem)
        {
            var tag = selectedItem.Tag?.ToString();
            selectedAiLang = string.IsNullOrEmpty(tag) ? null : tag;
        }

        var instruction = new UserInstruction
        {
            Content = textBox?.Text ?? string.Empty,
            ContextSize = (int)(contextSize?.Value ?? 4096),
            MaxTokens = (int)(maxTokens?.Value ?? 1024),
            AiLanguage = selectedAiLang,
            ReasoningEnabled = reasoningToggle?.IsChecked ?? false,
            GlobalMemoryEnabled = memoryToggle?.IsChecked ?? false,
            LastModified = DateTime.UtcNow
        };

        _instructionService.SaveUserInstruction(instruction);
        Close();
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TitleBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void DragSurface_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;
        if (e.Source is Visual v)
        {
            if (v is Button || v is TextBox || v is NumericUpDown || v is ComboBox || 
                v.FindAncestorOfType<Button>() is not null ||
                v.FindAncestorOfType<TextBox>() is not null ||
                v.FindAncestorOfType<NumericUpDown>() is not null ||
                v.FindAncestorOfType<ComboBox>() is not null)
                return;
        }
        BeginMoveDrag(e);
    }

    private void Minimize_Click(object? sender, RoutedEventArgs e)
        => WindowState = WindowState.Minimized;

    private void Close_Click(object? sender, RoutedEventArgs e)
        => Close();
}