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

web-e2e:
	docker compose up -d --build
	cd src/web && npm ci && npx playwright install --with-deps chromium && npm run e2e
