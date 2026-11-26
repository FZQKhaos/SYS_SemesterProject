# Profile Microservice

Bounded context: **User Profiles**. Owns display names and profile activity for Bizcord users.
Design rationale: see [`docs/analysis-and-design.md`](../../docs/analysis-and-design.md).

## Responsibilities

- CRUD for profiles (REST).
- Consumes `MessagePostedEvent` and records the author's last activity (`LastMessageAt`).
- Publishes `ProfileActivityUpdatedEvent` after processing a message.

## API

| Method | Route                | Description        |
|--------|----------------------|--------------------|
| GET    | `/api/profiles`      | List all profiles  |
| GET    | `/api/profiles/{id}` | Get one profile    |
| POST   | `/api/profiles`      | Create a profile   |
| PUT    | `/api/profiles/{id}` | Update display name|
| DELETE | `/api/profiles/{id}` | Delete a profile   |

## Events

| Event                         | Direction | Notes |
|-------------------------------|-----------|-------|
| `MessagePostedEvent`          | Consumed  | Subscription id `profile-microservice`. Unknown authors are ignored. `LastMessageAt` only moves forward, so redelivery does not move it backward, but outgoing duplicate events are still possible. |
| `ProfileActivityUpdatedEvent` | Published | Contains `MessageId`, `ProfileId`, `LastMessageAt`, `ProcessedAt`. |

Both types live in `Shared/Bizcord.Shared/Events`.

## Resiliency review (Chapter 12)

See [FAILURE-POINTS.md](FAILURE-POINTS.md) for identified failure points, the selected resilience patterns, implementation details, limitations, and outage simulation instructions.

The host does not wait for RabbitMQ at startup:

- **EasyNetQ subscription:** set up in the background with bounded setup attempts and exponential-backoff retries.
- **Rebus saga bus:** started in the background once a RabbitMQ login succeeds, with exponential-backoff retries (250 ms up to 10 s). Until the bus is ready, `POST /api/profiles/{id}/ban` returns 503 and the profile is not banned. If bus creation still fails, the app logs the error, sets exit code 1 and stops as a last resort; in Docker Compose, `restart: on-failure` restarts the container (this failure path is not tested).

See [FAILURE-POINTS.md](FAILURE-POINTS.md) for what has and has not been tested, and section 5.3 of [`docs/testing-guide.md`](../../docs/testing-guide.md) for the RabbitMQ outage test. Outbound EasyNetQ publishes have a bounded timeout; **a timeout is not proof of non-delivery**, and this version does not guarantee durable messages or exactly-once processing. Profiles are still held in memory and are lost on process restart.

## Run with Docker

From the repository root:

```bash
docker compose up --build
```

- API: http://localhost:8000/api/profiles
- RabbitMQ management UI: http://localhost:15672 (user `profileapp`, password `profileapp`)

Configuration is read from the environment variables `RABBITMQ_HOST`, `RABBITMQ_USER` and `RABBITMQ_PASSWORD`.

## Run tests

Start the broker first (the integration test needs a real RabbitMQ on port 5672):

```bash
docker compose up rabbitmq -d
dotnet test apps/Profile-Microservice/tests
```

| Test type   | File                              | What it proves |
|-------------|-----------------------------------|----------------|
| Unit        | `MessagePostedHandlerTests`       | Handler publishes a result event with the correct `MessageId`; unknown author publishes nothing. |
| Unit        | `MessageHandlerHostedServiceTests`| Temporary RabbitMQ subscription failure recovers; a hung attempt does not block API startup or shutdown. |
| Contract    | `MessagePostedHandlerTests`       | Handler accepts the minimum valid `MessagePostedEvent`. |
| Integration | `MessagePostedIntegrationTests`   | Event published on the real broker is consumed and the result event is published. |
