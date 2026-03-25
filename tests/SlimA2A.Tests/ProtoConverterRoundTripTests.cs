using System.Text.Json;
using A2A;
using Xunit;

namespace SlimA2A.Tests;

public sealed class ProtoConverterRoundTripTests
{
    [Fact]
    public void Message_text_part_and_metadata_round_trip()
    {
        var original = new Message
        {
            MessageId = "m1",
            Role = Role.User,
            ContextId = "ctx-1",
            Parts = [Part.FromText("hello")],
            Metadata = new Dictionary<string, JsonElement>
            {
                ["k"] = JsonDocument.Parse("\"v\"").RootElement,
            },
        };

        var proto = ProtoConverter.ToProto(original);
        var back = ProtoConverter.FromProto(proto);

        Assert.Equal(original.MessageId, back.MessageId);
        Assert.Equal(original.Role, back.Role);
        Assert.Equal(original.ContextId, back.ContextId);
        Assert.Single(back.Parts!);
        Assert.Equal("hello", back.Parts![0].Text);
        Assert.NotNull(back.Metadata);
        Assert.True(back.Metadata!.TryGetValue("k", out var el));
        Assert.Equal("v", el.GetString());
    }

    [Fact]
    public void AgentTask_status_and_history_round_trip()
    {
        var original = new AgentTask
        {
            Id = "t1",
            ContextId = "c1",
            Status = new A2A.TaskStatus
            {
                State = TaskState.Completed,
                Message = new Message
                {
                    MessageId = "sys",
                    Role = Role.Agent,
                    Parts = [Part.FromText("done")],
                },
            },
            History =
            [
                new Message
                {
                    MessageId = "u1",
                    Role = Role.User,
                    Parts = [Part.FromText("ping")],
                },
            ],
        };

        var proto = ProtoConverter.ToProto(original);
        var back = ProtoConverter.FromProto(proto);

        Assert.Equal(original.Id, back.Id);
        Assert.Equal(original.ContextId, back.ContextId);
        Assert.Equal(original.Status.State, back.Status.State);
        Assert.NotNull(back.Status.Message);
        Assert.Equal("done", back.Status.Message!.Parts![0].Text);
        Assert.Single(back.History!);
        Assert.Equal("ping", back.History![0].Parts![0].Text);
    }

    [Fact]
    public void SendMessageRequest_round_trip()
    {
        var msg = new Message
        {
            MessageId = "x",
            Role = Role.User,
            Parts = [Part.FromText("q")],
        };
        var original = new SendMessageRequest
        {
            Message = msg,
            Configuration = new SendMessageConfiguration
            {
                Blocking = true,
                HistoryLength = 3,
                AcceptedOutputModes = ["text/plain"],
            },
        };

        var proto = ProtoConverter.ToProto(original);
        var back = ProtoConverter.FromProto(proto);

        Assert.Equal(msg.MessageId, back.Message.MessageId);
        Assert.True(back.Configuration!.Blocking);
        Assert.Equal(3, back.Configuration.HistoryLength);
        Assert.Equal("text/plain", back.Configuration.AcceptedOutputModes![0]);
    }

    [Fact]
    public void GetTaskRequest_round_trip()
    {
        var original = new GetTaskRequest { Id = "abc", HistoryLength = 5 };
        var proto = ProtoConverter.ToProto(original);
        var back = ProtoConverter.FromProto(proto);
        Assert.Equal(original.Id, back.Id);
        Assert.Equal(original.HistoryLength, back.HistoryLength);
    }
}
