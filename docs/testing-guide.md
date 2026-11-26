# Test-guide

Samlet vejledning til at køre alle tests i projektet: de automatiske tests og de manuelle scenarier for uge 43–47. Den er skrevet, så alle på holdet kan køre dem uden at kende detaljerne i forvejen.

Alle kommandoer er til **PowerShell** og køres fra **repo-roden**. `curl.exe` bruges med vilje i stedet for `curl`, fordi `curl` i PowerShell er et alias for en anden kommando.

---

## 1. Forudsætninger

- Docker Desktop, .NET 10 SDK og PowerShell.
- En `.env`-fil i repo-roden. Den ligger ikke i git, så den skal oprettes lokalt med disse fire variabler:

  | Variabel | Indhold |
  |---|---|
  | `JWT_SIGNING_KEY` | Hemmelig nøgle på mindst 32 tegn. Auth-Service signerer tokens med den, og Gateway og Profile validerer med den. |
  | `MESSAGE_SERVICE_SECRET` | Secret til Auth-klienten `messageservice` (rollen internal-service). |
  | `UNPRIVILEGED_SECRET` | Secret til Auth-klienten `unprivileged` (rollen viewer). |
  | `PROFILE_SVC_VAULT_PASSWORD` | Password til Vault-brugeren `profile-svc`. |

- Vault skal være initialiseret og låst op, med hemmeligheden `secret/bizcord/profile`, policyen `profile-read` og userpass-brugeren `profile-svc`. Trinnene til at sætte det op står ikke i denne guide.
- Alle tests nedenfor, undtagen afsnit 4 (automatiske tests), kræver, at hele opsætningen kører:

```powershell
docker compose up --build -d
docker compose ps
```

Vent ca. et minut, så Elasticsearch, Kibana og Profile er klar.

**Porte:** Gateway `http://localhost:8000`, Auth-Service `http://localhost:8100`, Vault `http://localhost:8200`, Elasticsearch `http://localhost:9200`, Kibana `http://localhost:5601`. Profile har ingen publiceret port i compose. Når Profile køres lokalt med `dotnet run`, bruger den `http://localhost:5210`.

### Tokens (bruges i de fleste manuelle tests)

```powershell
# internal-service
$secret = (Get-Content .env | Where-Object { $_ -like 'MESSAGE_SERVICE_SECRET=*' }).Split('=',2)[1]
$tok = (Invoke-RestMethod -Method Post -Uri http://localhost:8100/connect/token -ContentType 'application/json' -Body (@{ clientId = 'messageservice'; clientSecret = $secret } | ConvertTo-Json)).accessToken

# viewer (uprivilegeret)
$secretV = (Get-Content .env | Where-Object { $_ -like 'UNPRIVILEGED_SECRET=*' }).Split('=',2)[1]
$tokV = (Invoke-RestMethod -Method Post -Uri http://localhost:8100/connect/token -ContentType 'application/json' -Body (@{ clientId = 'unprivileged'; clientSecret = $secretV } | ConvertTo-Json)).accessToken
```

---

## 2. Faldgruber (læs først)

1. **Kun én Profile ad gangen, når du kører integrationstesten eller saga-testene.** Profile i compose, `dotnet run` og testværterne lytter alle på den samme RabbitMQ-kø. RabbitMQ deler beskederne mellem dem, så en test kan time ud, fordi en anden instans tog beskeden. Tjek først:
   ```powershell
   docker compose ps profilemicroservice
   Get-Process | Where-Object { $_.ProcessName -eq "src" }
   ```
   Stop containeren (`docker compose stop profilemicroservice`) og luk eventuelle `dotnet run`-vinduer for Profile.
2. **`docker compose restart` bygger ikke om.** Ændringer i kode eller i `apps/Gateway/src/ocelot.json` ligger i imaget og kræver `docker compose up --build -d`.
3. **Tokens udløber efter 5 minutter.** Giver en test uventet 401, så hent tokens igen.
4. **`ban` kan give 503 lige efter opstart eller når RabbitMQ er nede.** Saga-bussen startes i baggrunden. Vent på linjen `Rebus saga bus started.` i Profiles log, og prøv igen.
5. **Miljøvariabler gælder kun i det PowerShell-vindue, de er sat i.** Fjern stub-indstillinger mellem saga-tests (se afsnit 5.6).
6. **Resultatet af `dotnet test` kan forsvinde, hvis du sender det gennem en pipe.** Brug exit code i stedet: `0` betyder, at alt bestod (se afsnit 4).
7. RabbitMQ kan have mange gamle køer, der hedder `…_test-<guid>`. De kommer fra tidligere testkørsler og er harmløse.

