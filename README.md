# ficsit-mcp
Ficsit does not waste

An MCP (Model Context Protocol) server for *Satisfactory*, written in C# / .NET. It exposes
Satisfactory server data and actions to MCP clients (Claude Desktop, Claude Code) over stdio.

## Configuration

The server connects to up to three independent surfaces. **Each is optional** — configure
any subset; the rest stay dormant. Settings come from `appsettings.json` (non-secret
defaults) overridden by `FICSITMCP_`-prefixed environment variables, which is how MCP clients
pass config (including secrets) in their `mcpServers` block.

| Surface | Activating env var | Other env vars |
|---|---|---|
| Dedicated Server HTTPS API | `FICSITMCP_DedicatedServer__BaseUrl` | `FICSITMCP_DedicatedServer__AdminToken` (secret), `FICSITMCP_DedicatedServer__DangerousAcceptAnyCert` (dev only) |
| Ficsit Remote Monitoring (FRM) | `FICSITMCP_Frm__BaseUrl` | `FICSITMCP_Frm__TransportMode` (`Direct` \| `DedicatedApiPassthrough`) |
| FicsIt-Networks bridge | `FICSITMCP_FinBridge__ListenUrl` | `FICSITMCP_FinBridge__SharedSecret` (secret) |

The double underscore `__` separates section from key. A surface counts as configured once its
activating URL is set; a tool that needs an unconfigured surface fails with an actionable
message naming the exact env var to set. Secrets (`AdminToken`, `SharedSecret`) are never
logged or echoed in tool output — keep them in env vars, not in `appsettings.json`.

### Example Claude Desktop / Code config

```json
{
  "mcpServers": {
    "ficsit-mcp": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/ficsit-mcp/src/FicsitMcp"],
      "env": {
        "FICSITMCP_DedicatedServer__BaseUrl": "https://127.0.0.1:7777",
        "FICSITMCP_DedicatedServer__AdminToken": "<admin-api-token>",
        "FICSITMCP_Frm__BaseUrl": "http://127.0.0.1:8080",
        "FICSITMCP_Frm__TransportMode": "Direct",
        "FICSITMCP_FinBridge__ListenUrl": "http://0.0.0.0:8421",
        "FICSITMCP_FinBridge__SharedSecret": "<bridge-shared-secret>"
      }
    }
  }
}
```

Include only the surfaces you use. See `CLAUDE.md` for the full configuration reference and
developer docs.

## Tools

Tools are annotated with behavioral hints (`ReadOnly` / `Destructive` / `Idempotent`) that MCP
clients trust, so they are set to reflect the real consequence of each call.

### Save management (Dedicated Server HTTPS API)

Requires the `DedicatedServer` surface (`FICSITMCP_DedicatedServer__BaseUrl`). Before any load,
delete, or rollback, the named save/session is validated against the live `EnumerateSessions` list;
an unknown name fails with the closest matching names so you can retry without another round trip.

| Tool | Read-only | Destructive | Idempotent | What it does |
|---|:---:|:---:|:---:|---|
| `list_sessions` | yes | no | yes | Lists all saves across sessions, marking the loaded one. |
| `save_game` | no | no | no | Saves the running game under a name (no disconnect). |
| `set_auto_load_session` | no | no | yes | Sets the session to auto-load on next start. |
| `download_save` | yes | no | yes | Streams a save off-box to a local file. |
| `upload_save` | no | **yes** | no | Streams a local save up; can load it immediately (disconnects players). |
| `load_save` | no | **yes** | no | **Disconnects all players** and loads the named save. |
| `delete_save` | no | **yes** | no | Permanently deletes one save file. |
| `delete_session` | no | **yes** | no | Permanently deletes every save in a session. |
| `rollback_to` | no | **yes** | no | Checkpoints the current world first, then loads the target (disconnects players). |

`rollback_to` is the survivable path back: it always takes a `pre-rollback-<utc>` safety save
**before** loading the target, and a failed checkpoint aborts the rollback without loading anything.
