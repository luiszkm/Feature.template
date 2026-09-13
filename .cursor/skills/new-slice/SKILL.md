---
name: new-slice
version: 1
description: "Scaffold a VSA slice in Product.Template v2. TRIGGER: \"new slice\", \"add slice\", \"create endpoint\". SKIP: use /new-module for new bounded context."
disable-model-invocation: true
---

# Skill: /new-slice

Arguments: `{Module} {Slice}` — e.g. `/new-slice Identity Login`

Read `src/Api/Features/{Module}/AGENTS.md` first. See also `docs/architecture/guidelines.md`.

## Creates (always both)

```
src/Api/Features/{Module}/{Slice}.cs
tests/Api.Tests/{Module}/{Slice}Tests.cs
```

Update `features.json`:

```json
"{Slice}": {
  "m": "{Module}",
  "app": "src/Api/Features/{Module}/{Slice}.cs",
  "test": "tests/Api.Tests/{Module}/{Slice}Tests.cs",
  "route": "METHOD /api/v1/...",
  "featureFlag": null,
  "policy": "..."
}
```

Regenerate the contract and add the front client — both are enforced:

```bash
UPDATE_OPENAPI=1 dotnet test tests/E2ETests    # src/Api/openapi.json
```

```
src/web/src/app/features/{module}/{slice}.ts   # npm test fails without a client
```

## {Slice}.cs template

```csharp
namespace Api.Features.{Module};

public sealed record {Slice}Command(...) : ICommand<{Slice}Response>;
public sealed record {Slice}Response(...);
public sealed class {Slice}Validator : AbstractValidator<{Slice}Command> { }
public sealed class {Slice}Handler(...) : IRequestHandler<{Slice}Command, {Slice}Response> { }
public sealed class {Slice}Endpoint : IEndpoint
{
    public void Map(IEndpointRouteBuilder app)
    {
        app.MapPost("...", ...)
           // .RequireFeature(FeatureFlags.X)  // if gated
           .WithName("{Slice}");
    }
}
```

## Rules

- Update `Features/{Module}/AGENTS.md` if slice list or public surface changed
- Do NOT edit `Program.cs`
- Entity file = noun only (`User.cs`), slice = verb (`Login.cs`)
- Tests: validator + handler sections in one `{Slice}Tests.cs`

## Verify

```bash
dotnet build
dotnet test tests/Api.Tests --filter {Slice}
dotnet test tests/ArchitectureTests          # features.json <-> openapi.json, both directions
cd src/web && npm test                       # route coverage + layer folders
```