---

## 3. Oversigt

| # | Hvad | Uge | Kræver |
|---|---|---|---|
| 4 | Automatiske tests (`dotnet test`) | 44, 47 | RabbitMQ kun til integrationstesten |
| 5.1 | Routing gennem gatewayen | 43 | compose |
| 5.2 | Authorization (401/403) | 43 | compose |
| 5.3 | RabbitMQ nede (reliability) | 44 | compose |
| 5.4 | East/West: Profile direkte | 45 | compose, `dotnet run` |
| 5.5 | Vault | 45 | compose, Vault låst op |
| 5.6 | Saga (ban) | 46 | RabbitMQ, stub, `dotnet run` |
| 5.7 | Logging med Fluentd | 47 | compose |
| 5.8 | GraphQL | 47 | compose |

---

## 4. Automatiske tests

Stop andre Profile-instanser først (faldgrube 1). Start RabbitMQ til integrationstesten:

```powershell
docker compose stop profilemicroservice
docker compose up rabbitmq -d
dotnet test apps/Profile-Microservice/tests
```

Forventet: alle tests består.

| Del | Kommando | Kræver RabbitMQ |
|---|---|---|
| GraphQL | `dotnet test apps/Profile-Microservice/tests --filter FullyQualifiedName~GraphQLTests` | Nej (virker også, mens RabbitMQ er stoppet) |
| Reliability (retry og timeout) | `dotnet test apps/Profile-Microservice/tests --filter FullyQualifiedName~MessageHandlerHostedServiceTests` | Nej |
| Integration | `dotnet test apps/Profile-Microservice/tests --filter Category=Integration` | Ja |

De to testklasser, der starter en hel Profile-vært (`GraphQLTests` og `MessagePostedIntegrationTests`), ligger i samme xUnit-collection (`[Collection("Profile host")]`), så de kører efter hinanden og ikke konkurrerer om køen.

**Kør flere gange for at afsløre ustabile tests** (viser kun exit code):

```powershell
1..5 | ForEach-Object { dotnet test apps/Profile-Microservice/tests --no-build *> $null; "Kørsel $_ : exit $LASTEXITCODE" }
```

Kør `dotnet build apps/Profile-Microservice/tests` først. Forventet: `exit 0` hver gang.

Bagefter: `docker compose up -d` starter Profile i compose igen.

---

## 5. Manuelle tests

### 5.1 Routing gennem gatewayen (uge 43, task 1)

Alle `/api`-ruter kræver login, så brug `$tok`.

```powershell
$h = @{ Authorization = "Bearer $tok" }
$p = Invoke-RestMethod -Method Post -Uri http://localhost:8000/api/profiles -Headers $h -ContentType 'application/json' -Body '{"displayName":"Test"}'
Invoke-RestMethod http://localhost:8000/api/profiles -Headers $h
Invoke-RestMethod http://localhost:8000/api/profiles/$($p.id) -Headers $h
Invoke-RestMethod -Method Put -Uri http://localhost:8000/api/profiles/$($p.id) -Headers $h -ContentType 'application/json' -Body '{"displayName":"Test2"}'
curl.exe -s -o NUL -w "%{http_code}" -X DELETE -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles/$($p.id)
curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles/$($p.id)
```

Forventet: POST giver profilen med et id. GET (liste og id) giver 200. PUT giver 200 med det nye navn. DELETE giver 204. GET efter sletning giver 404.

**Ban, unban og bans:**

```powershell
$p = Invoke-RestMethod -Method Post -Uri http://localhost:8000/api/profiles -Headers $h -ContentType 'application/json' -Body '{"displayName":"BanTest"}'
$ban = Invoke-RestMethod -Method Post -Uri http://localhost:8000/api/profiles/$($p.id)/ban -Headers $h
Invoke-RestMethod http://localhost:8000/api/bans/$($ban.banId) -Headers $h
curl.exe -s -o NUL -w "%{http_code}" -X POST -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles/$($p.id)/unban
```

