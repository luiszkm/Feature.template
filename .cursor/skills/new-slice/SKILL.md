---
name: new-slice
version: 1
description: "Scaffold a VSA slice in Product.Template v2. TRIGGER: \"new slice\", \"add slice\", \"create endpoint\". SKIP: use /new-module for new bounded context."
disable-model-invocation: true
---

# Skill: /new-slice

Arguments: `{Module} {Slice}` — e.g. `/new-slice Identity Login`

## Creates (always both)

```
src/App/Features/{Module}/{Slice}.cs
tests/App.Tests/{Module}/{Slice}Tests.cs
```

Update `features.json`:

```json
"{Slice}": {
  "m": "{Module}",
  "app": "src/App/Features/{Module}/{Slice}.cs",
  "test": "tests/App.Tests/{Module}/{Slice}Tests.cs",
  "route": "METHOD /api/v1/...",
  "featureFlag": null,
  "policy": "..."
}
```

## {Slice}.cs template

```csharp
namespace App.Features.{Module};

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

- Do NOT edit `Program.cs`
- Entity file = noun only (`User.cs`), slice = verb (`Login.cs`)
- Tests: validator + handler sections in one `{Slice}Tests.cs`

## Verify

```bash
dotnet build
dotnet test tests/App.Tests --filter {Slice}
dotnet test tests/ArchitectureTests
```
