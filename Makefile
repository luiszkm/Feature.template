.PHONY: build test verify verify-all health docker-up web-verify web-e2e

build:
	dotnet build

test:
	dotnet test tests/ArchitectureTests
	dotnet test tests/Api.Tests
	dotnet test tests/E2ETests

verify: build test

# Everything a PR touches: API, contract guards and the front.
verify-all: verify web-verify

health:
	curl -sf http://localhost:5000/health/live || curl -sf http://localhost:5080/health/live

docker-up:
	docker compose up -d

web-verify:
	cd src/web && npm ci && npm test && npm run build

# Browser e2e hits the compose API. Development + EnableAI keeps StubLlmService
# (Production + EnableAI without a key fails fast). compose.env is interpolation
# input when present; CI passes the same vars in the job env instead.
COMPOSE_ENV_FILE := $(wildcard compose.env)
COMPOSE_ENV_ARGS := $(if $(COMPOSE_ENV_FILE),--env-file compose.env,)

web-e2e:
	ASPNETCORE_ENVIRONMENT=Development \
	FEATURE_FLAGS_ENABLE_AI=true \
	CORS_ORIGIN=http://localhost:4200 \
	SEED_ADMIN_EMAIL=$${SEED_ADMIN_EMAIL:-admin@producttemplate.com} \
	SEED_ADMIN_PASSWORD=$${SEED_ADMIN_PASSWORD:-Admin@123} \
	docker compose $(COMPOSE_ENV_ARGS) up -d --build
	@ok=0; \
	for i in $$(seq 1 60); do \
	  if curl -sf http://localhost:5080/health/ready >/dev/null; then ok=1; break; fi; \
	  sleep 5; \
	done; \
	if [ "$$ok" != "1" ]; then docker compose logs api; exit 1; fi
	cd src/web && npm ci && npx playwright install --with-deps chromium && npm run e2e