Forventet: ban giver 202 med et `banId` (kommer der 503, så vent på, at saga-bussen er startet, se faldgrube 4). `/api/bans/{banId}` giver 200 med en tilstand. Uden stubben kørende er tilstanden `Started`. Unban giver 202.

### 5.2 Authorization (uge 43, task 2)

| # | Test | Kommando | Forventet |
|---|---|---|---|
| 1 | Uden token | `curl.exe -s -o NUL -w "%{http_code}" http://localhost:8000/api/profiles/me` | 401 |
| 2 | Ugyldigt token | `curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer abc" http://localhost:8000/api/profiles/me` | 401 |
| 3 | Falsk `X-UserId` | `curl.exe -s -H "Authorization: Bearer $tok" -H "X-UserId: hacker" http://localhost:8000/api/profiles/me` | `{"userId":"messageservice"}`. Gatewayen fjerner klientens header. |
| 4 | `viewer` på ban | `curl.exe -s -o NUL -w "%{http_code}" -X POST -H "Authorization: Bearer $tokV" http://localhost:8000/api/profiles/<id>/ban` | 403 |
| 5 | `internal-service` på ban | samme med `$tok` | 202 |
| 6 | `viewer` på `/internal/profiles` | `curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tokV" http://localhost:8000/internal/profiles` | 403 |
| 7 | `internal-service` på `/internal/profiles` | samme med `$tok` | 200 |

Opret en profil først og indsæt dens id for `<id>`.

### 5.3 RabbitMQ nede (uge 44, task 2)

Profile skal kunne starte og svare uden RabbitMQ. Saga-bussen starter i baggrunden, når RabbitMQ er der, og `ban` svarer 503, mens den ikke er klar.

```powershell
docker compose up --build -d
docker compose stop rabbitmq
docker compose restart profilemicroservice
Start-Sleep -Seconds 20
docker compose ps profilemicroservice
(docker compose logs profilemicroservice | Select-String "not ready for the Rebus").Count
(docker compose logs profilemicroservice | Select-String "Hosting failed").Count
```

Forventet: containeren er `Up` (ikke `Restarting`), første tal er over 0, og andet tal er 0.

Med RabbitMQ stadig stoppet (hent evt. nyt `$tok`):

```powershell
$p = Invoke-RestMethod -Method Post -Uri http://localhost:8000/api/profiles -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body '{"displayName":"NedeTest"}'
curl.exe -s -w " %{http_code}" -X POST -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles/$($p.id)/ban
(Invoke-RestMethod http://localhost:8000/api/profiles/$($p.id) -Headers @{ Authorization = "Bearer $tok" }).isBanned
```

Forventet: oprettelsen virker. `ban` giver `503` med teksten "The ban saga is not ready yet. Try again shortly.", og `isBanned` er `False` (profilen bliver ikke bandlyst, før sagaen kan køre).

Start RabbitMQ igen:

```powershell
docker compose start rabbitmq
Start-Sleep -Seconds 60
(docker compose logs profilemicroservice | Select-String "saga bus started").Count
curl.exe -s -o NUL -w "%{http_code}" -X POST -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles/$($p.id)/ban
```

Forventet: tallet er 2 (én fra første opstart, hvor RabbitMQ var oppe, og én efter genopretningen). Ban giver nu 202.

**Data forsvinder ved genstart (fejlpunkt F3).** Profiler ligger i hukommelsen:

```powershell
$p = Invoke-RestMethod -Method Post -Uri http://localhost:8000/api/profiles -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body '{"displayName":"Forsvinder"}'
docker compose restart profilemicroservice
curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles/$($p.id)
```

Forventet: 404. Profilen er væk. Det er forventet og viser F3.

### 5.4 East/West-sikkerhed: Profile direkte (uge 45, task 1)

Profile skal selv afvise kald uden gyldigt token og uden rollen `internal-service`, også når den kaldes direkte uden om gatewayen. Profile skal bruge den samme signeringsnøgle som Auth-Service, så den hentes fra `.env`:

