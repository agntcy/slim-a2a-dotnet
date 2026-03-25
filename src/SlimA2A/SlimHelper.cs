using Agntcy.Slim;

namespace SlimA2A;

/// <summary>Connects a <see cref="SlimApp"/> to a SLIM server and performs a synchronous <see cref="SlimApp.Subscribe"/> (per Slim .NET API).</summary>
public static class SlimHelper
{
    /// <summary>Initializes Slim (if needed), creates an app, connects asynchronously, then subscribes synchronously.</summary>
    public static async Task<(SlimApp App, ulong ConnectionId)> ConnectAndSubscribeAsync(
        string localIdentity,
        string sharedSecret,
        string serverEndpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(localIdentity);
        ArgumentException.ThrowIfNullOrEmpty(sharedSecret);
        ArgumentException.ThrowIfNullOrEmpty(serverEndpoint);

        Slim.Initialize();
        using var appName = SlimName.Parse(localIdentity);
        using var service = Slim.GetGlobalService();
        var app = service.CreateApp(appName, sharedSecret);
        var connId = await Slim.ConnectAsync(serverEndpoint, cancellationToken).ConfigureAwait(false);
        app.Subscribe(app.Name, connId);
        return (app, connId);
    }
}
