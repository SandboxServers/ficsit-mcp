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
