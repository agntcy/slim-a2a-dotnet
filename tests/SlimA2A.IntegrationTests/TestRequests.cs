using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

internal static class TestRequests
{
    public static SendMessageRequest Text(string text, string? contextId = null, string? taskId = null) =>
        Parts([Part.FromText(text)], contextId, taskId);

    public static SendMessageRequest Parts(List<Part> parts, string? contextId = null, string? taskId = null) =>
        new()
        {
            Message = new Message
            {
                Role = Role.User,
                MessageId = NewId(),
                ContextId = contextId,
                TaskId = taskId,
                Parts = parts,
            },
        };

    public static string NewId() => Guid.NewGuid().ToString("N");

    /// <summary>Sends a request the agent answers with a task, and returns that task.</summary>
    public static async Task<AgentTask> CreateTaskAsync(
        this SlimA2AClient client, string text, string? contextId = null, CancellationToken cancellationToken = default)
    {
        var response = await client.SendMessageAsync(Text(text, contextId), cancellationToken).ConfigureAwait(false);
        Assert.Equal(SendMessageResponseCase.Task, response.PayloadCase);
        return response.Task!;
    }

    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var items = new List<T>();
        await foreach (var item in source.ConfigureAwait(false))
            items.Add(item);
        return items;
    }
}
