# Profile Microservice — Chapter 12 resiliency review

Reviewed against Sam Newman's *Building Microservices*, 2nd ed., Chapter 12 (pp. 387–417).

## Architecture and failure points

| ID | Location | Failure and impact | Priority | Mitigation / status |
| --- | --- | --- | --- | --- |
| F1 | `src/Messaging/MessageHandlerHostedService.cs` | RabbitMQ is offline, a subscription call fails, or the call hangs during host startup. The hosted service originally awaited it in `StartAsync`, so the REST API could fail to start or wait indefinitely. | Critical | **Implemented:** move the subscription to a background worker, apply a 5-second timeout, retry failures with capped exponential backoff (250 ms up to 10 s), log failures, cancel during shutdown. The Rebus saga bus had the same startup coupling: **Implemented:** `src/Messaging/RebusStartupService.cs` checks RabbitMQ with a login attempt (5-second connection timeout), retries with exponential backoff (250 ms up to 10 s) and creates the bus only after a successful login. Until then, `POST /api/profiles/{id}/ban` returns 503 without banning the profile. If bus creation still fails, it logs the error, sets exit code 1 and stops the host; Compose has `restart: on-failure` (this failure path is not tested). |
| F2 | `src/Messaging/MessageClient.cs` / `src/Handlers/MessagePostedHandler.cs` | Publishing `ProfileActivityUpdatedEvent` can wait too long for RabbitMQ. This ties up message processing, and an uncertain publish outcome can cause duplicate delivery after retry/redelivery. | High | **Implemented:** 5-second outbound publish timeout, propagate errors. Do **not** blindly retry publishing when the result is unknown. A durable outbox and consumer idempotency are future work. |
| F3 | `src/Services/ProfileService.cs` | All profiles are held in a singleton `ConcurrentDictionary`. A process/container restart loses all profiles and `LastMessageAt` values; multiple replicas would not share state. | Critical | **Not implemented:** adopt service-owned durable storage, migration strategy, backups and restore tests. This project currently has **no database dependency** to test for slowness/unavailability. |
| F4 | `src/Handlers/MessagePostedHandler.cs` | Message redelivery updates the timestamp monotonically, but can publish multiple `ProfileActivityUpdatedEvent` events for one `MessageId`. An update followed by failed publication is not atomic. | High | **Not implemented:** durable inbox/outbox with an idempotency key (`MessageId`), plus downstream deduplication. The current timestamp update is only partially idempotent. |
| F5 | `docker-compose.yml` | `depends_on: condition: service_healthy` helps initial startup order but does not ensure RabbitMQ stays healthy or that connections remain available. | Medium | Background subscription retry and background Rebus bus startup reduce startup coupling. EasyNetQ and Rebus must still be validated for reconnection **after** an active connection drops. |
| F6 | `src/EndPoints/ProfileEndpoints.cs` | A slow or failed future database dependency would make CRUD endpoints slow or unavailable. Today the REST controller calls in-memory storage and does **not** make synchronous interservice network calls. | Future | When adding persistence, set database connection/command timeouts, use a bounded pool, reject writes on outage, and test degraded behavior. Do not add network retries to methods that do not make network calls. |
| F7 | `src/Program.cs` / `docker-compose.yml` | When `Vault:Address` is set (as in Compose), Profile reads its secret from Vault during startup. If Vault is sealed or unavailable, startup throws and Profile stops. The code has no retry, and Compose only waits for `service_started`, not for an unsealed Vault. `apps/Vault/config.hcl` configures no auto-unseal. | High | **Not implemented:** startup retry, a health check that requires an unsealed Vault, or auto-unseal. `restart: on-failure` restarts the container, but does not unseal Vault. Manual steps are in section 5.5 of `docs/testing-guide.md`. Not tested in this update. |
| F8 | `apps/Gateway/src/ocelot.json` / `docker-compose.yml` | Compose runs one gateway, and Profile has no published port, so a gateway outage cuts off client access to Profile. `ocelot.json` has no timeout or QoS settings, so slow downstream calls are not limited by project configuration. Ocelot's default behaviour has not been checked. | High | **Not implemented:** explicit downstream timeouts/QoS, and redundant gateway instances behind a load balancer. Not tested. |
| F9 | `src/Sagas/BanSaga.cs` / `src/Messaging/RebusServiceCollectionExtensions.cs` | The saga uses an in-memory saga store (`StoreInMemory`) and has no saga timeout. A restart loses saga state, and `BanSagaTracker` is in memory too. A saga whose reply never arrives has no time limit. Replies arriving after a restart are assumed not to find their saga. | High | **Not implemented:** durable saga storage and saga timeouts. Not tested. |
| F10 | `src/EndPoints/ProfileEndpoints.cs` (`Ban`) | The ban is saved locally first, and `UserBannedEvent` is then published without an outbox. If `Publish` fails while the process keeps running, the profile can stay banned without a saga, and a new ban request returns 409. A crash between the two steps currently also loses the in-memory profile (F3); with durable profile storage alone, it could leave a banned profile without a saga. The readiness check does not close this gap. | High | **Not implemented:** durable profile storage combined with an outbox. Not tested. |
| F11 | `docker-compose.yml` (`JWT_SIGNING_KEY`) | Auth, Gateway and Profile share one symmetric JWT key (Auth signs with HMAC-SHA256). If the key leaks from any of the three services, it can be used to forge tokens, including the `internal-service` role. | High | **Not implemented:** asymmetric signing, where only Auth holds the private key and Gateway and Profile validate with the public key. |

