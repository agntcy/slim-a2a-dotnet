using A2A;
using Xunit;

namespace SlimA2A.IntegrationTests;

/// <summary>GetExtendedAgentCard over SLIMRPC.</summary>
public sealed class AgentCardTests(SlimNodeFixture node)
{
    [Fact]
    public async Task GetExtendedAgentCard_returns_the_served_card()
    {
        var expected = SlimNodeFixture.Card;

        var card = await node.Client.GetExtendedAgentCardAsync(new GetExtendedAgentCardRequest());

        Assert.Equal(expected.Name, card.Name);
        Assert.Equal(expected.Description, card.Description);
        Assert.Equal(expected.Version, card.Version);
        Assert.Equal(expected.DefaultInputModes, card.DefaultInputModes);
        Assert.Equal(expected.DefaultOutputModes, card.DefaultOutputModes);
        Assert.True(card.Capabilities!.Streaming);
        Assert.True(card.Capabilities.PushNotifications);

        var iface = Assert.Single(card.SupportedInterfaces!);
        Assert.Equal("slim://agntcy/slima2a_it/server", iface.Url);
        Assert.Equal("SLIMRPC", iface.ProtocolBinding);
        Assert.Equal("1.0", iface.ProtocolVersion);

        var skill = Assert.Single(card.Skills!);
        Assert.Equal("echo", skill.Id);
        Assert.Equal("Echo", skill.Name);
        Assert.Equal(["echo", "test"], skill.Tags);
    }
}
