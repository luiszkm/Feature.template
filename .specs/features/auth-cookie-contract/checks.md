# Refresh em cookie httpOnly e guarda de contrato - checks

Profile: ui
Plan: `.specs/features/auth-cookie-contract/plan.md`

## Intent

18 checks in 3 slices · 6 one-way doors · 0 open

## Checks

### S1 - Refresh token fora do alcance do browser · 12 ficheiros · ~40 KB · ~10k

**C1** - O login devolve `Set-Cookie` com `pt_refresh`, `httponly`, `samesite=strict` e `path=/api/v1/identity` (AUTH-01, AC 1)
Proof: `dotnet test tests/E2ETests --filter "FullyQualifiedName~IdentityAuthE2ETests.Login_ShouldSetHttpOnlyRefreshCookie"`

**C2** - O corpo do login não traz a propriedade `refreshToken` (AUTH-01, AC 1)
Proof: `dotnet test tests/E2ETests --filter "FullyQualifiedName~IdentityAuthE2ETests.Login_ShouldNotReturnRefreshTokenInBody"`

**C3** - O refresh usa o valor do cookie e devolve um `pt_refresh` diferente do anterior (AUTH-01, AC 2)
Proof: `dotnet test tests/E2ETests --filter "FullyQualifiedName~IdentityAuthE2ETests.Refresh_ShouldRotateTheCookie"`

**C4** - Um refresh sem o cookie `pt_refresh` devolve `401` (AUTH-01, AC 3)
Proof: `dotnet test tests/E2ETests --filter "FullyQualifiedName~IdentityAuthE2ETests.Refresh_WithoutCookie_ShouldReturn401"`

**C5** - O logout com cookie devolve `204`, apaga o cookie e o refresh seguinte com o token antigo devolve `401` (AUTH-01, AC 4)
Proof: `dotnet test tests/E2ETests --filter "FullyQualifiedName~IdentityAuthE2ETests.Logout_ShouldRevokeTheRefreshToken"`

**C6** - O logout sem cookie devolve `204` e não altera nenhum registo (AUTH-01, AC 5)
Proof: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~LogoutTests.Handle_ShouldNoOp_WhenTokenIsMissing"`

**C7** - O cookie leva `Secure` quando o pedido chega por HTTPS e não o leva em HTTP simples (AUTH-01, AC 6)
Proof: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~RefreshCookieTests.Options_ShouldSetSecure_ByScheme"`

**C8** - Toda a requisição do front para `/api/v1/**` leva `withCredentials` (AUTH-01, AC 7)
Proof: `cd src/web && npx vitest run src/app/core/http/api.interceptor.spec.ts -t "envia withCredentials"`

**C9** - Depois do login, `localStorage['pt.auth']` contém `tenantKey` e `user` e nenhum campo de token (AUTH-01, AC 8)
Proof: `cd src/web && npx vitest run src/app/features/identity/login.spec.ts -t "guarda a sessao e navega para users"`

**C10** - O refresh do front é emitido sem corpo com token e a sessão sobrevive (AUTH-01, AC 2)
Proof: `cd src/web && npx vitest run src/app/core/http/api.interceptor.spec.ts -t "401 renova e repete"`

**C11** - Sair chama `POST /api/v1/identity/logout` antes de limpar o estado local (AUTH-01, AC 4)
Proof: `cd src/web && npx vitest run src/app/shell/shell.spec.ts -t "logout chama a API"`

**C12** - Contra a API real, recarregar a página renova a sessão sem nenhum token em `localStorage` (AUTH-01, AC 8)
Proof: `cd src/web && npx playwright test e2e/auth.spec.ts -g "renova o token expirado"`

### S2 - Contrato com autoridade fora de Development · 4 ficheiros · ~12 KB · ~3k

**C13** - O documento servido pelo host de testes é idêntico ao `src/Api/openapi.json` versionado, e cobre as 30 rotas de `features.json` (CONTRACT-01, AC 9)
Proof: `dotnet test tests/E2ETests --filter "FullyQualifiedName~OpenApiDocumentTests.Document_ShouldMatch_TheCommittedContract"`
Proof: `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~OpenApiContractTests.Document_ShouldBeGenerated_AtBuildTime"`

**C14** - Uma rota de `features.json` ausente de `openapi.json` falha os testes de arquitetura (CONTRACT-01, AC 10)
Proof: `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~OpenApiContractTests.EveryFeatureRoute_ShouldExist_InTheDocument"`

**C15** - Uma rota `/api/v1/**` do documento ausente de `features.json` falha os testes de arquitetura (CONTRACT-01, AC 11)
Proof: `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~OpenApiContractTests.EveryDocumentedRoute_ShouldExist_InFeaturesJson"`

**C16** - Um caminho chamado por um cliente do front e ausente de `openapi.json` falha `npm test` (CONTRACT-01, AC 12)
Proof: `cd src/web && npx vitest run src/app/architecture.spec.ts -t "clientes so chamam rotas documentadas"`

### S3 - Solução compilável na raiz · 1 ficheiro · ~2 KB · ~1k

**C17** - `dotnet build` na raiz compila os quatro projetos e sai com código zero (BUILD-01, AC 13)
Proof: `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~SolutionFileTests.Solution_ShouldListEveryProject"`

