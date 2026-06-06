---
name: convention-stdout-jsonrpc
description: On stdio transport, stdout is the JSON-RPC stream — all logging must go to stderr; never Console.WriteLine
metadata:
  type: feedback
---

The MCP server uses the stdio transport, so `stdout` IS the JSON-RPC protocol stream. All log output is routed to stderr in `Program.cs` via `builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace)`.

**Why:** A single `Console.WriteLine` (or any log line on stdout) corrupts the JSON-RPC stream and surfaces to clients as a baffling "client disconnected" — the kind of bug that eats an afternoon.
**How to apply:** Never write to stdout in host or tool code. When reviewing/adding code in `src/FicsitMcp`, flag any `Console.WriteLine`/`Console.Out`. Keep the stderr logging config in Program.cs intact.

Testing note: when smoke-testing the server over stdio on Windows, MSYS/Bash stdout redirection of the `dotnet` muxer drops the child's stdout (shows empty even when responses are produced). Use native process redirection (PowerShell `System.Diagnostics.Process` with `RedirectStandardOutput`) to actually capture JSON-RPC responses. Verified the server emits clean JSON-RPC on stdout and all logs on stderr this way.

See [[project-solution-layout]].
