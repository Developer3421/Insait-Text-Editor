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
/// Інструмент для збереження відповіді асистента у текстовий файл
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
    /// Автоматичний виклик після генерації агентом (без UI блокування)
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
            // Додати розширення якщо його немає
            suggestedFileName += extension;
        }
        
        return await ExecuteAsync(content, suggestedFileName);
    }

    /// <summary>
    /// Визначити тип контенту та запропонувати розширення
    /// </summary>
    private (string extension, string fileType) DetectContentType(string content)
    {
        // Перевірка на вірш (кілька рядків, римування)
        if (IsPoem(content))
            return (".txt", "Poem");
        
        // Перевірка на markdown
        if (content.Contains("##") || content.Contains("**") || content.Contains("```"))
            return (".md", "Markdown");
        
        // Перевірка на код
        if (content.Contains("function") || content.Contains("class ") || content.Contains("public "))
            return (".txt", "Code");
        
        // За замовчуванням
        return (".txt", "Text");
    }

    /// <summary>
    /// Перевірити чи це вірш
    /// </summary>
    private bool IsPoem(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        
        // Вірш зазвичай має 4+ рядки
        if (lines.Length < 4) 
            return false;
        
        // Короткі рядки (характерно для віршів)
        var avgLength = lines.Average(l => l.Length);
        if (avgLength > 80) 
            return false;
        
        // Вірші часто мають рядки середньої довжини (20-60 символів)
        if (avgLength < 15)
            return false;
        
        // Перевірка на наявність розділових знаків в кінці рядків (характерно для віршів)
        var linesWithPunctuation = lines.Count(l => 
            l.EndsWith(',') || l.EndsWith('.') || l.EndsWith('!') || l.EndsWith('?') || 
            l.EndsWith(':') || l.EndsWith(';') || l.EndsWith("..."));
        
        // Якщо більше 30% рядків мають розділові знаки - ймовірно вірш
        var punctuationRatio = (double)linesWithPunctuation / lines.Length;
        if (punctuationRatio > 0.3)
            return true;
        
        return false;
    }

    /// <summary>
    /// Виконати збереження файлу
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

            // Використати пропоновану назву або дефолтну
            var fileName = string.IsNullOrWhiteSpace(suggestedFileName)
                ? "response.txt"
                : suggestedFileName;

            // Відкрити діалог збереження файлу
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

            // Зберегти контент у файл
            await File.WriteAllTextAsync(filePath, content);

            // Відкрити файл як нову вкладку
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
    /// Показати Windows Save File Dialog
    /// </summary>
    private async Task<string?> ShowSaveFileDialogAsync(string suggestedFileName)
    {
        if (_ownerWindow?.StorageProvider == null)
        {
            // Fallback: зберегти у тимчасову директорію
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
    /// Парсити параметри з JSON строки
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
/// Параметри для SaveToFileTool
/// </summary>
public class SaveToFileParameters
{
    public string Content { get; set; } = string.Empty;
    public string? SuggestedFileName { get; set; }
}

/// <summary>
/// Результат виконання інструменту
/// </summary>
public class ToolResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}