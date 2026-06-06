# dotnet-infra-engineer memory

- [Solution layout](project_solution_layout.md) — projects, root config (global.json, Directory.Build.props, Directory.Packages.props), net10.0, where versions/settings live
- [stdout is JSON-RPC](convention_stdout_jsonrpc.md) — stdio transport: all logging to stderr, never Console.WriteLine; Windows stdio capture gotcha
- [editorconfig rationale discipline](convention_editorconfig_rationale.md) — every config severity override carries a why-comment; file-scoped namespaces enforced
- [MCP tool shape](convention_mcp_tool_shape.md) — thin [McpServerToolType] tools delegating to injectable Domain services; behavioral hints set deliberately
