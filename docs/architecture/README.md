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

## Harness por módulo

Ao trabalhar num bounded context, ler primeiro o AGENTS local:

- [Identity](../../src/App/Features/Identity/AGENTS.md)
- [Authorization](../../src/App/Features/Authorization/AGENTS.md)
- [Tenants](../../src/App/Features/Tenants/AGENTS.md)
- [Ai](../../src/App/Features/Ai/AGENTS.md)
