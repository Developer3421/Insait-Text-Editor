// filepath: InsaitTextEditor.Tests/MsAgentsCoreMapperTests.cs
using System;
using System.Linq;
using InsaitTextEditor.AI;
using InsaitTextEditor.Models;
using Xunit;

namespace InsaitTextEditor.Tests;

public class MsAgentsCoreMapperTests
{
    [Fact]
    public void RoundTrip_UserMessage_PreservesContentAndRole()
    {
        var original = new ChatMessage
        {
            Sender = "User",
            Content = "Привіт, як справи?",
            Timestamp = DateTime.UtcNow.AddMinutes(-1)
        };

        var core = MsAgentsCoreMapper.TryCreateCoreMessage(original);
        Assert.NotNull(core);

        var back = MsAgentsCoreMapper.FromCoreMessage(core!);
        Assert.Equal("User", back.Sender);
        Assert.Equal(original.Content, back.Content);
        Assert.True((DateTime.UtcNow - back.Timestamp).TotalHours < 1);
    }

    [Fact]
    public void ToCoreMessages_ReturnsList_ForHistory()
    {
        var history = new[]
        {
            new ChatMessage { Sender = "User", Content = "Hello" },
            new ChatMessage { Sender = "Assistant", Content = "Hi!" }
        }.ToList();

        var list = MsAgentsCoreMapper.ToCoreMessages(history);
        Assert.NotNull(list);
        Assert.True(list.Count >= 1);
    }
}

