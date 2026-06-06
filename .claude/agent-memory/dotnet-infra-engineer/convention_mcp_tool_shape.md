---
name: convention-mcp-tool-shape
description: MCP tools are thin [McpServerToolType] methods that delegate to injectable Domain services; hints set deliberately
metadata:
  type: project
---

Established tool pattern (placeholder `server_info` in `src/FicsitMcp/Tools/ServerInfoTool.cs` is the template):

- Tool class is `[McpServerToolType]`; method is `[McpServerTool(Name = "snake_case_name", ...)]` with a real `[Description]` on the method and every model-facing parameter.
- Tools stay THIN: they delegate to a service interface in `FicsitMcp.Domain`, resolved via DI. The service is registered in `Program.cs` (`AddSingleton<IServerInfoProvider, ServerInfoProvider>()`).
- Service implementations take their external/ambient inputs (assembly, runtime, clock, etc.) as constructor params with production defaults, so they are deterministically unit-testable (Arrange-Act-Assert). Domain has NO MCP reference.
- DI-injected tool parameters (e.g. the service) do NOT leak into the tool's model-facing JSON input schema — verified `inputSchema.properties` empty for server_info.
- Behavioral hints reflect real behavior: `server_info` is `ReadOnly = true, Idempotent = true, OpenWorld = false`. Tool naming via the attribute's `Name`, in snake_case.

**Why:** Keeps tool logic testable in isolation from MCP plumbing and gives surface agents a consistent shape to fill in.
**How to apply:** New tools follow this exact shape. Tool DESIGN (which tools, semantics) is owned by the surface agents, not infra — infra provides the skeleton and DI wiring.

See [[project-solution-layout]], [[convention-stdout-jsonrpc]].
