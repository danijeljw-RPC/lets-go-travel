<!-- markdownlint-disable MD013 -->

# Consumer Foundation Design

## Status

Approved for autonomous implementation on 2026-07-29 under the product owner's direction to choose the smallest strong design and continue without waiting for production evidence.

## Objective

Deliver Slice 2 of the consumer MVP: Keycloak-compatible authentication, locale handling, subject-linked customers, customer-owned trips and low-risk saved travellers. The slice must be useful without supplier access and must not activate reusable date-of-birth, passport or identity-document storage.

## Approaches Considered

### Approach A — One consumer module with internal feature areas

Create one `ReadyToGoTravel.Consumer` project containing Customers, Trips and Travellers as separate namespaces and endpoint groups over one module-owned database context.

This keeps the solution small while preserving table ownership, authorization and public-contract boundaries. The three areas change together during onboarding and do not justify separate assemblies or deployables.

### Approach B — One project per domain noun

Create separate Customers, Trips and Travellers projects and public interfaces between them.

This makes compile-time boundaries stronger but introduces project, registration and migration overhead without independent deployment or ownership needs.

### Approach C — Put Slice 2 directly in the API project

Add entities, EF Core mappings and endpoints to `ReadyToGoTravel.Api`.

This is initially shorter but makes HTTP hosting own product rules and creates the layer-only monolith rejected by ADR-0009.

## Decision

Choose Approach A. `ReadyToGoTravel.Consumer` is a feature module, not a generic domain/application/infrastructure framework. Its public surface is limited to service and endpoint registration. Product types remain internal unless another accepted slice needs a deliberate contract.

## Identity Boundary

Keycloak owns credentials, email verification, recovery, federation, sessions and tokens. The API validates issuer, audience, signature, lifetime and token type through ASP.NET Core JWT bearer authentication. Protected endpoints derive ownership from the immutable `sub` claim; email, username, booking reference and surname never identify an owner.

The Blazor SSR host uses authorization-code flow with PKCE and a secure server authentication cookie. It may be configured as a public Keycloak client for local development; no browser-delivered supplier or identity administration secret is introduced. The web host forwards the current access token only to the configured RTGT API origin.

API and web hosts remain available for public routes when Keycloak itself is unavailable. Authentication challenges or protected requests may fail closed, but identity discovery is not required at process startup.

## Customer Model

A customer is created or updated by idempotent `PUT /api/v1/me`. The row contains:

- UUIDv7 platform customer ID;
- unique Keycloak subject;
- `Active`, `Suspended` or `Closed` product status;
- preferred supported BCP 47 locale;
- display currency, initially `AUD`;
- adult-purchaser attestation timestamp and policy version; and
- created and updated UTC timestamps.

Email and display name are read from the current verified token when needed and are not duplicated into the customer row in this slice. Account activation requires an affirmative age-18 attestation. The platform stores the attestation rather than date of birth.

`GET /api/v1/me` returns the current product profile or `404 profile_not_found`. A suspended or closed profile cannot create or change trips or travellers.

## Locale Model

The supported-locale catalogue is code-owned and initially contains only `en-AU`, with `AUD` as the default display currency. Static UI translations use version-controlled .NET resources and `en-AU` fallback; the database is not a translation catalogue.

Anonymous selection is stored by the web host in an essential, secure-in-production, `HttpOnly`, `SameSite=Lax` locale cookie. For authenticated customers, the PostgreSQL preference is authoritative after sign-in and is updated only through the profile API. Language never changes supplier, charged, settlement or refund currency.

Unsupported locale values fail with `400 unsupported_locale`; they do not silently become another locale.

## Trip Model

A trip contains UUIDv7 ID, owning customer ID, title, optional primary destination, optional start and end dates, `Planning`, `Active` or `Archived` status and UTC timestamps. End date cannot precede start date. Title is required and limited to 120 characters; destination is limited to 160 characters.

The slice exposes owned list, create, detail and archive operations under `/api/v1/trips`. Every query includes the authenticated customer ID. Looking up another customer's identifier returns the same `404 trip_not_found` response as a missing trip.

Sharing, manual itinerary items and hard deletion are outside this slice.

## Traveller Model

