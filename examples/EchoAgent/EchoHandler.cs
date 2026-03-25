using A2A;

namespace EchoAgent;

internal sealed class EchoHandler : IAgentHandler
{
    public async Task ExecuteAsync(RequestContext context, AgentEventQueue eventQueue, CancellationToken cancellationToken)
    {
        var reply = $"Echo: {context.UserText}";
        Console.WriteLine($"[EchoAgent] server: request contextId={context.ContextId} userText={context.UserText}");
        var responder = new MessageResponder(eventQueue, context.ContextId);
        await responder.ReplyAsync(reply, cancellationToken).ConfigureAwait(false);
        Console.WriteLine($"[EchoAgent] server: sent reply ({reply.Length} chars)");
    }
}
