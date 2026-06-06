# Dedicated Server HTTPS API envelope fixtures

Captured/representative wire envelopes for the official Satisfactory Dedicated Server HTTPS API
(`POST https://<host>:7777/api/v1`), verified against the Satisfactory modding docs and the
community OpenAPI spec (api version 0.2.1). All secrets (passwords, bearer tokens) are **redacted**
to placeholder values — never commit a real token.

These exist so `mcp-test-harness-engineer` (#22) and the client tests can exercise every code path
without a live server:

| File | Path exercised |
|---|---|
| `request_queryserverstate.json` | Idempotent request envelope (`function` + no `data`). |
| `request_savegame.json` | Non-idempotent request envelope with a typed `data` payload. |
| `request_passwordlogin.json` | Auth request; privilege level serialized by NAME, password present (redacted). |
| `response_queryserverstate_success.json` | Success envelope with the nested `serverGameState` sub-state. |
| `response_enumeratesessions_success.json` | Success envelope with `sessions[]` / `saveHeaders[]`. |
| `response_healthcheck_success.json` | Unauthenticated success envelope. |
| `response_error_wrong_password.json` | Error envelope → `DedicatedServerAuthException`. |
| `response_error_server_claimed.json` | Error envelope → `DedicatedServerApiException` (ClaimServer one-shot). |
| `response_error_save_game_load_failed.json` | Error envelope with `errorData`. |
| `response_401_bare.txt` | A bare 401 with no body (drives re-auth/replay). |

Field casing is camelCase (`authenticationToken`, `serverGameState`, `health`, `saveName`), per the
OpenAPI spec. `CreateNewGame` uses `bSkipOnboarding` (an Unreal-prefixed server quirk), captured in
`request_createnewgame.json`.