A saved traveller contains UUIDv7 ID, owning customer ID, given name, family name, optional relationship label, minor flag, optional guardian-authority attestation timestamp and UTC timestamps. Names are required and limited to 100 characters; relationship is limited to 60 characters.

A minor traveller requires affirmative guardian-authority attestation. The account holder remains an adult purchasing account; this does not claim that every traveller is an adult or permit unaccompanied-minor inventory.

The slice exposes owned list, create and remove operations under `/api/v1/travellers`. Removal deletes only the reusable low-risk profile and cannot rewrite future booking evidence.

Date of birth, passport number, nationality, document dates and other identity-document fields are absent from saved-traveller commands and tables. Unknown JSON members are rejected. A public capability response reports sensitive reusable storage as disabled. A later activation requires its own reviewed encryption, masking, audit, consent, retention and key-management design.

Booking-time traveller snapshots remain a booking-module responsibility in Slice 4. The traveller module will later provide an explicit snapshot input contract, but this slice must not persist a pretend booking snapshot without a booking aggregate.

## Persistence

PostgreSQL is authoritative. Entity Framework Core mappings live inside the consumer module and use schema `consumer`. Database constraints and unique indexes reinforce domain validation, including one customer per Keycloak subject and owned identifier lookups.

The repository carries an initial migration and a pinned `dotnet-ef` tool. Development may apply migrations only when `Database:ApplyMigrations=true`; production migration remains a controlled deployment step. Readiness includes the consumer database health check.

SQLite in-memory integration tests verify mappings, ownership and HTTP behavior without weakening the production provider boundary. PostgreSQL migration and container smoke checks remain separate verification steps.

## API and Error Contract

All routes remain under `/api/v1`. Protected groups require authenticated users and return existing `ProblemDetails` envelopes with stable `code` and `correlationId` extensions. Validation errors include an `errors` extension. Unknown JSON properties fail closed.

Create operations return `201` with a resource location. Profile replacement is idempotent. Trip archive and traveller removal are idempotent for the current owner. This slice has no supplier, payment or production capability dependency.

## Web Experience

The existing landing page gains a compact locale selector and sign-in/account navigation. Static SSR pages provide account setup plus simple trip and traveller management through the public API. No product repository or consumer-module reference is added to the web project.

The UI states plainly that DOB and passport details are entered during checkout and are not saved for reuse. It does not show a disabled consent checkbox or invite consent for a capability that cannot yet be protected.

## Testing

- Domain tests cover adult attestation, locale validation, trip dates, minor guardian authority and absence of sensitive saved fields.
- API integration tests use authenticated test subjects and SQLite to prove provisioning, ownership isolation, validation, status behavior and stable errors.
- Architecture tests ensure the web host has no consumer-module reference and the consumer module has no dependency on API or web.
- Authentication tests prove protected endpoints reject anonymous requests and ownership always comes from `sub`.
- Existing foundation tests, release build, formatting, documentation validation, dependency audit and all four container builds remain required.

## Activation and Deferrals

Keycloak production realm/client configuration, reusable sensitive traveller storage and customer-facing supplier capabilities remain disabled until their recorded production gates are approved. Their absence does not block the provider-neutral module, API, web or local integration work.

No organization, tenancy, TMC, reseller, enterprise role hierarchy, sharing, loyalty, native mobile, live flight status or supplier payload enters this slice.

## Success Criteria

- A Keycloak subject can idempotently establish one adult consumer profile.
- Locale defaults and precedence are explicit and testable.
- One customer cannot observe or change another customer's trips or travellers.
- Low-risk traveller profiles work without storing DOB or passport data.
- Minor profiles require guardian authority.
- The solution remains a small four-process deployment with one additional feature-module assembly.
- Slice 3 can consume customer/trip ownership without changing the public Slice 2 contract.

## Self-review Record

- Placeholder scan: no placeholder or unspecified behavior remains.
- Consistency: identity, locale, privacy and module boundaries match ADR-0003, ADR-0009, OI-0007 and OI-0008.
- Scope: one consumer module and existing four hosts; no new deployable or speculative infrastructure.
- Ambiguity: account age, locale precedence, ownership, minor authority and sensitive-data disablement are explicit.
