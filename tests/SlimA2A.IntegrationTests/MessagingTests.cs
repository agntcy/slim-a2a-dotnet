using System.Text.Json;
using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>SendMessage and SendStreamingMessage over SLIMRPC.</summary>
public sealed class MessagingTests(SlimNodeFixture node)
{
    [Fact]
    public async Task SendMessage_returns_a_bare_message_reply()
    {
        var response = await node.Client.SendMessageAsync(TestRequests.Text(TestAgent.MessageOnly));

        Assert.Equal(SendMessageResponseCase.Message, response.PayloadCase);
        Assert.Equal(Role.Agent, response.Message!.Role);
        Assert.Equal(TestAgent.MessageOnlyReply, Assert.Single(response.Message.Parts!).Text);
    }

    [Fact]
    public async Task SendMessage_returns_a_completed_task_with_the_echoed_artifact()
    {
        var task = await node.Client.CreateTaskAsync("hello over slim");

        Assert.Equal(TaskState.Completed, task.Status.State);
        Assert.Equal("hello over slim", Assert.Single(Assert.Single(task.Artifacts!).Parts).Text);
    }

    [Fact]
    public async Task SendMessage_round_trips_raw_url_and_data_parts_with_metadata()
    {
        byte[] raw = [0x00, 0x01, 0x7F, 0xFE, 0xFF];
        var data = JsonDocument.Parse("""{"name":"slim","count":3,"nested":{"ok":true},"list":["a","b"]}""").RootElement;
        var text = Part.FromText("text part");
        text.Metadata = new Dictionary<string, JsonElement> { ["source"] = JsonDocument.Parse("\"integration\"").RootElement };

        var response = await node.Client.SendMessageAsync(TestRequests.Parts(
        [
            text,
            Part.FromRaw(raw, "application/octet-stream", "blob.bin"),
            Part.FromUrl("https://example.com/report.pdf", "application/pdf", "report.pdf"),
            Part.FromData(data),
        ]));

        var parts = Assert.Single(response.Task!.Artifacts!).Parts;
        Assert.Equal(4, parts.Count);

        Assert.Equal("text part", parts[0].Text);
        Assert.Equal("integration", parts[0].Metadata!["source"].GetString());

        Assert.Equal(raw, parts[1].Raw);
        Assert.Equal("application/octet-stream", parts[1].MediaType);
        Assert.Equal("blob.bin", parts[1].Filename);

        Assert.Equal("https://example.com/report.pdf", parts[2].Url);
        Assert.Equal("application/pdf", parts[2].MediaType);
        Assert.Equal("report.pdf", parts[2].Filename);

        var echoed = parts[3].Data!.Value;
        Assert.Equal("slim", echoed.GetProperty("name").GetString());
        Assert.Equal(3, echoed.GetProperty("count").GetDouble());
        Assert.True(echoed.GetProperty("nested").GetProperty("ok").GetBoolean());
        Assert.Equal(["a", "b"], echoed.GetProperty("list").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task SendStreamingMessage_streams_artifact_chunks_then_completes()
    {
        var events = await node.Client.SendStreamingMessageAsync(TestRequests.Text(TestAgent.Streaming)).ToListAsync();

        var chunks = events
            .Where(e => e.PayloadCase == StreamResponseCase.ArtifactUpdate)
            .SelectMany(e => e.ArtifactUpdate!.Artifact.Parts)
            .Select(p => p.Text);
        Assert.Equal(TestAgent.StreamingChunks, chunks);

        var taskIds = events.Select(TaskIdOf).Distinct().ToList();
        Assert.Single(taskIds);
        Assert.False(string.IsNullOrEmpty(taskIds[0]));

        var last = events[^1];
        Assert.Equal(StreamResponseCase.StatusUpdate, last.PayloadCase);
        Assert.Equal(TaskState.Completed, last.StatusUpdate!.Status.State);
    }

    internal static string? TaskIdOf(StreamResponse e) => e.PayloadCase switch
    {
        StreamResponseCase.Task => e.Task!.Id,
        StreamResponseCase.StatusUpdate => e.StatusUpdate!.TaskId,
        StreamResponseCase.ArtifactUpdate => e.ArtifactUpdate!.TaskId,
        _ => null,
    };
}