**C18** - Os GUIDs de projeto da solução são os que `.template.config/template.json` regenera (BUILD-01, AC 13)
Proof: `dotnet test tests/ArchitectureTests --filter "FullyQualifiedName~SolutionFileTests.ProjectGuids_ShouldMatch_TemplateConfig"`

## Coverage

Uma set row por rota de `Surface`; os membros são os statuses. Os statuses que o interceptor do
front já cobre (`401` fora do caminho de refresh, `429`) apoiam-se nos checks da feature
`web-frontend`, que não foram alterados por este trabalho.

| Set (size) | Member -> proof | Unproven |
| --- | --- | --- |
| `POST /api/v1/identity/login` statuses (5) | 200 C1 · 400 web-frontend C3 · 401 web-frontend C2 · 409 web-frontend C15 · 429 web-frontend C13 | - |
| `POST /api/v1/identity/refresh` statuses (5) | 200 C3 · 401 C4 · 404 web-frontend C14 · 409 web-frontend C15 · 429 web-frontend C13 | - |
| `POST /api/v1/identity/logout` statuses (2) | 204 C5 · 429 existing - mesma política `auth` de login e refresh, provada por web-frontend C13 | - |
| atributos do cookie (5) | `HttpOnly` C1 · `SameSite=Strict` C1 · `Path=/api/v1/identity` C1 · `Max-Age` C1 · `Secure` C7 | - |
| one-way doors do plano (6) | cookie de refresh C1 · corpo sem token C2 · rota de logout C5 · documento no build C13 · documento versionado C13 · solução na raiz C17 | - |
| direções de drift do contrato (2) | features.json -> documento C14 · documento -> features.json C15 | - |
| superfícies do front tocadas (3) | interceptor C8 · sessão C9 · shell C11 | - |
| bootstrap: providers HTTP (3 montagens) | `app.config.ts` C8 · setup do Vitest C8 · build servido ao Playwright C12 | - |

- Claims que nomeiam status code, rota ou forma de resposta: C1, C2, C3, C4, C5, C6, C10, C11 - cada um tem uma prova que atravessa a fronteira HTTP
- Nenhum outro check reclama mais do que o único caso que a sua prova exercita

## Test policy

As linhas aprovadas em `.specs/features/web-frontend/checks.md` continuam a valer e não são
reabertas. O que este trabalho acrescenta é código de decisão em dois sítios, e ambos caem na
primeira linha da tabela:

| Code | Required proofs | Coverage expectation |
| --- | --- | --- |
| Decide, atravessado por uma fronteira | uma na fronteira **e** uma no seu próprio nível | o contrato na fronteira; um caso asserido por linha da tabela de decisão no seu próprio nível |
| Decide, não atravessado por uma fronteira | uma no seu próprio nível | um caso asserido por linha da tabela de decisão |
| Ponto de entrada que não decide | uma na fronteira | entrada aceite, cada entrada rejeitada, cada caminho de erro |
| Instrumentação, pass-through | nenhuma própria | coberto pela prova do consumidor |

Evidence:

- `src/Api/Features/Identity/RefreshCookie.cs`: decide `Secure` pelo esquema do pedido e monta os atributos; 2 pontos de ramificação -> decide, atravessado pela fronteira HTTP
- `src/Api/Features/Identity/Logout.cs`: decide entre revogar e não fazer nada consoante o cookie; 2 pontos de ramificação -> decide, atravessado pela fronteira HTTP
- `src/Api/Features/Identity/Login.cs`, `RefreshAccessToken.cs`: os endpoints passam a escrever o cookie mas não decidem nada de novo -> instrumentação sobre handlers já provados
- análogo mais próximo no repositório: `src/Api/Host/Security/SecurityConfiguration.cs` decide `RequireHttpsMetadata` pelo ambiente e é provado em `tests/Api.Tests/Host/FailFastConfigurationTests.cs` - mesma forma, mesmo tratamento

Cost: 2 provas no próprio nível em 2 ficheiros (`RefreshCookieTests`, `LogoutTests`). Sem elas, a
decisão de `Secure` e o caminho sem cookie ficam provados apenas por um teste E2E que atravessa
um dos dois ramos.

## Swept

- validation: existing - `RefreshTokenValidator` continua a validar o valor, que agora chega do cookie em vez do corpo
- failure modes: C4, C6 - pedido sem cookie no refresh e no logout, os dois caminhos onde o valor pode faltar
- idempotency: C5, C6 - dois logouts seguidos devolvem `204` e só o primeiro revoga
- authorization: C1, C5 - o cookie é a credencial; `HttpOnly` tira-a do alcance de script, e o logout revoga o token apresentado e mais nenhum
- concurrency: existing - a fila única de refresh do front (`web-frontend` C7) não muda com o transporte; o cookie é escrito pela resposta que a fila já serializa
- data lifecycle: C5 - o `Max-Age` do cookie segue `RefreshTokenExpirationDays`, e o logout apaga-o antes disso
- dependency failure: n/a - este trabalho não acrescenta dependência externa; o documento OpenAPI é gerado pelo próprio build
- state transitions: C3, C5 - emitido -> rodado -> revogado, com o reuso do token antigo a falhar em `401`
- observability: n/a - nenhum critério pede telemetria nova; o logout reutiliza o logging do pipeline existente

## Handoff

- S1 ~10k + S2 ~3k + S3 ~1k = ~14k, mais ~15k de contexto já lido (contratos de Identity, testes existentes, workspace do front) = ~29k, abaixo do orçamento de 150k -> um único builder, sem handoff
