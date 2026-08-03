---
name: vsa-review
version: 1
description: "Review a VSA change for Product.Template. TRIGGER: \"review slice\", \"review PR\", \"architecture check\". SKIP: scaffolding (use /new-slice or /new-module)."
disable-model-invocation: true
---

# Skill: /vsa-review

## Checklist

1. Flat slice file; no layer folders; noun vs verb naming
2. No `Program.cs` edits for endpoints; `IEndpoint` + module registration only when needed
3. Repo: per-aggregate interface; no public `IRepository<T>`
4. Auth: named policies only; tenant/`security_stamp` if Identity touch
5. `features.json` entry matches app/test/route/flag/policy
6. Tests mirror under `tests/App.Tests/{Module}/`; no tests in `src/App/`

## Verify

```bash
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/App.Tests --filter <SliceOrModule>
```

## Output

Findings as `path:line — severity — problem — fix` (no praise fluff).
