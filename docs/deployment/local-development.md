<!-- markdownlint-disable MD013 -->

# Local Development

This runbook starts the Slice 4 development dependencies without live supplier access or committed secrets. PostgreSQL 17.10 is the product-data store. Keycloak 26.7.0 owns local credentials and issues an API audience to the public PKCE web client. The imported realm contains no users, passwords or production configuration. Search, hosted payment and booking use sanitized deterministic fixtures only in Development.

## Prerequisites

- .NET 10 SDK matching `global.json`;
- Docker with Compose v2; and
- two terminal windows for the API and Blazor web hosts.

## Configure Local Secrets

From the repository root:

```bash
cp deploy/local/.env.example deploy/local/.env
```

Replace both example passwords. `deploy/local/.env` is ignored by Git. These are development credentials only and must never be copied to production.

## Start PostgreSQL and Keycloak

```bash
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml up --detach
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml ps
```

Both services bind to loopback only. PostgreSQL listens on `127.0.0.1:5432`; Keycloak listens on `127.0.0.1:8080`. Wait until both health checks report `healthy`.

## Apply the Consumer and Booking Migrations

Load the local variables, restore the pinned EF tool and apply the checked-in migration explicitly:

```bash
set -a
source deploy/local/.env
set +a
dotnet tool restore
dotnet tool run dotnet-ef database update \
  --project src/ReadyToGoTravel.Consumer/ReadyToGoTravel.Consumer.csproj \
  --connection "Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}"
dotnet tool run dotnet-ef database update \
  --project src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj \
  --context ReadyToGoTravel.Booking.Persistence.BookingDbContext \
  --connection "Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}"
```

Applications do not silently migrate a production database during startup. Deployment automation must run the reviewed migration as a controlled step.

## Run the API and Web Host

Keep the variables loaded. Start the API in the first terminal:

```bash
ConnectionStrings__Consumer="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj
```

Start the web host in a second terminal:

```bash
dotnet run --project src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj
```

Open `http://localhost:5081`. The API is at `http://localhost:5080`; its development OpenAPI document is `/openapi/v1.json`, liveness is `/health/live` and database-aware readiness is `/health/ready`.

Choose **Sign in** and register the first local account in Keycloak. Local self-registration is intentional; the imported realm never ships a known customer password. The first account profile still requires the RTGT adult-purchaser attestation.

## Exercise Sandbox Checkout

Open `/search`, run the deterministic Melbourne hotel search or Sydney-to-Melbourne flight search, and choose hotel, flight or combined checkout. Select an owned trip and traveller, review the server-resolved total and terms, accept the current revision, complete the local hosted-payment fixture and submit booking. The browser sends only platform offer IDs, the accepted revision and one opaque hosted-payment completion reference; it does not send an authoritative price, provider selection or card data.

The QF checkout fixture demonstrates repricing from the search total to the current checkout total and therefore requires acceptance of the new authoritative revision. The hotel fixture reaches `Completed`; the QF flight fixture deliberately remains `BookingPending`, including inside a combined journey, so the recovery control can demonstrate safe retrieval without a blind retry. Payment and every component booking remain visibly separate.

Default Production configuration keeps `Booking:Environment=Production` and `Booking:EnableFixtures=false`. With Production configuration, authenticated checkout creation returns `503 booking_capability_unavailable` even if a secret-like setting is present because no payment or booking provider is registered.

## Stop or Reset

Stop containers while retaining local data:

```bash
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml down
```

To deliberately erase the local PostgreSQL and Keycloak volumes, add `--volumes` to that command. This reset is destructive and cannot be recovered from the local stack.

## Boundaries

- The local realm is not a production realm template: production requires HTTPS, verified email and reviewed recovery, federation, session, administrative access and backup controls.
- Supplier search, booking, hosted payment and webhook capabilities remain disabled in Production; only sanitized Development fixtures are available.
- Reusable passport, identity-document and date-of-birth storage remains disabled; those details are not accepted by Slice 2 APIs or persisted in its schema.
