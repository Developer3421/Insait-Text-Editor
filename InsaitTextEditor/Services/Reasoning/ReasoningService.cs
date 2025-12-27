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
/// Service for executing reasoning chains (Chain-of-Thought)
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
    /// Ensure the database is initialized before use
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
    /// Generate a reasoning chain for a query with streaming output
    /// </summary>
    public async IAsyncEnumerable<ReasoningStreamEvent> GenerateReasoningChainStreamAsync(
        string userQuery, 
        Guid conversationId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // ✅ Initialize DB before use
        await EnsureInitializedAsync();
        
        var chain = new ReasoningChain
        {
            UserQuery = userQuery,
            ConversationId = conversationId,
            Status = ChainStatus.InProgress
        };

        Console.WriteLine($"[ReasoningService] 🧠 Starting reasoning for: {userQuery}");
        
        // Wrapper to handle streaming without try/catch in the generator
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
        // === Step 1: Generate plan with STREAMING and FILTERING ===
        // ✅ Use localization for initial messages
        yield return new ReasoningStreamEvent 
        { 
            Type = ReasoningEventType.StatusUpdate, 
            Message = Services.LocalizationService.GetString("Key.ReasoningGeneratingPlan", "📝 Generating reasoning plan...")
        };
        
        var planPrompt = _promptBuilder.BuildPlanningPrompt(userQuery);
        var planBuilder = new System.Text.StringBuilder();
        var planHasError = false;
        var tokenCount = 0;
        var lastUpdateTime = DateTime.UtcNow;
        
        Console.WriteLine("[ReasoningService] 🔄 Starting streaming plan generation with filtering...");
        
        // ✅ USE FILTERED STREAMING from the agent
        await foreach (var token in _agent.GenerateReasoningStepStreamAsync(planPrompt, maxTokens: 384, cancellationToken))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                planHasError = true;
                Console.WriteLine("[ReasoningService] ⚠️ Plan cancelled by user");
                break;
            }
            
            // 🛡️ FILTERING: ensure token does not contain technical symbols
            if (ContainsTechnicalTokens(token))
            {
                Console.WriteLine("[ReasoningService] 🚫 Detected technical token in plan, stopping");
                break;
            }
            
            planBuilder.Append(token);
            tokenCount++;
            
            // Send progress every 5 tokens or every 100ms to avoid spamming UI
            if (tokenCount % 5 == 0 || (DateTime.UtcNow - lastUpdateTime).TotalMilliseconds > 100)
            {
                // ✅ Use localization
                var progressMsg = string.Format(
                    Services.LocalizationService.GetString("Key.ReasoningGeneratingPlanProgress", "📝 Generating plan... ({0} tokens)"),
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
                Message = Services.LocalizationService.GetString("Key.ReasoningPlanCancelled", "❌ Plan generation cancelled")
            };
            yield break;
        }
        
        var planResponse = planBuilder.ToString();
        
        if (string.IsNullOrWhiteSpace(planResponse))
        {
            Console.WriteLine("[ReasoningService] ❌ Plan is empty");
            chain.Status = ChainStatus.Failed;
            await _reasoningDb.SaveChainAsync(chain);
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.Error,
                Message = Services.LocalizationService.GetString("Key.ReasoningPlanFailed", "❌ Failed to generate plan")
            };
            yield break;
        }
        
        Console.WriteLine($"[ReasoningService] ✅ Plan generated: {planResponse.Length} chars");
        
        var steps = _promptBuilder.ParsePlanIntoSteps(planResponse);
        chain.Steps = steps;
        
        // ✅ Use localization
        var planReadyMsg = string.Format(
            Services.LocalizationService.GetString("Key.ReasoningPlanReady", "✅ Plan ready: {0} steps"),
            steps.Count
        );
        yield return new ReasoningStreamEvent 
        { 
            Type = ReasoningEventType.StatusUpdate, 
            Message = planReadyMsg
        };

        await _reasoningDb.SaveChainAsync(chain);
        
        // Small pause for readability
        await Task.Delay(500, cancellationToken);

        // === Step 2: Execute each step with filtered streaming ===
        int stepNum = 0;
        foreach (var step in chain.Steps)
        {
            stepNum++;
            step.Status = StepStatus.Running;
            step.StartedAt = DateTime.UtcNow;
            
            Console.WriteLine($"[ReasoningService] 🔄 Starting step {stepNum}/{steps.Count}: {step.Title}");
            
            // Notify about step start
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.StepStart,
                StepNumber = stepNum,
                StepTitle = step.Title,
                Message = $"🔄 Step {stepNum}/{steps.Count}: {step.Title}"
            };

            // ✅ GENERATE STEP ANSWER WITH FILTERED STREAMING
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
                    Console.WriteLine($"[ReasoningService] ⚠️ Step {stepNum} cancelled");
                    break;
                }
                
                // 🛡️ FILTERING: ensure token does not contain technical symbols
                if (ContainsTechnicalTokens(token))
                {
                    Console.WriteLine($"[ReasoningService] 🚫 Detected technical token in step {stepNum}, stopping");
                    hasError = true;
                    break;
                }
                
                stepContentBuilder.Append(token);
                stepTokenCount++;
                
                // Send each token for smooth UI (throttle every 50ms)
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
                    // Still send without delay
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
            
            Console.WriteLine($"[ReasoningService] ✅ Step {stepNum} completed: {stepTokenCount} tokens");
            
            // Notify about step completion
            yield return new ReasoningStreamEvent
            {
                Type = ReasoningEventType.StepComplete,
                StepNumber = stepNum,
                Message = $"✅ Step {stepNum} completed"
            };
            
            // Small pause between steps for readability
            await Task.Delay(300, cancellationToken);
        }

        // === Step 3: Generate final answer with FILTERING ===
        // ✅ Use localization
        yield return new ReasoningStreamEvent 
        { 
            Type = ReasoningEventType.StatusUpdate, 
            Message = Services.LocalizationService.GetString("Key.ReasoningGeneratingFinalAnswer", "🎯 Generating final answer...")
        };
        
        Console.WriteLine("[ReasoningService] 🎯 Starting final answer generation with filtering...");
        
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
                Console.WriteLine("[ReasoningService] ⚠️ Final answer cancelled");
                break;
            }
            
            // 🛡️ FILTERING: ensure token does not contain technical symbols
            if (ContainsTechnicalTokens(token))
            {
                Console.WriteLine("[ReasoningService] 🚫 Detected technical token in final answer, stopping");
                finalHasError = true;
                break;
            }
            
            finalContentBuilder.Append(token);
            finalTokenCount++;
            
            // Send each token (throttle every 50ms)
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

        Console.WriteLine($"[ReasoningService] ✅ Final answer: {finalTokenCount} tokens");

        yield return new ReasoningStreamEvent
        {
            Type = ReasoningEventType.Complete,
            Message = Services.LocalizationService.GetString("Key.ReasoningComplete", "🎉 Reasoning completed!"),
            Chain = chain
        };

        Console.WriteLine("[ReasoningService] 🎉 Reasoning completed successfully!");
    }

    /// <summary>
    /// Execute async operation safely without try-catch in generator
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
    /// Get reasoning chains history for a conversation
    /// </summary>
    public Task<List<ReasoningChain>> GetChainsForConversationAsync(Guid conversationId)
    {
        return _reasoningDb.GetChainsByConversationAsync(conversationId);
    }

    /// <summary>
    /// Get a specific reasoning chain
    /// </summary>
    public Task<ReasoningChain?> GetChainByIdAsync(Guid chainId)
    {
        return _reasoningDb.GetChainByIdAsync(chainId);
    }

    /// <summary>
    /// Check whether text contains technical tokens (for filtering)
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
/// Events for reasoning stream to display progress
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
    StatusUpdate,      // General status message
    StepStart,         // Start of a new step
    StepContent,       // Step content (streaming tokens)
    StepComplete,      // Step finished
    FinalAnswerContent,// Final answer content (streaming)
    Complete,          // All done
    Error             // Error
}
