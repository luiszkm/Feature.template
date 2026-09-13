# Architecture docs

Documentação estável do **Product.Template v2.1** (template finalizado).

| Doc | Conteúdo |
|-----|----------|
| [vsa.md](vsa.md) | Layout, Host/Shared/Features, módulos, feature flags, harness AGENTS |
| [guidelines.md](guidelines.md) | Padrões de código: slices, repos, contracts, testes, anti-patterns |

## Outros docs

| Doc | Conteúdo |
|-----|----------|
| [../guides/getting-started.md](../guides/getting-started.md) | Onboarding, Docker, credenciais dev |
| [../security/RBAC_MATRIX.md](../security/RBAC_MATRIX.md) | Políticas e rotas protegidas |
| [../../AGENTS.md](../../AGENTS.md) | Harness global para agentes |
| [../../features.json](../../features.json) | Índice de slices (app, test, route, policy, flag) |
| [../../src/web/AGENTS.md](../../src/web/AGENTS.md) | Front Angular: VSA espelhada, sessão, harness de testes |
| [../../src/Api/openapi.json](../../src/Api/openapi.json) | Contrato versionado — autoridade entre API, índice e front |

## Harness por módulo

Ao trabalhar num bounded context, ler primeiro o AGENTS local:

- [Identity](../../src/Api/Features/Identity/AGENTS.md)
- [Authorization](../../src/Api/Features/Authorization/AGENTS.md)
- [Tenants](../../src/Api/Features/Tenants/AGENTS.md)
- [Ai](../../src/Api/Features/Ai/AGENTS.md)
