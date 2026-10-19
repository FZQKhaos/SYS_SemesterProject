# Analysis & Design: Profile Microservice (Bizcord)

## 1. Domain and context

Bizcord is a Discord-like real-time communication platform (backend only). The overall
scope is real-time communication, channel management, user profiles and engagement. Each
team owns one bounded context. **Our bounded context is User Profiles.** See
`diagrams/Bizcord microservices DDD.drawio` for the coarse system overview (Servers,
Profiles, Messaging).

## 2. Responsibility

The Profile microService is responsible for user profiles and their friend list: creating,
reading, updating and deleting profiles, and keeping simple profile activity data up to date.

The Messaging microService is for Communication between Profiles, it does not own profiles or handle profiles. 
it just gets absolute minimum info required from the Profile microservice to handle messaging between Profiles.

Servers Utilise Messaging microservice to write between multible people on a server, and gets Profile Info from the Profile microservice in the same way as Messaging microService.
Servers know what User ID's is on a server, and from there is the only thing that controls everything in a server.

## 3. Data ownership

Note that the following is our first iteration and can change in the future and is a very coaurse and minimalistic representation of the final product.

Profile:

| Data | Owner | Notes |
|------|-------|-------|
| Profile (id, displayName, createdAt, lastMessageAt) | Profiles | Single source of truth |
| Friend (id, displayName, createdAt) | Profiles | Entity to avoid recursive data |

Messaging:

| Data | Owner | Notes |
|------|-------|-------|
| Message (id, SenderID, ChatID, MessageBody, CreatedAt) | Messaging | Individual message belonging to a chat |
| Chat (id, CreatedByID, RecipientID, ServerID) | Messaging | Represents either a private conversation or a server conversation (SQL CHECK constraint)|

Server:

| Data | Owner | Notes |
|------|-------|-------|
| Server (id, CreatedAt) | Servers | Controls basic information about different servers |
| Voicechat (id, serverId, Name) | Servers | Servers private copy of a voicechat |
| Textchat (id, serverId, Name) | Servers |  Servers private copy of a textchat |
| ServerUsers (id, serverId, UserId) | Servers |  Server's users |

## 4. Boundaries

- Profiles knows nothing about messaging or servers.
- Microservices needs to share as little infomation as possible to ensure high cohesion and loose coupling.
- No other microservice reads other microservices data/database; access goes through the REST API or events.

## 5. Interactions

| Direction | Style | What | Why |
|-----------|-------|------|-----|
| Synchronous Communication between microservies | REST (sync) | `GET/POST/PUT/DELETE /api/####` | Immediate answer needed (dangerous use as little as possible to avoid coupling)|
| Asynchronous communication | Publish/subscribe (async) | `MessageClient` | Handling of events that does not need to be handled instantly |
