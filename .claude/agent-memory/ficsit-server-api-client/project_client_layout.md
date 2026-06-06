---
name: project-client-layout
description: Where the DedicatedServerApiClient lives, the open-set InvokeRawAsync seam for #11, source-gen JSON, and fixture locations
metadata:
  type: project
---

Client code (issue #5) lives in `src/FicsitMcp.Domain/DedicatedServer/`:
- `IDedicatedServerApiClient` + `DedicatedServerApiClient` (one method per function).
- `Model/` — request/response records, one file per group; `ApiPrivilegeLevel` enum (string-serialized).
- `DedicatedServerJsonContext` — STJ **source-generated** context (CamelCase, UseStringEnumConverter,
  IgnoreNull). Registers every request/response record + `DedicatedServerSuccessEnvelope<T>` per shape +
  `DedicatedServerErrorEnvelope` + `JsonElement`. NB: the two envelope types must be **public** or the
  generated context fails with CS0053 (inconsistent accessibility).
- Exceptions: `DedicatedServerApiException` (base, carries ErrorCode/ServerMessage/ErrorData/HttpStatusCode),
  `DedicatedServerAuthException` (derives), `DedicatedServerAmbiguousResultException` (derives).
- `ApiFunctions` — internal const strings for the closed base-game function set.

**Open-set envelope (for #11 FRM passthrough):** `InvokeRawAsync(string function, JsonElement? data,
bool allowRetry=false)` sends ANY function name through the same envelope build + bearer auth + error
mapping, returns the raw `data` sub-element (or null on 204). Function name is never validated against a
closed enum — mods (FRM registers `frm`) reach the API this way with TLS+auth. Typed methods are the
base-game layer on top of the same `BuildEnvelopeRequest`/`SendWithReauthAsync` core.

**Body handling gotcha:** response body is buffered ONCE via `ReadBodyBytesAsync` (ReadAsByteArrayAsync),
then both the error-envelope check and the success deserialize run off the bytes. Original bug: calling
`ReadFromJsonAsync` for the error check consumed/closed the stream → ObjectDisposedException on the
success read. Download success path stays streamed (binary copied in chunks); only JSON bodies buffer.

**DI:** registered in the HOST at `src/FicsitMcp/DedicatedServer/DedicatedServerClientRegistration.cs`
(`AddDedicatedServerApiClient`, called from Program.cs). Wraps the factory `HttpClient`
(`SurfaceHttpClients.DedicatedServer`) in the infra `SurfaceHttpClient` shell — never `new HttpClient()`.

**Tests/fixtures:** `tests/FicsitMcp.Tests/DedicatedServer/` (DedicatedServerTestHarness +
RecordingHandler capture envelope/headers/AllowRetry; DedicatedServerApiClientTests = 30+ cases).
Redacted wire fixtures for #22 in `tests/FicsitMcp.Tests/Fixtures/DedicatedServer/` (request/success/
error/401 envelopes; secrets are placeholders). Fixtures copy to output via the existing csproj glob.

See [[project-api-surface]] and [[project-auth-lifecycle]].
