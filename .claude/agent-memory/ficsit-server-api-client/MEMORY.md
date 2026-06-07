# ficsit-server-api-client memory

- [Dedicated Server API surface](project_api_surface.md) — doc-verified function list, envelope/casing, error codes, save transports
- [Auth lifecycle design](project_auth_lifecycle.md) — token precedence, 401 re-auth/replay, non-idempotent ambiguity
- [Client layout & open-set envelope](project_client_layout.md) — file map, InvokeRawAsync seam for #11 FRM passthrough, fixtures
- [DI lifetime & validation contract](project_di_lifetime_and_validation.md) — singleton + per-call HttpClient factory, BaseUrl-without-AdminToken bootstrap mode
- [MCP tool conventions](project_mcp_tool_conventions.md) — thin tool→Domain-service shape, hints, exception mapping, optional IFinBridge, verify_connection probes (#6/PR #36)
- [Settings keys & #8 tools](project_settings_keys_and_tools.md) — verified FG.* key set, typed+passthrough mapper, tool hints, first interface-level fake client
