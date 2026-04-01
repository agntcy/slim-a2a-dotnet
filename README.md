# slim-a2a-dotnet

.NET library that maps the [A2A](https://github.com/a2aproject/A2A) gRPC contract to [a2a-dotnet](https://github.com/a2aproject/a2a-dotnet) (`IA2AClient` / `IA2ARequestHandler`) over **SLIMRPC**, similar to slim-a2a-go / slim-a2a-python.

## Dependencies

**Agntcy.Slim** / **Agntcy.Slim.SlimRpc** are consumed from NuGet ([Agntcy.Slim.SlimRpc](https://www.nuget.org/packages/Agntcy.Slim.SlimRpc)).

**A2A** is consumed from NuGet (`A2A` preview) so the solution builds with the stock .NET 8 SDK. You can instead use a `ProjectReference` to a local `a2a-dotnet` clone if you need unreleased API changes.

## Codegen

1. Install the SlimRPC `protoc` plugin so `protoc-gen-slimrpc-csharp` is on your `PATH` (published as [agntcy-protoc-slimrpc-plugin](https://crates.io/crates/agntcy-protoc-slimrpc-plugin) on crates.io):

   ```bash
   cargo install agntcy-protoc-slimrpc-plugin --version 1.3.0
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

## Echo sample

`examples/EchoAgent` hosts an **A2AServer** + **InMemoryTaskStore** + **ChannelEventNotifier** behind **SlimA2AHandler** (same stack idea as the HTTP JSON-RPC samples, but transport is SLIMRPC).

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

## Security note

The sample insecure client config and demo shared secret are for local development only, not for production.