```powershell
docker compose stop profilemicroservice
$env:Jwt__SigningKey = (Get-Content .env | Where-Object { $_ -like 'JWT_SIGNING_KEY=*' }).Split('=',2)[1]
$env:Jwt__Issuer = "bizcord-auth"
$env:Jwt__Audience = "internal-api"
dotnet run --project apps/Profile-Microservice/src/src.csproj --launch-profile http
```

I et nyt vindue (hent `$tok` og `$tokV` igen):

```powershell
curl.exe -s -o NUL -w "%{http_code}" http://localhost:5210/api/internal/profiles
curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tokV" http://localhost:5210/api/internal/profiles
curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tok" http://localhost:5210/api/internal/profiles
```

Forventet: 401 uden token, 403 for `viewer`, 200 for `internal-service`.

**Forkert nøgle (valgfri):** Start Profile igen med `$env:Jwt__SigningKey = "en-anden-nogle-paa-mindst-32-bytes-12345"`. Kald `/api/internal/profiles` med det gyldige `$tok`. Forventet: 401.

**Profile kan ikke nås uden om gatewayen i compose:** Stop `dotnet run`, kør `docker compose start profilemicroservice` og derefter `docker compose ps profilemicroservice`. Kolonnen PORTS viser kun den interne port (`8080/tcp`), uden `0.0.0.0:…->`.

### 5.5 Vault (uge 45, task 2)

Vault skal være låst op (`curl.exe -s http://localhost:8200/v1/sys/seal-status` viser `"sealed":false`). Log ind som `profile-svc` (selve hemmeligheden bliver ikke vist):

```powershell
$pw = (Get-Content .env | Where-Object { $_ -like 'PROFILE_SVC_VAULT_PASSWORD=*' }).Split('=',2)[1]
$login = Invoke-RestMethod -Method Post -Uri http://localhost:8200/v1/auth/userpass/login/profile-svc -ContentType 'application/json' -Body (@{ password = $pw } | ConvertTo-Json)
$vt = $login.auth.client_token
$login.auth.policies
```

Forventet: policylisten indeholder `profile-read`.

| # | Test | Kommando | Forventet |
|---|---|---|---|
| 1 | Profile får sin hemmelighed fra Vault | `docker compose config \| Select-String "RABBITMQ_PASSWORD"` og `docker compose logs profilemicroservice --tail 50` | `RABBITMQ_PASSWORD` står ikke som miljøvariabel på `profilemicroservice`. Loggen viser, at Profile startede. |
| 2 | Læs egen hemmelighed | `curl.exe -s -o NUL -w "%{http_code}" -H "X-Vault-Token: $vt" http://localhost:8200/v1/secret/data/bizcord/profile` | 200 |
| 3 | Ugyldigt token | samme med `-H "X-Vault-Token: ugyldigt"` | 403 |
| 4 | Læs en anden sti | samme med stien `…/secret/data/andet` | 403 |
| 5 | Skriv til egen sti | `curl.exe -s -o NUL -w "%{http_code}" -X POST -H "X-Vault-Token: $vt" -H "Content-Type: application/json" -d '{"data":{"x":"y"}}' http://localhost:8200/v1/secret/data/bizcord/test` | 403 |

**Test 5 mod den rigtige sti (`…/bizcord/profile`) må kun køres, hvis policyen er kontrolleret** og kun har `capabilities = ["read"]`. Ellers kan testen overskrive det rigtige RabbitMQ-password. Kontrollér med `docker compose exec vault sh`, derefter `vault login` og `vault policy read profile-read`.

**Vault forsegler ved genstart (fejlpunkt F7):**

```powershell
docker compose restart vault
curl.exe -s http://localhost:8200/v1/sys/seal-status
docker compose restart profilemicroservice
docker compose logs profilemicroservice --tail 30
```

Forventet: `"sealed":true`, og Profile fejler ved opstart, fordi den ikke kan læse hemmeligheden fra Vault. Lås op med to forskellige unseal-nøgler (de vises ikke, når du skriver dem):

```powershell
docker compose exec vault vault operator unseal
docker compose exec vault vault operator unseal
docker compose start profilemicroservice
```

Forventet: `"sealed":false`, og Profile kommer op igen.

### 5.6 Saga: ban af en bruger (uge 46)

Beskrivelse og tidligere resultater: `docs/saga-task02.md`.

