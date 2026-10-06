# slim-a2a-dotnet

[![OpenSSF Scorecard](https://api.scorecard.dev/projects/github.com/agntcy/slim-a2a-dotnet/badge)](https://scorecard.dev/viewer/?uri=github.com/agntcy/slim-a2a-dotnet)

.NET library that maps the [A2A](https://github.com/a2aproject/A2A) gRPC contract to [a2a-dotnet](https://github.com/a2aproject/a2a-dotnet) (`IA2AClient` / `IA2ARequestHandler`) over **SLIMRPC**, similar to slim-a2a-go / slim-a2a-python.

## Dependencies

**Agntcy.Slim** / **Agntcy.Slim.SlimRpc** are consumed from NuGet ([Agntcy.Slim.SlimRpc](https://www.nuget.org/packages/Agntcy.Slim.SlimRpc)), pinned to **2.2.0** (2.1.0 or later is required: earlier releases lose RPC responses that encode to zero bytes, such as `google.protobuf.Empty` or an empty list). In 2.0 the SLIMRPC FFI types (`Channel`, `Server`, `Context`, `RpcException`, …) moved out of `uniffi.slim_bindings` into their own `uniffi.slim_rpc` namespace, so the generated stubs and the SLIMRPC plugin must be on 2.x together.

**A2A** is consumed from NuGet (`A2A` 1.0.0-preview2) so the solution builds with the stock .NET 8 SDK. You can instead use a `ProjectReference` to a local `a2a-dotnet` clone if you need unreleased API changes.

## Usage

One `SlimA2AConnection` per SLIM node serves agents and calls them. Servers and clients created from it share the connection, so one process can both serve an agent and call others.

```csharp
await using var slim = await SlimA2AConnection.ConnectAsync(new SlimA2AConnectionOptions
{
    Endpoint = "https://slim.example.com:46357",
});

// Serve an agent: any IA2ARequestHandler, typically an A2AServer.
var a2a = new A2AServer(agent, new InMemoryTaskStore(), new ChannelEventNotifier(), logger);
await using var server = await slim.StartServerAsync(
    new SlimA2AServerOptions { Identity = "agntcy/a2a/echo", SharedSecret = secret },
    a2a);

// Call an agent: an IA2AClient.
await using var client = slim.CreateClient(new SlimA2AClientOptions
{
    Identity = "agntcy/a2a/client",
    SharedSecret = secret,
    Remote = "agntcy/a2a/echo",            // or the slim:// URL from the agent's card
    DefaultTimeout = TimeSpan.FromSeconds(30),
});
var response = await client.SendMessageAsync(request);
```

- **Transport security** follows the endpoint by default: `http` connects without TLS, `https` with TLS against the system's root CAs. Set `Tls` to choose explicitly: `SlimA2ATls.Insecure`, `SystemRoots`, `TrustedCa(caFile)`, or `InsecureSkipVerify` (development only), and add mutual TLS with `.WithClientCertificate(certFile, keyFile)`. For settings not covered here, such as OIDC authentication to the node, use `ConfigureClient`.
- **Connect timeout**: `ConnectAsync` fails with `TimeoutException` after `ConnectTimeout` (default 30 s) instead of retrying forever.
- **One connection per node endpoint**: the SLIM runtime allows only one per process, so share it. Disposing the connection also stops its servers and disposes its clients.
- **Identities** are SLIM names (`org/namespace/app`); give each server and client its own. Every identity that talks to an agent must use the same shared secret (at least 32 characters).
- **Tenant**: the `Tenant` of every A2A request reaches the server. To serve a card per tenant, set `SlimA2AServerOptions.ResolveExtendedAgentCard`.

## Codegen

1. Install the SlimRPC `protoc` plugin so `protoc-gen-slimrpc-csharp` is on your `PATH` (published as [agntcy-protoc-slimrpc-plugin](https://crates.io/crates/agntcy-protoc-slimrpc-plugin) on crates.io):

   ```bash
   cargo install agntcy-protoc-slimrpc-plugin --version 2.0.0
   ```

2. From `src/SlimA2A.Protos`:

   ```bash
   buf generate
   ```

The A2A proto git ref is pinned in `buf.gen.yaml`; re-verify when bumping **a2a-dotnet** / spec versions. When you upgrade the plugin, align the `cargo install` version (and CI) with the release you want.

## Build & test

```bash
dotnet build SlimA2A.sln
dotnet test SlimA2A.sln
```

### Integration tests

`tests/SlimA2A.IntegrationTests` drives every A2A RPC, including error paths, between a server and a client sharing one `SlimA2AConnection` to a real SLIM node. Without a reachable node these tests are skipped, so `dotnet test` stays green offline. CI runs them against the node pinned in `.github/workflows/ci.yml`. To run them locally, start that node:

```bash
docker run -d --name slim-node -p 127.0.0.1:46357:46357 \
  -v "$PWD/tests/SlimA2A.IntegrationTests/slim-node-config.yaml:/config.yaml:ro" \
  ghcr.io/agntcy/slim:2.1.1 /slim --config /config.yaml
dotnet test tests/SlimA2A.IntegrationTests
```

`SLIM_SERVER` overrides the node endpoint (default `http://127.0.0.1:46357`). Set `SLIM_A2A_INTEGRATION=required` to fail instead of skipping when the node is unreachable.

Cross-language interop with the Go, Python, Java and Node SDKs is covered separately by the [csit A2A SLIMRPC matrix](https://agntcy.github.io/csit/a2a-slimrpc/).

## Echo sample

`examples/EchoAgent` serves an **A2AServer** + **InMemoryTaskStore** + **ChannelEventNotifier** with `SlimA2AConnection.StartServerAsync`, and calls it with a `SlimA2AClient` (same stack idea as the HTTP JSON-RPC samples, but transport is SLIMRPC).

Requires a running SLIM server and compatible shared secret (see slim .NET examples). Demo default secret matches slim samples; override with **`SLIM_SHARED_SECRET`** (min 32 characters). **`SLIM_SERVER`** defaults to `http://localhost:46357`.

```bash
# terminal 1 — SLIM server (use your usual slim server setup)
# terminal 2
dotnet run --project examples/EchoAgent/EchoAgent.csproj -- server
# terminal 3
dotnet run --project examples/EchoAgent/EchoAgent.csproj -- client
```

Optional env: **`SLIM_A2A_SERVER_NAME`**, **`SLIM_A2A_CLIENT_NAME`** (SLIM identities, defaults `agntcy/a2a/echo` and `agntcy/a2a/client`).

CLI overrides env for endpoint and secret: `--server`, `--shared-secret` (server and client must use the same secret as the SLIM server).

## Migrating from 0.2

0.3 replaces the low-level setup with `SlimA2AConnection`, and no public API exposes SLIM FFI (`uniffi.*`) or generated (`Lf.A2a.V1`) types any more.

| 0.2 | 0.3 |
|---|---|
| `SlimHelper.ConnectAndSubscribeAsync(identity, secret, endpoint)` | `SlimA2AConnection.ConnectAsync(new() { Endpoint = endpoint })`; identities move to the server and client options |
| `SlimRpcServerFactory.CreateServer` + `SlimA2AServerRegistration.RegisterA2AService(server, new SlimA2AHandler(a2a, resolveCard))` + `ServeAsync()` | `await connection.StartServerAsync(new() { Identity, SharedSecret, ResolveExtendedAgentCard }, a2a)`; await `server.Completion`, stop with `StopAsync()` or dispose |
| `SlimRpcChannelFactory.CreateChannel` + `new SlimA2AClient(channel, timeout)` | `connection.CreateClient(new() { Identity, SharedSecret, Remote, DefaultTimeout })` |
| resolve-card callback `Func<CancellationToken, Task<AgentCard>>` | `ResolveExtendedAgentCard`: `Func<GetExtendedAgentCardRequest, CancellationToken, Task<AgentCard>>`, which sees the tenant |
| `SlimA2AHandler`, `ProtoConverter`, `A2ARpcErrorMapping` | internal; no replacement needed |

Also in 0.3:

- **A2A 1.0.0-preview2**: `SendMessageConfiguration.Blocking` became `ReturnImmediately`, with the **opposite meaning**: replace `Blocking = false` with `ReturnImmediately = true`.
- **Agntcy.Slim 2.2.0**; projects that reference the SLIM packages themselves need the same version.
- Projects no longer need a reference to the generated `Lf.A2a.V1` types.

## Security note

The sample's plaintext connection (`http://` endpoint) and demo shared secret are for local development only. In production, connect over `https://` (or set `Tls`), and use a strong secret from configuration.
