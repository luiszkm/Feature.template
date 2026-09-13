# AGENTS.md — src/web (Angular 22)

Front-end do template. Espelha a VSA do backend: **slice = ficheiro plano**, zero pastas de camada.

Stack: Angular 22 (zoneless, standalone, Signal Forms), Angular Material 22, Vitest + MSW, Playwright.

## Layout

```
src/web/src/app/
├── app.config.ts        # providers da aplicação — interceptor entra aqui
├── app.routes.ts        # rotas + guards
├── architecture.spec.ts # cobertura de rotas e proibição de pastas de camada
├── core/                # sessão, HTTP, guards, permissões
├── shared/              # estados de lista, diálogos, ProblemDetails, ecrãs genéricos
├── shell/               # barra + navegação
└── features/{module}/
    ├── {module}.contracts.ts   # DTOs da API (espelha {Module}Contracts.cs)
    └── {slice}.ts              # store + cliente HTTP + componente, no mesmo ficheiro
```

## Regras

- **Sem pastas de camada** sob `features/`: `services`, `components`, `models`, `pages`, `dtos`, `interfaces` fazem `npm test` falhar.
- **Toda a rota de `features.json` precisa de cliente** em `src/app/**`, senão `npm test` falha.
- **Todo o caminho chamado tem de existir em `src/Api/openapi.json`**, senão `npm test` falha. Depois de mudar um endpoint: `UPDATE_OPENAPI=1 dotnet test tests/E2ETests`.
- URLs sempre `${API_BASE}/...` — é o prefixo por que o interceptor se guia.
- `X-Tenant` e `Authorization` só no interceptor; nenhum slice os acrescenta.
- `401` → renovação partilhada (`RefreshCoordinator`); nunca tratar `401` dentro de um slice.
- **Nenhum token em storage.** O refresh vive no cookie `pt_refresh` (`HttpOnly`, emitido pela API); `pt.auth` guarda apenas `tenantKey` e `user`. Todo o pedido leva `withCredentials`, senão o cookie não viaja.
- Sair chama `POST /api/v1/identity/logout` **antes** de limpar o estado local — limpar só o local deixaria o cookie utilizável.
- `403` → ecrã `forbidden`. Para tratar localmente, marcar o pedido com `LOCAL_403`.
- Erros da API são `ProblemDetails`/`ValidationProblemDetails` — usar `parseProblem` e `fieldError`.
- Mensagens de erro do servidor ficam fora de `<mat-error>`: Material só as mostra quando o próprio controlo está inválido.

## Testes

```bash
npm test          # Vitest + MSW (component, store, interceptor, arquitetura)
npm run build     # tsc strict + ng build
npm run e2e       # Playwright contra a API real em http://localhost:5080
```

- Specs usam MSW sobre XHR; os handlers vivem no próprio teste.
- Helpers em `src/testing.ts`: `settle`, `render`, `waitFor`, `provideRouteStub`, `authenticate`, `stubConfirm`.
- `provideRouteStub()` e `stubConfirm()` correm **antes** do primeiro `TestBed.inject`.
- O `@angular/compiler` é carregado pelos polyfills da configuração `test` em `angular.json`; sem isso, as bibliotecas parcialmente compiladas falham no TestBed.

## Dev

```bash
npm start                      # :4200, proxy de /api para http://localhost:5080
API_URL=http://localhost:5000 npm start   # contra `dotnet run`
```

Credenciais e tenant: `docs/guides/getting-started.md`.