## Why these patterns?

- **Bulkhead / isolation and graceful degradation:** the RabbitMQ subscription and the Rebus saga bus are moved off the API startup path. Profile CRUD does not call the broker, while activity propagation may lag. `ban` returns 503 until the saga bus has started.
- **Timeout:** individual subscription and publish operations have a 5-second upper wait limit in application code. A timeout indicates an uncertain downstream outcome, not proof that a message was not delivered.
- **Retry:** retry subscription setup because reconnecting is safe and transient broker/network failures are expected. Delays increase exponentially with a 10-second cap to avoid hammering RabbitMQ. The broker check before Rebus bus creation uses the same delays; bus creation itself is not retried in-process.
- **No automatic publish retry:** publication may have succeeded despite the client timing out. Automatic retries would increase duplicate-event risk until durable idempotency/outbox support is added.
- **Circuit breaker:** not chosen for this first iteration. There are no synchronous REST calls to downstream microservices in the current Profile service. The asynchronous broker path benefits most from isolated startup, bounded waits, and retrying subscription setup.

## Testing and simulation

### Automated tests (no RabbitMQ required)

```bash
dotnet test apps/Profile-Microservice/tests --filter FullyQualifiedName~MessageHandlerHostedServiceTests
```

1. Simulate one failed broker subscription, then a successful attempt. Expect two attempts, successful subscription, and clean disposal on shutdown.
2. Simulate a subscription that waits until cancelled. Expect `StartAsync` to complete promptly, no false `WhenSubscribed` signal, and `StopAsync` to complete cleanly.

### Integration tests (real RabbitMQ)

```bash
docker compose up rabbitmq -d
dotnet test apps/Profile-Microservice/tests --filter Category=Integration
```

The integration test waits for the **first** successful subscription before publishing its test event; this prevents an asynchronous-startup race.

The two test classes that start a Profile host (`GraphQLTests` and `MessagePostedIntegrationTests`) are in the xUnit collection `Profile host`, so they do not run in parallel; they previously competed for the same RabbitMQ queue. Five consecutive runs of the whole suite returned exit code 0. Stop other Profile instances before running these tests (see `docs/testing-guide.md`).

### Manual outage / recovery experiment

The full PowerShell commands are in section 5.3 of [`docs/testing-guide.md`](../../docs/testing-guide.md).

1. Start the stack, stop RabbitMQ, then restart only Profile: `docker compose up --build -d`, `docker compose stop rabbitmq`, `docker compose restart profilemicroservice`. Restarting the single service lets a broker-down startup be tested.
2. Wait about 20 seconds. Expect the container to be `Up`, log lines `RabbitMQ is not ready for the Rebus saga bus. Retrying in {Delay}.` (with the actual delay), and no `Hosting failed`. Measured with a new image: `Up`, 3 retry lines, 0 `Hosting failed`.
3. While RabbitMQ is stopped, create a profile through the gateway (`http://localhost:8000`, with a token), call `ban` on it and read it back. Expect creation to work, `ban` to return 503 with `The ban saga is not ready yet. Try again shortly.`, and `isBanned` to be `False`. The 503 and `isBanned=False` were measured with `dotnet run` without RabbitMQ; this step has not been tested through the gateway.
4. Restart RabbitMQ: `docker compose start rabbitmq`, and wait about 60 seconds. Expect `Rebus saga bus started.` twice in the log (first startup plus recovery; measured). Then expect `ban` to return 202 (not tested through the gateway). In the `dotnet run` measurement, a later ban reached `Completed`.
5. Stop other Profile instances (`docker compose stop profilemicroservice`), then run the real-broker integration test to check that a `MessagePostedEvent` is consumed and produces `ProfileActivityUpdatedEvent`.
6. Stop RabbitMQ **after** successful subscription and bus startup, then restart it. Verify EasyNetQ's and Rebus' active-connection recovery separately (not tested); the retries cover **initial setup**, not a later disconnect.

### Still-unresolved tests

- Restart the application after creating a profile: the profile disappears (demonstrates F3, not a passing durability test).
- Deliver the same `MessagePostedEvent` twice: `LastMessageAt` should not move backward, but outgoing event deduplication is not guaranteed (F4).
- Force publishing to stall or become unavailable: observe a timeout/failure rather than indefinite waiting, and verify the event-handling semantics with an end-to-end recovery test.

## Limitations

- Behaviour when RabbitMQ disappears **after** the Rebus bus has started has not been tested. The code never resets `RebusReadiness`, so this readiness check will not make `ban` return 503 in that case (see F10).
- In-process retry is not possible after a failed Rebus bus creation: according to the code comment in `RebusStartupService`, Rebus remembers the failure. The app therefore stops with exit code 1. `restart: on-failure` applies in Docker Compose, not to `dotnet run`. This failure path is not tested.
