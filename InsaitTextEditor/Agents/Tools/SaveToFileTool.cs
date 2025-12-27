using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using InsaitTextEditor.Services;

namespace InsaitTextEditor.Agents.Tools;

/// <summary>
/// Tool for saving the assistant response to a text file.
/// </summary>
public class SaveToFileTool
{
    private readonly TabManager _tabManager;
    private readonly Window? _ownerWindow;

    public string Name => "save_to_file";

    public string Description =>
        "Saves the assistant's response to a text file. Opens Windows Save File Dialog, " +
        "saves the content, and opens the file as a new tab in the editor. " +
        "Parameters: content (string, required), suggestedFileName (string, optional, default: 'response.txt')";

    public SaveToFileTool(TabManager tabManager, Window? ownerWindow = null)
    {
        _tabManager = tabManager ?? throw new ArgumentNullException(nameof(tabManager));
        _ownerWindow = ownerWindow;
    }

    /// <summary>
    /// Auto-invoke after agent generation (without blocking the UI).
    /// </summary>
    public async Task<ToolResult> AutoSaveAsync(
        string content, 
        string? suggestedFileName = null,
        bool openInEditor = true)
    {
        var (extension, fileType) = DetectContentType(content);
        
        if (suggestedFileName == null)
        {
            suggestedFileName = $"{fileType}_{DateTime.Now:yyyyMMdd_HHmmss}{extension}";
        }
        else if (!suggestedFileName.Contains('.'))
        {
            // Add extension if missing
            suggestedFileName += extension;
        }
        
        return await ExecuteAsync(content, suggestedFileName);
    }

    /// <summary>
    /// Detect content type and suggest an extension.
    /// </summary>
    private (string extension, string fileType) DetectContentType(string content)
    {
        // Check for a poem (multiple lines, rhyming)
        if (IsPoem(content))
            return (".txt", "Poem");
        
        // Check for markdown
        if (content.Contains("##") || content.Contains("**") || content.Contains("```"))
            return (".md", "Markdown");
        
        // Check for code
        if (content.Contains("function") || content.Contains("class ") || content.Contains("public "))
            return (".txt", "Code");
        
        // Default
        return (".txt", "Text");
    }

    /// <summary>
    /// Check whether this is a poem.
    /// </summary>
    private bool IsPoem(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        
        // A poem usually has 4+ lines
        if (lines.Length < 4) 
            return false;
        
        // Short lines (typical for poems)
        var avgLength = lines.Average(l => l.Length);
        if (avgLength > 80) 
            return false;
        
        // Poems often have medium-length lines (20-60 characters)
        if (avgLength < 15)
            return false;
        
        // Check punctuation at line endings (typical for poems)
        var linesWithPunctuation = lines.Count(l => 
            l.EndsWith(',') || l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || 
            l.EndsWith(':') || l.EndsWith(';') || l.EndsWith("..."));
        
        // If >30% of lines end with punctuation, it's likely a poem
        var punctuationRatio = (double)linesWithPunctuation / lines.Length;
        if (punctuationRatio > 0.3)
            return true;
        
        return false;
    }

    /// <summary>
    /// Execute file save.
    /// </summary>
    public async Task<ToolResult> ExecuteAsync(string content, string? suggestedFileName = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new ToolResult
                {
                    Success = false,
                    Message = "Content cannot be empty",
                    FilePath = null
                };
            }

            // Use suggested name or default
            var fileName = string.IsNullOrWhiteSpace(suggestedFileName)
                ? "response.txt"
                : suggestedFileName;

            // Open save file dialog
            var filePath = await ShowSaveFileDialogAsync(fileName);

            if (string.IsNullOrEmpty(filePath))
            {
                return new ToolResult
                {
                    Success = false,
                    Message = "User cancelled save operation",
                    FilePath = null
                };
            }

            // Save content to file
            await File.WriteAllTextAsync(filePath, content);

            // Open the file as a new tab
            await _tabManager.OpenFileAsync(filePath);

            return new ToolResult
            {
                Success = true,
                Message = $"Successfully saved to {Path.GetFileName(filePath)} and opened as new tab",
                FilePath = filePath
            };
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Error saving file: {ex.Message}",
                FilePath = null
            };
        }
    }

    /// <summary>
    /// Show the Windows Save File Dialog.
    /// </summary>
    private async Task<string?> ShowSaveFileDialogAsync(string suggestedFileName)
    {
        if (_ownerWindow?.StorageProvider == null)
        {
            // Fallback: save to a temp directory
            var tempPath = Path.Combine(Path.GetTempPath(), suggestedFileName);
            return tempPath;
        }

        var fileTypeChoices = new FilePickerFileType[]
        {
            new("Text Files") { Patterns = new[] { "*.txt" } },
            new("Markdown Files") { Patterns = new[] { "*.md" } },
            new("All Files") { Patterns = new[] { "*.*" } }
        };

        var options = new FilePickerSaveOptions
        {
            Title = "Save Response",
            SuggestedFileName = suggestedFileName,
            FileTypeChoices = fileTypeChoices,
            DefaultExtension = "txt",
            ShowOverwritePrompt = true
        };

        var result = await _ownerWindow.StorageProvider.SaveFilePickerAsync(options);

        return result?.Path.LocalPath;
    }

    /// <summary>
    /// Parse parameters from a JSON string.
    /// </summary>
    public async Task<ToolResult> ExecuteFromJsonAsync(string parametersJson)
    {
        try
        {
            var parameters = JsonSerializer.Deserialize<SaveToFileParameters>(parametersJson);

            if (parameters == null)
            {
                return new ToolResult
                {
                    Success = false,
                    Message = "Failed to parse parameters",
                    FilePath = null
                };
            }

            return await ExecuteAsync(parameters.Content, parameters.SuggestedFileName);
        }
        catch (Exception ex)
        {
            return new ToolResult
            {
                Success = false,
                Message = $"Error parsing parameters: {ex.Message}",
                FilePath = null
            };
        }
    }
}

/// <summary>
/// Parameters for SaveToFileTool.
/// </summary>
public class SaveToFileParameters
{
    public string Content { get; set; } = string.Empty;
    public string? SuggestedFileName { get; set; }
}

/// <summary>
/// Tool execution result.
/// </summary>
public class ToolResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}