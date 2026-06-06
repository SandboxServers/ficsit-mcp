---
name: project-api-surface
description: Doc-verified Dedicated Server HTTPS API function list, envelope shapes, JSON casing, error codes, and save transports (issue #5)
metadata:
  type: project
---

Verified against the Satisfactory wiki (satisfactory.wiki.gg/wiki/Dedicated_servers/HTTPS_API) and
the community OpenAPI spec (satisfactory-oas.github.io/spec, api version **0.2.1**) on 2026-06-06.
The official docs.ficsit.app `.../latest/...DedicatedServerAPI.html` URL **404s / 403s** — use the
wiki + OAS instead. Game build not stated by the spec; surface targets dedicated server **1.0+**.

**Endpoint:** `POST {BaseUrl}/api/v1` (single endpoint, function envelope). Also accepts
`?function=Name` query form, but we use the body envelope exclusively.

**Envelopes (camelCase per OAS):**
- Request: `{"function":"<Name>","data":{...}}` (omit `data` when none).
- Success: `{"data":{...}}`, or **204 No Content** for void functions.
- Error: `{"errorCode":"...","errorMessage":"...","errorData":{...}}` (errorData optional, free-form).

**Function list (the `function` strings, in `ApiFunctions.cs`):**
Auth: `PasswordlessLogin`, `PasswordLogin`, `VerifyAuthenticationToken` (204).
Reads: `HealthCheck`, `QueryServerState`, `GetServerOptions`, `GetAdvancedGameSettings`, `EnumerateSessions`.
Mgmt: `ClaimServer`, `RenameServer`, `SetClientPassword`, `SetAdminPassword`, `SetAutoLoadSessionName`,
`ApplyServerOptions`, `ApplyAdvancedGameSettings`.
Console: `RunCommand`, `Shutdown`.
Saves: `CreateNewGame`, `SaveGame`, `LoadGame`, `DeleteSaveFile`, `DeleteSaveSession`,
`UploadSaveGame`, `DownloadSaveGame`.

**Corrections vs the issue text:** issue listed `QueryServerState`/`PasswordlessLogin`/`ClaimServer`
from training data and was broadly right. Confirmed additions the issue did not enumerate:
`GetServerOptions`/`ApplyServerOptions`, `GetAdvancedGameSettings`/`ApplyAdvancedGameSettings`,
`RenameServer`, `SetClientPassword`, `SetAdminPassword`, `SetAutoLoadSessionName`,
`DeleteSaveFile` vs `DeleteSaveSession` (two distinct functions). Issue said "SaveGameList" — the
real name is `EnumerateSessions`.

**Key casing / quirks (must match exactly):**
- Token field is `authenticationToken` (camelCase 'a') on PasswordLogin/PasswordlessLogin/ClaimServer/SetAdminPassword.
- `QueryServerState` nests under `serverGameState` (a sub-state envelope) — fields: activeSessionName,
  numConnectedPlayers, playerLimit, techTier, activeSchematic, gamePhase, isGameRunning,
  totalGameDuration, isGamePaused, averageTickRate, autoLoadSessionName.
- `CreateNewGame` uses **`bSkipOnboarding`** (Unreal bool prefix), NOT `skipOnboarding` — server bug.
- `EnumerateSessions` → `sessions[]` (each SessionSaveStruct has `saveHeaders[]`) + `currentSessionIndex`.
- `minimumPrivilegeLevel` serialized BY NAME: NotAuthenticated/Client/Administrator/InitialAdmin/APIToken.

**Save transports:**
- `UploadSaveGame` = **multipart/form-data** with parts `data` (JSON, application/json), `_charset_`
  (plain "utf-8"), `saveGameFile` (binary octet-stream). Streamed, never buffered whole. 201/204 on success.
- `DownloadSaveGame` = **direct binary body** on success (Content-Disposition), JSON **error envelope**
  on failure. We branch on Content-Type: JSON → map error; else stream binary to destination.

**errorCode → exception mapping** (in `DedicatedServerApiClient.IsAuthErrorCode`):
auth codes (`wrong_password`, `unauthorized`, `invalid_token`, `token_expired`,
`insufficient_privilege`, `passwordless_login_not_possible`) + any 401 → `DedicatedServerAuthException`.
All other codes (e.g. `server_claimed`, `save_game_load_failed`, `file_not_found`,
`enumerate_sessions_failed`, `delete_save_*_failed`) → `DedicatedServerApiException` carrying
ErrorCode/ServerMessage/ErrorData/HttpStatusCode.

See [[project-auth-lifecycle]] and [[project-client-layout]].
