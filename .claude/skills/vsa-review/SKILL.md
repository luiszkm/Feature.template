---
name: vsa-review
version: 1
description: "Review a VSA change for Product.Template. TRIGGER: \"review slice\", \"review PR\", \"architecture check\". SKIP: scaffolding (use /new-slice or /new-module)."
disable-model-invocation: true
---

# Skill: /vsa-review

See also: `docs/architecture/vsa.md` · `docs/architecture/guidelines.md`

## Checklist

0. Module has `Features/{Module}/AGENTS.md`; changes to public surface reflected there
1. Flat slice file; no layer folders; noun vs verb naming
2. No `Program.cs` edits for endpoints; `IEndpoint` + module registration only when needed
3. Repo: per-aggregate interface; no public `IRepository<T>`
4. Auth: named policies only; tenant/`security_stamp` if Identity touch
5. `features.json` entry matches app/test/route/flag/policy
6. Tests mirror under `tests/Api.Tests/{Module}/`; no tests in `src/Api/`
7. `src/Api/openapi.json` regenerated in the same change (`UPDATE_OPENAPI=1 dotnet test tests/E2ETests`)
8. New route has a front client under `src/web/src/app/features/{module}/`, and the screen states it needs

## Verify

```bash
dotnet build
dotnet test tests/ArchitectureTests
dotnet test tests/Api.Tests --filter <SliceOrModule>
cd src/web && npm test
```

## Output

Findings as `path:line — severity — problem — fix` (no praise fluff).
