# Profile Microservice — Chapter 12 resiliency review

Reviewed against Sam Newman's *Building Microservices*, 2nd ed., Chapter 12 (pp. 387–417).

## Architecture and failure points

| ID | Location | Failure and impact | Priority | Mitigation / status |
| --- | --- | --- | --- | --- |
| F1 | `src/Messaging/MessageHandlerHostedService.cs` | RabbitMQ is offline, a subscription call fails, or the call hangs during host startup. The hosted service originally awaited it in `StartAsync`, so the REST API could fail to start or wait indefinitely. | Critical | **Implemented:** move the subscription to a background worker, apply a 5-second timeout, retry failures with capped exponential backoff (250 ms up to 10 s), log failures, cancel during shutdown. |
| F2 | `src/Messaging/MessageClient.cs` / `src/Handlers/MessagePostedHandler.cs` | Publishing `ProfileActivityUpdatedEvent` can wait too long for RabbitMQ. This ties up message processing, and an uncertain publish outcome can cause duplicate delivery after retry/redelivery. | High | **Implemented:** 5-second outbound publish timeout, propagate errors. Do **not** blindly retry publishing when the result is unknown. A durable outbox and consumer idempotency are future work. |
| F3 | `src/Services/ProfileService.cs` | All profiles are held in a singleton `ConcurrentDictionary`. A process/container restart loses all profiles and `LastMessageAt` values; multiple replicas would not share state. | Critical | **Not implemented:** adopt service-owned durable storage, migration strategy, backups and restore tests. This project currently has **no database dependency** to test for slowness/unavailability. |
| F4 | `src/Handlers/MessagePostedHandler.cs` | Message redelivery updates the timestamp monotonically, but can publish multiple `ProfileActivityUpdatedEvent` events for one `MessageId`. An update followed by failed publication is not atomic. | High | **Not implemented:** durable inbox/outbox with an idempotency key (`MessageId`), plus downstream deduplication. The current timestamp update is only partially idempotent. |
| F5 | `docker-compose.yml` | `depends_on: condition: service_healthy` helps initial startup order but does not ensure RabbitMQ stays healthy or that connections remain available. | Medium | Background subscription retry reduces startup coupling. EasyNetQ must still be validated for reconnection **after** an active connection drops. |
| F6 | `src/EndPoints/ProfileEndpoints.cs` | A slow or failed future database dependency would make CRUD endpoints slow or unavailable. Today the REST controller calls in-memory storage and does **not** make synchronous interservice network calls. | Future | When adding persistence, set database connection/command timeouts, use a bounded pool, reject writes on outage, and test degraded behavior. Do not add network retries to methods that do not make network calls. |

## Why these patterns?

- **Bulkhead / isolation and graceful degradation:** the RabbitMQ subscription is moved off the API startup path. Profile CRUD can continue without the broker, while activity propagation may lag.
- **Timeout:** individual subscription and publish operations have a 5-second upper wait limit in application code. A timeout indicates an uncertain downstream outcome, not proof that a message was not delivered.
- **Retry:** retry subscription setup because reconnecting is safe and transient broker/network failures are expected. Delays increase exponentially with a 10-second cap to avoid hammering RabbitMQ.
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

### Manual outage / recovery experiment

1. Stop RabbitMQ: `docker compose stop rabbitmq`.
2. Start the Profile Microservice **outside Docker Compose**, e.g. `dotnet run --project apps/Profile-Microservice/src` (with the default `host=localhost` broker setting). Unlike `docker compose up`, this bypasses `depends_on` so a broker-down startup can actually be tested.
3. While RabbitMQ is stopped, request `GET http://localhost:5210/api/profiles`. Expect HTTP 200 from the API, and retry warnings in the app logs. Message subscriptions are unavailable during this outage.
4. Restart RabbitMQ: `docker compose start rabbitmq`. Expect a later subscription retry to succeed, then an informational subscription log.
5. Run the real-broker integration test to check that a `MessagePostedEvent` is consumed and produces `ProfileActivityUpdatedEvent`.
6. Stop RabbitMQ **after** successful subscription and restart it. Verify EasyNetQ's active-connection recovery separately; this patch retries **initial setup failures**, not an undetectable post-subscription disconnect.

### Still-unresolved tests

- Restart the application after creating a profile: the profile disappears (demonstrates F3, not a passing durability test).
- Deliver the same `MessagePostedEvent` twice: `LastMessageAt` should not move backward, but outgoing event deduplication is not guaranteed (F4).
- Force publishing to stall or become unavailable: observe a timeout/failure rather than indefinite waiting, and verify the event-handling semantics with an end-to-end recovery test.
