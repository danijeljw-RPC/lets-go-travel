<!-- markdownlint-disable MD013 -->

# Local Development

This runbook starts the Slice 6 development dependencies without live supplier, notification, object-storage or malware-scanning access or committed secrets. PostgreSQL 17.10 is the product-data store. Keycloak 26.7.0 owns local credentials and issues an API audience to the public PKCE web client; the imported realm also defines a `support-agent` realm role for the privileged staff console. The imported realm contains no users, passwords or production configuration. MinIO and ClamAV are optional local dependencies for exercising private support-ticket attachments; support-ticket creation, correspondence and ticket status work without them. Search, hosted payment, booking and retrieval use sanitized deterministic fixtures only in Development.

## Prerequisites

- .NET 10 SDK matching `global.json`;
- Docker with Compose v2; and
- two terminal windows for the API and Blazor web hosts.

## Configure Local Secrets

From the repository root:

```bash
cp deploy/local/.env.example deploy/local/.env
```

Replace all example passwords, including `MINIO_ROOT_PASSWORD`. `deploy/local/.env` is ignored by Git. These are development credentials only and must never be copied to production.

## Start PostgreSQL, Keycloak, MinIO and ClamAV

```bash
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml up --detach
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml ps
```

All four services bind to loopback only. PostgreSQL listens on `127.0.0.1:5432`; Keycloak listens on `127.0.0.1:8080`; MinIO listens on `127.0.0.1:9000` (API) and `127.0.0.1:9001` (console); ClamAV listens on `127.0.0.1:3310`. Wait until all four health checks report `healthy` — ClamAV's virus-database load can take a minute or more on first start. The official ClamAV image ships `amd64` only, so `clamav` runs under emulation on Apple Silicon (`platform: linux/amd64`); expect a slower first pull and start there. If you only need Postgres and Keycloak, omit MinIO/ClamAV from your workflow; support tickets, correspondence and staff actions all work without them, and attachment uploads simply stay quarantined.

Create the MinIO bucket the first time you start it:

```bash
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml exec minio \
  sh -c 'mc alias set local http://localhost:9000 "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" && mc mb --ignore-existing local/rtgt-support-attachments'
```

## Apply the Consumer, Booking and Support Migrations

Load the local variables, restore the pinned EF tool and apply the checked-in migrations explicitly:

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
dotnet tool run dotnet-ef database update \
  --project src/ReadyToGoTravel.Support/ReadyToGoTravel.Support.csproj \
  --context ReadyToGoTravel.Support.Persistence.SupportDbContext \
  --connection "Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}"
```

Applications do not silently migrate a production database during startup. Deployment automation must run the reviewed migration as a controlled step.

## Run the API and Web Host

Both the API and Worker hosts resolve the Support module's connection string from `ConnectionStrings:Support`, falling back to the host's own default (`ConnectionStrings:Consumer` in the API, `ConnectionStrings:Booking` in the Worker) when `Support` is unset. Set `ConnectionStrings__Support` explicitly on both hosts if Support ever needs to point somewhere other than that host's default.

Keep the variables loaded. Start the API in the first terminal:

```bash
ConnectionStrings__Consumer="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj
```

Start the web host in a second terminal:

```bash
dotnet run --project src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj
```

Start durable work in additional terminals when exercising reconciliation:

```bash
ConnectionStrings__Booking="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.Worker/ReadyToGoTravel.Worker.csproj
ConnectionStrings__Booking="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.FlightReconciliation.Worker/ReadyToGoTravel.FlightReconciliation.Worker.csproj
```

Open `http://localhost:5081`. The API is at `http://localhost:5080`; its development OpenAPI document is `/openapi/v1.json`, liveness is `/health/live` and database-aware readiness is `/health/ready`.

Choose **Sign in** and register the first local account in Keycloak. Local self-registration is intentional; the imported realm never ships a known customer password. The first account profile still requires the RTGT adult-purchaser attestation.

## Exercise Sandbox Checkout

Open `/search`, run the deterministic Melbourne hotel search or Sydney-to-Melbourne flight search, and choose hotel, flight or combined checkout. Select an owned trip and traveller, review the server-resolved total and terms, accept the current revision, complete the local hosted-payment fixture and submit booking. The browser sends only platform offer IDs, the accepted revision and one opaque hosted-payment completion reference; it does not send an authoritative price, provider selection or card data.

The QF checkout fixture demonstrates repricing from the search total to the current checkout total and therefore requires acceptance of the new authoritative revision. The hotel fixture reaches `Completed`; the QF flight fixture deliberately remains `BookingPending`, including inside a combined journey, so the recovery control can demonstrate safe retrieval without a blind retry. Payment and every component booking remain visibly separate.

Default Production configuration keeps `Booking:Environment=Production` and `Booking:EnableFixtures=false`. With Production configuration, authenticated checkout creation returns `503 booking_capability_unavailable` even if a secret-like setting is present because no payment or booking provider is registered.

## Exercise Webhook Receipt

Webhook ingress remains disabled in checked-in Development configuration. For an intentional local-only exercise, provide the settings through environment variables and restart the API:

