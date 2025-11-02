# Integration Tests

This directory contains integration tests that verify the interaction between services and their dependencies (like Kafka message broker).

## Running Tests

### Unit Tests Only (Default)

```powershell
dotnet test
```

By default, integration tests are skipped to keep the test runs fast and reliable in CI/local development.

### With Integration Tests

1. Start the required infrastructure:

```powershell
# From repository root
docker compose up -d
```

2. Run tests with integration tests enabled:

```powershell
$env:ENABLE_KAFKA_INTEGRATION_TESTS = "true"
dotnet test
```

### GitHub Actions Example

To run integration tests in CI:

```yaml
name: Integration Tests
on: [push]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      kafka:
        image: confluentinc/cp-kafka:latest
        ports:
          - 9092:9092
        env:
          KAFKA_ADVERTISED_LISTENERS: PLAINTEXT://kafka:9092
          KAFKA_LISTENER_SECURITY_PROTOCOL_MAP: PLAINTEXT:PLAINTEXT
          KAFKA_INTER_BROKER_LISTENER_NAME: PLAINTEXT
          KAFKA_OFFSETS_TOPIC_REPLICATION_FACTOR: 1

    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: 9.0.x

      - name: Run Integration Tests
        env:
          ENABLE_KAFKA_INTEGRATION_TESTS: "true"
        run: dotnet test
```