**Regler:** Kun én Profile og én stub må køre ad gangen. Sæt stub-indstillinger i stubbens vindue, før den starter, og fjern dem igen, før næste test.

```powershell
docker compose stop profilemicroservice
docker compose up rabbitmq -d
```

Vindue 1 (stubben):

```powershell
dotnet run --project apps/Servers-Stub/src/ServersStub.csproj
```

Vindue 2 (Profile):

```powershell
$env:Jwt__SigningKey = "local-test-key-only-for-saga-testing-0123456789"
$env:Jwt__Issuer = "bizcord-auth"
$env:Jwt__Audience = "internal-api"
dotnet run --project apps/Profile-Microservice/src/src.csproj --launch-profile http
```

Vent på `Rebus saga bus started.` i Profile-vinduet. Vindue 3 (afvikling):

```powershell
$p = Invoke-RestMethod -Method Post -Uri http://localhost:5210/api/profiles -ContentType 'application/json' -Body '{"displayName":"Saga"}'
$ban = Invoke-RestMethod -Method Post -Uri "http://localhost:5210/api/profiles/$($p.id)/ban"
Start-Sleep -Seconds 10
Invoke-RestMethod "http://localhost:5210/api/bans/$($ban.banId)"
Invoke-RestMethod "http://localhost:5210/api/profiles/$($p.id)"
```

Pausen på 10 sekunder er nødvendig, fordi voice-fejltesten bruger 1 + 2 + 4 sekunder på genforsøg.

| Test | Stub-indstilling (sættes før stubben startes) | Forventet |
|---|---|---|
| A: alt lykkes | ingen | `Completed`, `isBanned = true` |
| B: servers afviser | `$env:Stub__FailSuspension = "true"` | `Compensated`, `isBanned = false` |
| D: voice fejler to gange | `$env:Stub__VoiceFailures = "2"` | `Completed`. Stubbens log viser, at forsøg 1 og 2 fejler og 3 lykkes. |
| E: voice opgiver | `$env:Stub__VoiceFailures = "4"` | `NeedsIntervention`, `isBanned = true`. Stubbens log viser `ALERT … Manual intervention required`. |
| H: stubben er slukket ved ban | stub stoppet | tilstand `Started`. Start stubben, og samme ban når `Completed`. |

Test med samme profil og med ukendt id:

```powershell
# Ban den samme profil to gange: andet kald giver 409
curl.exe -s -o NUL -w "%{http_code}" -X POST "http://localhost:5210/api/profiles/$($p.id)/ban"
# Ukendt id: 404
curl.exe -s -o NUL -w "%{http_code}" -X POST "http://localhost:5210/api/profiles/00000000-0000-0000-0000-000000000000/ban"
# Unban: efter kaldet er isBanned false
curl.exe -s -o NUL -w "%{http_code}" -X POST "http://localhost:5210/api/profiles/$($p.id)/unban"
```

Fjern stub-indstillingerne mellem tests:

```powershell
Remove-Item Env:Stub__FailSuspension, Env:Stub__VoiceFailures -ErrorAction SilentlyContinue
```

Begrænsninger, der ikke er testet: Profile nede midt i en saga, dobbeltlevering af samme besked, og første opstart uden køen `servers-stub`.

### 5.7 Logging med Fluentd (uge 47, task 1)

Hele opsætningen skal køre.

```powershell
# 1. Profile skriver JSON til stdout
docker compose logs profilemicroservice --tail 5

# 2. Sidecaren læser og videresender (lav et kald først)
curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles
docker compose logs fluentd --tail 20

# 3. Elasticsearch har modtaget logs
curl.exe -s "http://localhost:9200/_cat/indices?v"
curl.exe -s "http://localhost:9200/bizcord-logs-*/_count"
```

Forventet: 1) linjer i JSON-format med felter som `LogLevel`, `Category` og `Message`. 2) Fluentds log indeholder poster fra Profile med udpakkede felter. 3) Et indeks, der hedder `bizcord-logs-<dato>`, og et `count` over 0.

**Filteret (kun Profile):**

```powershell
curl.exe -s "http://localhost:9200/bizcord-logs-*/_count"
curl.exe -s "http://localhost:9200/bizcord-logs-*/_count?q=attrs.logsource:profile"
```

