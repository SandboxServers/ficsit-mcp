---
name: convention-editorconfig-rationale
description: Every rule-severity override in any config file carries a one-line why-comment; defaults need none
metadata:
  type: feedback
---

In every config file (`.editorconfig`, props, future markdownlint/CVE-ignore/coverage configs), every deviation from a tool's default — especially a rule-severity override — must carry a one-line comment saying WHY. Template/tool defaults need no comment; deviations do.

**Why:** This is the best meta-pattern from the Cimmeria repo review (2026-06-05) that this .NET port is mirroring. A bare severity override is unreviewable; the rationale lets the next person judge whether it still applies.
**How to apply:** When changing a rule severity or adding a config deviation, add the comment in the same edit. In `.editorconfig`, current commented deviations: `insert_final_newline = true` (template default is false) and `csharp_style_namespace_declarations = file_scoped:warning` (promoted from :suggestion to enforce file-scoped namespaces as a build error).

File-scoped namespaces are mandatory and enforced (warning => error under TreatWarningsAsErrors). One public type per file.

See [[project-solution-layout]].
