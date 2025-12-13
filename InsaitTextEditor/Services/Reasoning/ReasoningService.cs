using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using InsaitTextEditor.Models.Reasoning;
using InsaitTextEditor.Models;
using InsaitTextEditor.AI;
using InsaitTextEditor.Agents;
using InsaitTextEditor.Services.Database.Specialized;

namespace InsaitTextEditor.Services.Reasoning;

/// <summary>
/// Сервіс для виконання reasoning chains (Chain-of-Thought)
/// </summary>
public class ReasoningService
{
    private readonly MicrosoftInsaitAgent _agent;
    private readonly ReasoningPromptBuilder _promptBuilder;
    private readonly ReasoningDatabaseService _reasoningDb;
    private bool _isInitialized = false;
    private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);

    public ReasoningService(
        MicrosoftInsaitAgent agent,
        ReasoningDatabaseService reasoningDb,
        UserInstructionService? instructionService = null)
    {
        _agent = agent ?? throw new ArgumentNullException(nameof(agent));
        _promptBuilder = new ReasoningPromptBuilder(instructionService);
        _reasoningDb = reasoningDb ?? throw new ArgumentNullException(nameof(reasoningDb));
    }

    /// <summary>
    /// Забезпечує що база даних ініціалізована перед використанням
    /// </summary>
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

    /// <summary>
    /// Генерує reasoning chain для запиту з streaming відображенням
    /// </summary>
    public async IAsyncEnumerable<ReasoningStreamEvent> GenerateReasoningChainStreamAsync(
        string userQuery, 
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ✅ Ініціалізувати БД перед використанням
        await EnsureInitializedAsync();
        
        var chain = new ReasoningChain
        {
            UserQuery = userQuery,
            ConversationId = conversationId,
            Status = ChainStatus.InProgress
        };

        Console.WriteLine($"[ReasoningService] 🧠 Початок reasoning для: {userQuery}");
        
        // Обгортка для обробки помилок без try-catch в генераторі
        var enumerator = GenerateReasoningChainStreamInternalAsync(userQuery, conversationId, chain, cancellationToken);
        
        await foreach (var evt in enumerator)
        {
            yield return evt;
        }
    }

    private async IAsyncEnumerable<ReasoningStreamEvent> GenerateReasoningChainStreamInternalAsync(
        string userQuery,
        Guid conversationId,
        ReasoningChain chain,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // === Крок 1: Генерація плану зі STREAMING та ФІЛЬТРАЦІЄЮ ===
        // ✅ Використовуємо локалізацію для початкового повідомлення
        yield return new ReasoningStreamEvent 
        { 
            Type = ReasoningEventType.StatusUpdate, 
            Message = Services.LocalizationService.GetString("Key.ReasoningGeneratingPlan", "📝 Генерую план міркування...")
        };
        
        var planPrompt = _promptBuilder.BuildPlanningPrompt(userQuery);
        var planBuilder = new System.Text.StringBuilder();
        var planHasError = false;
        var tokenCount = 0;
        var lastUpdateTime = DateTime.UtcNow;
        
        Console.WriteLine("[ReasoningService] 🔄 Початок streaming генерації плану з фільтрацією...");
        
        // ✅ ВИКОРИСТОВУЄМО ФІЛЬТРОВАНИЙ STREAMING з агента
        await foreach (var token in _agent.GenerateReasoningStepStreamAsync(planPrompt, maxTokens: 384, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                planHasError = true;
                Console.WriteLine("[ReasoningService] ⚠️ План скасовано користувачем");
                break;
            }
            
            // 🛡️ ФІЛЬТРАЦІЯ: Перевіряємо чи токен не містить технічних символів
            if (ContainsTechnicalTokens(token))
            {
                Console.WriteLine("[ReasoningService] 🚫 Виявлено технічний токен у плані, зупинка");
                break;
            }
            
            planBuilder.Append(token);
            tokenCount++;
            
            // Відправляти прогрес кожні 5 токенів або кожні 100ms, щоб не спамити UI
            if (tokenCount % 5 == 0 || (DateTime.UtcNow - lastUpdateTime).TotalMilliseconds > 100)
            {
                // ✅ Використовуємо локалізацію
                var progressMsg = string.Format(
                    Services.LocalizationService.GetString("Key.ReasoningGeneratingPlanProgress", "📝 Генерую план... ({0} токенів)"),
                    tokenCount
                );
                yield return new ReasoningStreamEvent
                {
                    Type = ReasoningEventType.StatusUpdate,
                    Message = progressMsg
                };
                lastUpdateTime = DateTime.UtcNow;
            }
        }
        
        if (planHasError)
        {
            chain.Status = ChainStatus.Failed;
            await _reasoningDb.SaveChainAsync(chain);
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.Error,
                Message = Services.LocalizationService.GetString("Key.ReasoningPlanCancelled", "❌ Генерацію плану скасовано")
            };
            yield break;
        }
        
        var planResponse = planBuilder.ToString();
        
        if (string.IsNullOrWhiteSpace(planResponse))
        {
            Console.WriteLine("[ReasoningService] ❌ План порожній");
            chain.Status = ChainStatus.Failed;
            await _reasoningDb.SaveChainAsync(chain);
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.Error,
                Message = Services.LocalizationService.GetString("Key.ReasoningPlanFailed", "❌ Не вдалося згенерувати план")
            };
            yield break;
        }
        
        Console.WriteLine($"[ReasoningService] ✅ План згенеровано: {planResponse.Length} символів");
        
        var steps = _promptBuilder.ParsePlanIntoSteps(planResponse);
        chain.Steps = steps;
        
        // ✅ Використовуємо локалізацію
        var planReadyMsg = string.Format(
            Services.LocalizationService.GetString("Key.ReasoningPlanReady", "✅ План готовий: {0} кроків"),
            steps.Count
        );
        yield return new ReasoningStreamEvent 
        { 
            Type = ReasoningEventType.StatusUpdate, 
            Message = planReadyMsg
        };

        await _reasoningDb.SaveChainAsync(chain);
        
        // Невелика пауза для читабельності
        await Task.Delay(500, cancellationToken);

        // === Крок 2: Виконання кожного кроку з фільтрованим streaming ===
        int stepNum = 0;
        foreach (var step in chain.Steps)
        {
            stepNum++;
            step.Status = StepStatus.Running;
            step.StartedAt = DateTime.UtcNow;
            
            Console.WriteLine($"[ReasoningService] 🔄 Початок кроку {stepNum}/{steps.Count}: {step.Title}");
            
            // Повідомити про початок кроку
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.StepStart,
                StepNumber = stepNum,
                StepTitle = step.Title,
                Message = $"🔄 Крок {stepNum}/{steps.Count}: {step.Title}"
            };

            // ✅ ГЕНЕРУВАТИ ВІДПОВІДЬ ДЛЯ КРОКУ З ФІЛЬТРАЦІЄЮ
            var stepPrompt = _promptBuilder.BuildStepPrompt(userQuery, step, chain.Steps);
            
            var stepContentBuilder = new System.Text.StringBuilder();
            var hasError = false;
            var stepTokenCount = 0;
            var stepLastUpdate = DateTime.UtcNow;
            
            await foreach (var token in _agent.GenerateReasoningStepStreamAsync(stepPrompt, maxTokens: 512, cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    hasError = true;
                    Console.WriteLine($"[ReasoningService] ⚠️ Крок {stepNum} скасовано");
                    break;
                }
                
                // 🛡️ ФІЛЬТРАЦІЯ: Перевіряємо чи токен не містить технічних символів
                if (ContainsTechnicalTokens(token))
                {
                    Console.WriteLine($"[ReasoningService] 🚫 Виявлено технічний токен у кроці {stepNum}, зупинка");
                    hasError = true;
                    break;
                }
                
                stepContentBuilder.Append(token);
                stepTokenCount++;
                
                // Відправляти кожен токен для плавного відображення (throttling кожні 50ms)
                if ((DateTime.UtcNow - stepLastUpdate).TotalMilliseconds >= 50)
                {
                    yield return new ReasoningStreamEvent
                    {
                        Type = ReasoningEventType.StepContent,
                        StepNumber = stepNum,
                        Content = token
                    };
                    stepLastUpdate = DateTime.UtcNow;
                }
                else
                {
                    // Все одно відправляємо, але без затримки
                    yield return new ReasoningStreamEvent
                    {
                        Type = ReasoningEventType.StepContent,
                        StepNumber = stepNum,
                        Content = token
                    };
                }
            }
            
            if (hasError)
            {
                yield break;
            }

            step.Content = stepContentBuilder.ToString();
            step.Status = StepStatus.Completed;
            step.CompletedAt = DateTime.UtcNow;
            await _reasoningDb.UpdateChainAsync(chain);
            
            Console.WriteLine($"[ReasoningService] ✅ Крок {stepNum} завершено: {stepTokenCount} токенів");
            
            // Повідомити про завершення кроку
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.StepComplete,
                StepNumber = stepNum,
                Message = $"✅ Крок {stepNum} завершено"
            };
            
            // Невелика пауза між кроками для читабельності
            await Task.Delay(300, cancellationToken);
        }

        // === Крок 3: Генерація фінальної відповіді з ФІЛЬТРАЦІЄЮ ===
        // ✅ Використовуємо локалізацію
        yield return new ReasoningStreamEvent 
        { 
            Type = ReasoningEventType.StatusUpdate, 
            Message = Services.LocalizationService.GetString("Key.ReasoningGeneratingFinalAnswer", "🎯 Генерую фінальну відповідь...")
        };
        
        Console.WriteLine("[ReasoningService] 🎯 Початок генерації фінальної відповіді з фільтрацією...");
        
        var finalPrompt = _promptBuilder.BuildFinalAnswerPrompt(userQuery, chain.Steps);
        
        var finalContentBuilder = new System.Text.StringBuilder();
        var finalHasError = false;
        var finalTokenCount = 0;
        var finalLastUpdate = DateTime.UtcNow;
        
        await foreach (var token in _agent.GenerateReasoningStepStreamAsync(finalPrompt, maxTokens: 768, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                finalHasError = true;
                Console.WriteLine("[ReasoningService] ⚠️ Фінальна відповідь скасована");
                break;
            }
            
            // 🛡️ ФІЛЬТРАЦІЯ: Перевіряємо чи токен не містить технічних символів
            if (ContainsTechnicalTokens(token))
            {
                Console.WriteLine("[ReasoningService] 🚫 Виявлено технічний токен у фінальній відповіді, зупинка");
                finalHasError = true;
                break;
            }
            
            finalContentBuilder.Append(token);
            finalTokenCount++;
            
            // Відправляти кожен токен (throttling кожні 50ms)
            if ((DateTime.UtcNow - finalLastUpdate).TotalMilliseconds >= 50)
            {
                yield return new ReasoningStreamEvent
                {
                    Type = ReasoningEventType.FinalAnswerContent,
                    Content = token
                };
                finalLastUpdate = DateTime.UtcNow;
            }
            else
            {
                yield return new ReasoningStreamEvent
                {
                    Type = ReasoningEventType.FinalAnswerContent,
                    Content = token
                };
            }
        }
        
        if (finalHasError)
        {
            yield break;
        }
        
        chain.FinalAnswer = finalContentBuilder.ToString();
        chain.Status = ChainStatus.Completed;
        chain.CompletedAt = DateTime.UtcNow;
        await _reasoningDb.SaveChainAsync(chain);

        Console.WriteLine($"[ReasoningService] ✅ Фінальна відповідь: {finalTokenCount} токенів");

        yield return new ReasoningStreamEvent
        {
            Type = ReasoningEventType.Complete,
            Message = Services.LocalizationService.GetString("Key.ReasoningComplete", "🎉 Reasoning завершено!"),
            Chain = chain
        };

        Console.WriteLine($"[ReasoningService] 🎉 Reasoning завершено успішно!");
    }

    /// <summary>
    /// Виконати async операцію безпечно без try-catch в генераторі
    /// </summary>
    private async Task<T?> ExecuteSafeAsync<T>(Func<Task<T>> action, Action<Exception> onError) where T : class
    {
        try
        {
            return await action();
        }
        catch (Exception ex)
        {
            onError(ex);
            return null;
        }
    }

    /// <summary>
    /// Отримати історію reasoning chains для розмови
    /// </summary>
    public Task<List<ReasoningChain>> GetChainsForConversationAsync(Guid conversationId)
    {
        return _reasoningDb.GetChainsByConversationAsync(conversationId);
    }

    /// <summary>
    /// Отримати конкретний reasoning chain
    /// </summary>
    public Task<ReasoningChain?> GetChainByIdAsync(Guid chainId)
    {
        return _reasoningDb.GetChainByIdAsync(chainId);
    }

    /// <summary>
    /// Перевірка чи текст містить технічні токени (для фільтрації)
    /// </summary>
    private bool ContainsTechnicalTokens(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
            
        var patterns = new[] 
        { 
            "<end_of_turn>", 
            "<start_of_turn>", 
            "<eos>", 
            "<bos>",
            "<pad>",
            "<|", 
            "|>"
        };
        
        return patterns.Any(p => text.Contains(p, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Події reasoning stream для відображення прогресу
/// </summary>
public class ReasoningStreamEvent
{
    public ReasoningEventType Type { get; set; }
    public int StepNumber { get; set; }
    public string? StepTitle { get; set; }
    public string? Content { get; set; }
    public string? Message { get; set; }
    public ReasoningChain? Chain { get; set; }
}

public enum ReasoningEventType
{
    StatusUpdate,      // Загальне повідомлення про статус
    StepStart,         // Початок нового кроку
    StepContent,       // Контент кроку (streaming токени)
    StepComplete,      // Завершення кроку
    FinalAnswerContent,// Контент фінальної відповіді (streaming)
    Complete,          // Все завершено
    Error             // Помилка
}