Forventet: de to tal er ens. Er totalen større, indeholder indekset også poster, der ikke kommer fra Profile (for eksempel Fluentds egne `fluent.*`-events). Notér det i dokumentationen. Søg ikke efter ordene "rabbitmq" eller "elasticsearch" i Fluentds egen log, for startlinjerne nævner dem og giver falske fund.

**Ingen løkke:** Antallet af linjer i Fluentds log må ikke vokse uden nye kald:

```powershell
(docker compose logs fluentd | Measure-Object -Line).Lines
Start-Sleep -Seconds 20
(docker compose logs fluentd | Measure-Object -Line).Lines
```

**Kibana (valgfri):** Åbn `http://localhost:5601`, opret en data view med mønsteret `bizcord-logs-*` (tidsfelt `@timestamp`) og åbn Discover. Profiles logs vises, og man kan søge på `Message` og `LogLevel`.

**Sidecarens fordel:** Servicen virker uden den.

```powershell
docker compose stop fluentd
curl.exe -s -o NUL -w "%{http_code}" -H "Authorization: Bearer $tok" http://localhost:8000/api/profiles
docker compose start fluentd
```

Forventet: 200, selvom Fluentd er stoppet.

### 5.8 GraphQL (uge 47, task 2)

Gateway-routen `/graphql` (POST) kræver et Bearer-token. GraphQL har ingen egen login-kontrol. Det er gatewayen, der beskytter den.

```powershell
# Hent liste
$body = @{ query = '{ profiles { id displayName isBanned } }' } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:8000/graphql -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body $body

# Opret med en mutation
$m = @{ query = 'mutation($n: String!) { createProfile(input: { displayName: $n }) { id displayName } }'; variables = @{ n = "GraphQL Test" } } | ConvertTo-Json -Depth 5
$r = Invoke-RestMethod -Method Post -Uri http://localhost:8000/graphql -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body $m
$r.data.createProfile

# Kun de felter, klienten beder om (ingen over-fetching)
$id = $r.data.createProfile.id
$q = @{ query = 'query($id: String!) { profileById(id: $id) { displayName } }'; variables = @{ id = $id } } | ConvertTo-Json -Depth 5
(Invoke-RestMethod -Method Post -Uri http://localhost:8000/graphql -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body $q).data.profileById

# Fejl og ukendt id
$bad = @{ query = 'mutation { createProfile(input: { displayName: "" }) { id } }' } | ConvertTo-Json
(Invoke-RestMethod -Method Post -Uri http://localhost:8000/graphql -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body $bad).errors
$none = @{ query = '{ profileById(id: "findes-ikke") { displayName } }' } | ConvertTo-Json
(Invoke-RestMethod -Method Post -Uri http://localhost:8000/graphql -Headers @{ Authorization = "Bearer $tok" } -ContentType 'application/json' -Body $none).data

# Uden token
curl.exe -s -o NUL -w "%{http_code}" -X POST http://localhost:8000/graphql
```

Forventet: listen er en `data.profiles`-liste. Mutationen returnerer profilen med `id` og `displayName`. Opslaget returnerer kun `displayName`. Den tomme mutation giver en GraphQL-fejl med koden `DISPLAYNAME_REQUIRED` (HTTP 200, fordi GraphQL melder fejl i svaret). Det ukendte id giver `profileById: null`. Uden token giver gatewayen 401.

---

## 6. Oprydning efter tests

```powershell
Remove-Item Env:Jwt__SigningKey, Env:Jwt__Issuer, Env:Jwt__Audience, Env:Stub__FailSuspension, Env:Stub__VoiceFailures -ErrorAction SilentlyContinue
docker compose up -d
docker compose ps
```

Forventet: alle containere kører, og Profile startes igen i compose.

---

## 7. Hvad er verificeret hvornår

- **26/11-2026:** de automatiske tests (afsnit 4, 5 kørsler i træk med `exit 0`) og afsnit 5.3: Profile starter uden RabbitMQ i compose (genopbygget image), og saga-bussen starter efter genopretning. At `ban` giver 503, mens bussen ikke er klar, er set ved `dotnet run` mod Profile og ikke gentaget gennem gatewayen.
- Resten er kørt tidligere i projektet (saga: se `docs/saga-task02.md`), men er ikke gentaget efter ændringen af Rebus-opstarten.
