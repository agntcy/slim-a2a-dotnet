using A2A;

namespace SlimA2A.IntegrationTests;

/// <summary>
/// Agent driven by sentinel request texts (mirrors csit's CsitEchoHandler). Any other request is echoed back:
/// the task completes with one artifact holding the request's parts unchanged, so tests can check what crossed the wire.
/// </summary>
internal sealed class TestAgent : IAgentHandler
{
    /// <summary>Replies with a bare message; no task is created.</summary>
    public const string MessageOnly = "it:message-only";
    public const string MessageOnlyReply = "message-only reply";

    /// <summary>Leaves the task in input-required (non-terminal), so it can be subscribed to and canceled.</summary>
    public const string InputRequired = "it:input-required";
    public const string InputPrompt = "need more input";

    /// <summary>Continues an existing task (sent with its task id) to completion: the second turn of a conversation.</summary>
    public const string Continue = "it:continue";
    public const string ContinueReply = "continued";

    /// <summary>Emits working status, two artifact chunks, then completes.</summary>
    public const string Streaming = "it:streaming";
    public static readonly string[] StreamingChunks = ["chunk 1 ", "chunk 2"];

    /// <summary>Fails with InvalidParams before emitting anything.</summary>
    public const string FailInvalidParams = "it:fail-invalid-params";

    /// <summary>Submits the task, then fails with InvalidParams: an error after the first stream event.</summary>
    public const string FailAfterSubmit = "it:fail-after-submit";

    public const string FailureMessage = "rejected by test agent";

    /// <summary>Works for <see cref="SleepDuration"/>, honouring cancellation; <see cref="LastSleep"/> reports how it ended.</summary>
    public const string Sleep = "it:sleep";
    public static readonly TimeSpan SleepDuration = TimeSpan.FromSeconds(10);

    /// <summary>Completes with true when the last <see cref="Sleep"/> request was cancelled, false when it ran to the end.</summary>
    public TaskCompletionSource<bool> LastSleep { get; private set; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task ExecuteAsync(RequestContext context, AgentEventQueue queue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(queue, context.TaskId, context.ContextId);
        var isNew = context.Task is null;

        switch (context.UserText)
        {
            case MessageOnly:
                await queue.EnqueueMessageAsync(
                    new Message
                    {
                        Role = Role.Agent,
                        MessageId = Guid.NewGuid().ToString("N"),
                        ContextId = context.ContextId,
                        Parts = [Part.FromText(MessageOnlyReply)],
                    },
                    cancellationToken).ConfigureAwait(false);
                break;

            case InputRequired:
                if (isNew) await updater.SubmitAsync(cancellationToken).ConfigureAwait(false);
                await updater.RequireInputAsync(
                    new Message
                    {
                        Role = Role.Agent,
                        MessageId = Guid.NewGuid().ToString("N"),
                        ContextId = context.ContextId,
                        Parts = [Part.FromText(InputPrompt)],
                    },
                    cancellationToken).ConfigureAwait(false);
                break;

            case Continue when !isNew:
                // A unary response is built from the first Task/Message event, so re-surface the task before updating it.
                await queue.EnqueueTaskAsync(context.Task!, cancellationToken).ConfigureAwait(false);
                await updater.AddArtifactAsync([Part.FromText(ContinueReply)], cancellationToken: cancellationToken).ConfigureAwait(false);
                await updater.CompleteAsync(null, cancellationToken).ConfigureAwait(false);
                break;

            case Streaming:
                if (isNew) await updater.SubmitAsync(cancellationToken).ConfigureAwait(false);
                await updater.StartWorkAsync(null, cancellationToken).ConfigureAwait(false);
                foreach (var chunk in StreamingChunks)
                    await updater.AddArtifactAsync([Part.FromText(chunk)], cancellationToken: cancellationToken).ConfigureAwait(false);
                await updater.CompleteAsync(null, cancellationToken).ConfigureAwait(false);
                break;

            case Sleep:
                var outcome = LastSleep = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    await Task.Delay(SleepDuration, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    outcome.TrySetResult(true);
                    throw;
                }
                outcome.TrySetResult(false);
                await queue.EnqueueMessageAsync(
                    new Message { Role = Role.Agent, MessageId = Guid.NewGuid().ToString("N"), Parts = [Part.FromText("slept")] },
                    cancellationToken).ConfigureAwait(false);
                break;

            case FailInvalidParams:
                throw new A2AException(FailureMessage, A2AErrorCode.InvalidParams);

            case FailAfterSubmit:
                if (isNew) await updater.SubmitAsync(cancellationToken).ConfigureAwait(false);
                throw new A2AException(FailureMessage, A2AErrorCode.InvalidParams);

            default:
                if (isNew) await updater.SubmitAsync(cancellationToken).ConfigureAwait(false);
                await updater.StartWorkAsync(null, cancellationToken).ConfigureAwait(false);
                await updater.AddArtifactAsync(context.Message?.Parts ?? [], cancellationToken: cancellationToken).ConfigureAwait(false);
                await updater.CompleteAsync(null, cancellationToken).ConfigureAwait(false);
                break;
        }
    }

    public async Task CancelAsync(RequestContext context, AgentEventQueue queue, CancellationToken cancellationToken)
    {
        var updater = new TaskUpdater(queue, context.TaskId, context.ContextId);
        await updater.CancelAsync(cancellationToken).ConfigureAwait(false);
    }
}