```bash
Booking__Webhooks__LiteApi__Enabled=true \
Booking__Webhooks__LiteApi__Environment=Sandbox \
Booking__Webhooks__LiteApi__CurrentSecret="replace-with-a-long-local-only-secret" \
ConnectionStrings__Consumer="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj
```

Send the exact configured secret in `Authorization` to `POST /api/v1/webhooks/liteapi/sandbox`. A valid LiteAPI-style envelope is acknowledged only after durable receipt. Processing extracts a booking reference from its stringified nested request/response, enqueues retrieval and lets reconciliation create provider-neutral history. Never reuse the local secret in another environment, commit it or interpret this exercise as OI-0005 production evidence.

## Exercise Support Tickets, Guest Links and Attachments

Open `/support/new` while signed out to submit a guest ticket, or while signed in at `/support/new` to submit an authenticated ticket. Support tickets and their acknowledgement notifications work without MinIO or ClamAV.

Guest-acknowledgement magic-link tokens are minted just-in-time by the outbox processor and are never persisted anywhere in plaintext, including in `support.support_notification_outbox.payload_json` - only their SHA-256 hash is ever stored, in `support.support_guest_access_tokens`. There is deliberately no query against the database that can recover a raw token. The checked-in Development configuration instead sets `Support:Notifications:Sender` to `DevelopmentLog`, which registers `DevelopmentLogSupportNotificationSender` in place of a live email provider: it logs the full notification - including the raw guest-link token - to the console of whichever host actually sends it, and never sends real email. Retrieve the token from the running `ReadyToGoTravel.Worker` console (the worker drains the notification outbox) as a line beginning `[DEV ONLY - not a real send] Support notification ...`; staff-initiated guest-link rotation logs the same way from the `ReadyToGoTravel.Api` console instead, since that action sends synchronously. `DevelopmentLogSupportNotificationSender` only activates when the host environment reports Development - `SupportModule.AddSupportModule` throws on startup if `Support:Notifications:Sender=DevelopmentLog` is ever set outside Development, so this cannot be enabled by a Production configuration mistake. Open `/support/guest/{token}` to exercise the guest thread, reply and attachment flow for that ticket only.

The checked-in `Support:GuestTokens:NotificationSigningKey` in `appsettings.Development.json` is a fixed, non-secret placeholder that lets the outbox processor deterministically re-derive a guest token if it has to retry an ambiguous delivery, without ever storing the raw value at rest. It is safe to commit only because it is a development fixture; Production has no default and refuses to start without an operator-supplied secret (`Support__GuestTokens__NotificationSigningKey`), the same way `Booking__Webhooks__LiteApi__CurrentSecret` must be supplied out-of-band below. Never reuse it in another environment.

To exercise attachment upload/download, start MinIO and ClamAV (above), create the bucket, and start the API/worker with storage and scanning enabled:

```bash
Support__Storage__Enabled=true \
Support__Storage__AccessKey="${MINIO_ROOT_USER}" \
Support__Storage__SecretKey="${MINIO_ROOT_PASSWORD}" \
Support__Scanning__ClamAv__Enabled=true \
ConnectionStrings__Consumer="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj
Support__Storage__Enabled=true \
Support__Storage__AccessKey="${MINIO_ROOT_USER}" \
Support__Storage__SecretKey="${MINIO_ROOT_PASSWORD}" \
Support__Scanning__ClamAv__Enabled=true \
ConnectionStrings__Booking="Host=localhost;Port=5432;Database=rtgt;Username=rtgt;Password=${RTGT_POSTGRES_PASSWORD}" \
  dotnet run --project src/ReadyToGoTravel.Worker/ReadyToGoTravel.Worker.csproj
```

Upload a PDF, JPEG, PNG or plain-text file under 10 MiB. It stays `Pending` until the running worker's attachment-scan cycle processes it against ClamAV; only a `Clean` result makes it downloadable, and the download link is a MinIO presigned URL valid for five minutes. Grant a Keycloak user the `support-agent` realm role (via the Keycloak admin console at `http://localhost:8080`) to reach `/staff/support` and exercise ticket reply, close, and guest-link rotate/revoke. Never reuse local MinIO/ClamAV configuration in another environment or interpret this exercise as production object-storage or malware-scanning evidence.

## Stop or Reset

Stop containers while retaining local data:

```bash
docker compose --env-file deploy/local/.env --file deploy/local/compose.yaml down
```

To deliberately erase the local PostgreSQL and Keycloak volumes, add `--volumes` to that command. This reset is destructive and cannot be recovered from the local stack.

## Boundaries

- The local realm is not a production realm template: production requires HTTPS, verified email and reviewed recovery, federation, session, administrative access and backup controls.
- Supplier search, booking, hosted payment, webhook and outbound notification capabilities remain disabled in Production; only sanitized Development fixtures are available.
- Reusable passport, identity-document and date-of-birth storage remains disabled; those details are not accepted by Slice 2 APIs or persisted in its schema.
- Object storage and malware scanning remain disabled in Production by default; support attachments stay quarantined until both are explicitly activated and evidenced.
- `Support:Notifications:Sender=DevelopmentLog` (the local guest-token logging path) only ever activates when the host environment reports Development; Production requires an operator-supplied `Support:GuestTokens:NotificationSigningKey` and has no default sender other than the disabled one.
