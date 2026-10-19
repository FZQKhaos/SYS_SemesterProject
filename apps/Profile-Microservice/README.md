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
| `MessagePostedEvent`          | Consumed  | Subscription id `profile-microservice`. Unknown authors are ignored. `LastMessageAt` only moves forward, so redelivery is harmless. |
| `ProfileActivityUpdatedEvent` | Published | Contains `MessageId`, `ProfileId`, `LastMessageAt`, `ProcessedAt`. |

Both types live in `Shared/Bizcord.Shared/Events`.

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
| Contract    | `MessagePostedHandlerTests`       | Handler accepts the minimum valid `MessagePostedEvent`. |
| Integration | `MessagePostedIntegrationTests`   | Event published on the real broker is consumed and the result event is published. |