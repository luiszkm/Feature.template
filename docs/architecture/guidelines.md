# Coding guidelines (VSA v2.1)

Padrões para slices, módulos e testes. Regras enforced: `.cursor/rules/architecture-vsa.mdc`, `tests/ArchitectureTests/`.

## Slice anatomy

Um ficheiro `{Slice}.cs` contém (quando aplicável):

```csharp
public sealed record {Slice}Command(...) : ICommand<{Output}>;
public sealed class {Slice}Validator : AbstractValidator<{Slice}Command> { }
public sealed class {Slice}Handler(...) : IRequestHandler<...> { }
public sealed class {Slice}Endpoint : IEndpoint { public void Map(...) { } }
```

Queries usam `IQuery<T>` em vez de `ICommand<T>`.

## Naming

| Tipo | Convenção | Exemplo |
|------|-----------|---------|
| Entidade / agregado | substantivo | `User.cs`, `Role.cs`, `Tenant.cs` |
| Caso de uso | verbo | `RegisterUser.cs`, `ListRoles.cs` |
| Módulo DI | `{Module}Module.cs` | `IdentityModule.cs` |
| Contracts cross-slice | `{Module}Contracts.cs` | `AuthContracts.cs` |
| Permissões | `{Module}Permissions.cs` | `IdentityPermissions.cs` |
| Output DTO | `{Entity}Output` ou contracts | `UserOutput`, `RoleOutput` |

## Repositórios

- Interface por agregado em `{Entity}.cs` — `I{Entity}Repository`
- Implementação `internal sealed` no mesmo ficheiro
- CRUD genérico via `Shared/EfRepositoryHelpers.cs` (`internal`)
- **Sem** `IRepository<T>` público
- `db.Set<T>()` — sem `DbSet` explícitos no `AppDbContext`
- Tenant filters: `ITenantQueryFilterConfigurator` no `{Module}Module.cs`

## Contracts e mappers

- DTOs partilhados entre slices: `{Module}Contracts.cs` (ex. `AuthContracts.cs`, `AuthorizationContracts.cs`)
- Mapper `internal static` no ficheiro de contracts ou output (ex. `UserMapper.ToOutput`)
- Preferir `UserOutput` sobre response DTOs duplicados por slice

## Auth e RBAC

- Endpoints protegidos: `.RequireAuthorization(SecurityPolicies.X)` (`Api.Shared`) — nunca `[Authorize]` bare
- Permissões canônicas: `{module}.{resource}.{action}` (lowercase, dot-separated)
- Matriz de rotas: `docs/security/RBAC_MATRIX.md`
- JWT: claims `security_stamp` e `tenant_id` validados no pipeline

## Testes

| Tipo | Local | Harness |
|------|-------|---------|
| Handler / validator | `tests/Api.Tests/{Module}/{Slice}Tests.cs` | `TestServiceFactory` + InMemory |
| Arquitetura | `tests/ArchitectureTests/` | NetArchTest |
| HTTP | `tests/E2ETests/` | `E2EWebApplicationFactory` |
| Contrato | `tests/E2ETests/Common/OpenApiDocumentTests.cs` | host em memória vs `src/Api/openapi.json` versionado |
| Front | `src/web/src/app/**/*.spec.ts` | Vitest + MSW (ver `src/web/AGENTS.md`) |

- Definir tenant antes de resolver handlers: `TestServiceFactory.SetTenant(provider, tenantId)`
- Nunca colocar testes em `src/Api/`

## Limites

- `Program.cs`: ≤40 linhas non-empty
- Slice: ~150 linhas — se exceder, extrair endpoint ou lógica para serviço interno do módulo
- `AGENTS.md` raiz: curto; detalhe de módulo em `Features/{Module}/AGENTS.md`

## Anti-patterns (nunca)

- Pastas por camada (`Handlers/`, `Validators/`, `Mappers/`, `Repositories/`)
- Subpasta por slice (`RegisterUser/RegisterUser.cs`)
- Vários handlers de verbo diferente no mesmo ficheiro
- Editar `Program.cs` para registar endpoint
- `CreateWithId` em agregados — `AggregateRoot` gera `Id`; seed por email/nome quando ID fixo não é necessário
- Generic `IRepository<T>` ou base repository exposta fora de `Shared/`

## Nova feature (checklist)

1. Ler `Features/{Module}/AGENTS.md`
2. Criar `Features/{Module}/{Slice}.cs`
3. Criar `tests/Api.Tests/{Module}/{Slice}Tests.cs`
4. Atualizar `features.json` (app, test, route, policy, featureFlag)
5. Regenerar o contrato: `UPDATE_OPENAPI=1 dotnet test tests/E2ETests` — `tests/ArchitectureTests` falha se `features.json` e `src/Api/openapi.json` divergirem, nos dois sentidos
6. Criar o cliente no front (`src/web/src/app/features/{module}/`) — `npm test` falha se uma rota de `features.json` não tiver cliente
7. Atualizar `RBAC_MATRIX.md` se endpoint protegido novo
8. Atualizar `Features/{Module}/AGENTS.md` se superfície pública mudou
9. **Não** editar `Program.cs`

Skills: `/new-slice {Module} {Slice}` · `/new-module {Module}` · `/vsa-review`
