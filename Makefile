.PHONY: build test verify health docker-up

build:
	dotnet build

test:
	dotnet test tests/ArchitectureTests
	dotnet test tests/App.Tests
	dotnet test tests/E2ETests

verify: build test

health:
	curl -sf http://localhost:5000/health/live || curl -sf http://localhost:5080/health/live

docker-up:
	docker compose up -d