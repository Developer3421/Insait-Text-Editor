using System.Collections.Generic;
using System.Linq;
using InsaitTextEditor.Models;

namespace InsaitTextEditor.Services;

public class ConversationStateService
{
    private readonly List<ChatMessage> _conversationHistory = new();
    private int _totalTokensUsed = 0;

    public void AddMessage(ChatMessage message)
    {
        _conversationHistory.Add(message);
        
        if (message is AgentMessage agentMessage)
        {
            _totalTokensUsed += agentMessage.TokensUsed;
        }
    }

    public List<ChatMessage> GetConversationHistory()
    {
        return _conversationHistory.ToList();
    }

    public void ClearHistory()
    {
        _conversationHistory.Clear();
        _totalTokensUsed = 0;
    }

    public int GetTotalTokensUsed()
    {
        return _totalTokensUsed;
    }

    public int GetMessageCount()
    {
        return _conversationHistory.Count;
    }

    // Limit history for context (e.g. last N messages)
    public List<ChatMessage> GetRecentMessages(int count)
    {
        return _conversationHistory.TakeLast(count).ToList();
    }
}