using System.Text.Json;
using A2A;
using Xunit;

namespace SlimA2A.Tests;

/// <summary>
/// Every message with a <c>tenant</c> field carries it both ways. Checked on the proto as well as after the round trip,
/// so a converter that drops the tenant on the way out and invents it on the way back can't pass.
/// </summary>
public sealed class TenantMappingTests
{
    private static Message Msg() => new() { MessageId = "m", Role = Role.User, Parts = [Part.FromText("hi")] };

    private static PushNotificationConfig Push() => new() { Url = "https://example.com/hook" };

    private static AgentCard Card(string? tenant) => new()
    {
        Name = "n",
        Description = "d",
        Version = "1",
        SupportedInterfaces = [new AgentInterface { Url = "slim://a/b/c", ProtocolBinding = "SLIMRPC", ProtocolVersion = "1.0", Tenant = tenant }],
        DefaultInputModes = ["text/plain"],
        DefaultOutputModes = ["text/plain"],
        Capabilities = new AgentCapabilities(),
        Skills = [],
    };

    /// <summary>Tenant in → (tenant on the proto, tenant after converting back).</summary>
    private static readonly Dictionary<string, Func<string?, (string Wire, string? Back)>> RoundTrips = new()
    {
        ["SendMessageRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new SendMessageRequest { Message = Msg(), Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["GetTaskRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new GetTaskRequest { Id = "t1", Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["ListTasksRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new ListTasksRequest { Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["CancelTaskRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new CancelTaskRequest { Id = "t1", Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["SubscribeToTaskRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new SubscribeToTaskRequest { Id = "t1", Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["CreateTaskPushNotificationConfigRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new CreateTaskPushNotificationConfigRequest { TaskId = "t1", ConfigId = "c1", Config = Push(), Tenant = t });
            return (p.Tenant, ProtoConverter.FromProtoCreateRequest(p).Tenant);
        },
        ["TaskPushNotificationConfig"] = t =>
        {
            var p = ProtoConverter.ToProtoResource(new TaskPushNotificationConfig { Id = "c1", TaskId = "t1", PushNotificationConfig = Push(), Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["GetTaskPushNotificationConfigRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new GetTaskPushNotificationConfigRequest { TaskId = "t1", Id = "c1", Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["ListTaskPushNotificationConfigRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new ListTaskPushNotificationConfigRequest { TaskId = "t1", Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["DeleteTaskPushNotificationConfigRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new DeleteTaskPushNotificationConfigRequest { TaskId = "t1", Id = "c1", Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["GetExtendedAgentCardRequest"] = t =>
        {
            var p = ProtoConverter.ToProto(new GetExtendedAgentCardRequest { Tenant = t });
            return (p.Tenant, ProtoConverter.FromProto(p).Tenant);
        },
        ["AgentInterface"] = t =>
        {
            var p = ProtoConverter.ToProto(Card(t));
            return (p.SupportedInterfaces[0].Tenant, ProtoConverter.FromProto(p).SupportedInterfaces[0].Tenant);
        },
    };

    public static TheoryData<string> Messages()
    {
        var data = new TheoryData<string>();
        foreach (var name in RoundTrips.Keys)
            data.Add(name);
        return data;
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Tenant_is_carried_both_ways(string message)
    {
        var (wire, back) = RoundTrips[message]("acme");

        Assert.Equal("acme", wire);
        Assert.Equal("acme", back);
    }

    [Theory]
    [MemberData(nameof(Messages))]
    public void Missing_tenant_stays_missing(string message)
    {
        var (wire, back) = RoundTrips[message](null);

        Assert.Equal("", wire);
        Assert.Null(back);
    }

    [Fact]
    public void CancelTaskRequest_carries_metadata()
    {
        var original = new CancelTaskRequest
        {
            Id = "t1",
            Metadata = new Dictionary<string, JsonElement> { ["reason"] = JsonDocument.Parse("\"user\"").RootElement },
        };

        var back = ProtoConverter.FromProto(ProtoConverter.ToProto(original));

        Assert.Equal("user", back.Metadata!["reason"].GetString());
    }
}
