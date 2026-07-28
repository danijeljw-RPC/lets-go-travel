# Target Architecture

## Proposed platform components

### Client layer

The initial client is a web UI. Android and iOS applications will be added later.

All clients:

- authenticate through Keycloak;
- obtain OAuth 2.0/OpenID Connect tokens;
- call the platform Web API;
- use platform identifiers;
- display platform models;
- never receive supplier API keys;
- never call LiteAPI directly;
- avoid owning authoritative pricing or booking logic.

### Public application API

The ASP.NET Core Web API is the only public application service used by the platform's clients.

It is responsible for:

- token validation;
- authorisation;
- request validation;
- rate limiting;
- idempotency;
- customer and traveller ownership checks;
- search orchestration;
- supplier selection;
- pricing and discount calculation;
- booking workflow orchestration;
- reconciliation;
- notification initiation;
- API compatibility;
- observability and audit context.

### Identity service

Keycloak is the identity provider.

It should own authentication concerns such as:

- credentials;
- external identity providers;
- email verification;
- password reset;
- multifactor authentication;
- passkeys when selected and supported;
- token issuance;
- session management;
- account recovery policy.

The platform database should not duplicate authentication secrets.

### PostgreSQL

PostgreSQL is the canonical application datastore for:

- platform customer profiles;
- saved travellers;
- trips;
- bookings;
- supplier mappings;
- current normalised booking state;
- pricing records;
- booking versions;
- reconciliation outcomes;
- notification state;
- support and audit information;
- preferences and consent records.

Relational structures should be used for current operational data. JSONB is suitable for controlled extensibility, supplier payload retention, canonical snapshots, and structured differences.

### Supplier integrations

LiteAPI/Nuitee Connect is an initial private integration.

Future supplier categories may include:

- alternative accommodation providers;
- flight aggregators;
- operational flight-status services;
- activities and attractions;
- transfers;
- rail;
- maps;
- weather;
- foreign exchange;
- insurance;
- communications.

Supplier integration logic should sit behind internal interfaces or application boundaries. Clients should not know which supplier fulfilled an operation.

### Background processing

Background workers should handle work that is not required to complete the immediate HTTP response, including:

- booking reconciliation;
- webhook processing;
- notification delivery;
- retryable supplier operations;
- document generation;
- cleanup;
- cache refresh;
- scheduled reminders;
- analytics projection;
- later AI itinerary generation.

### Cache

A distributed cache may be introduced when justified.

Likely cache candidates:

- static hotel content;
- airport, airline, country, and city reference data;
- destination metadata;
- short-lived search responses;
- supplier capability metadata;
- exchange-rate display data.

Durable booking state, payment decisions, and authoritative cancellation state should not rely on cache as the source of truth.

### Object and document storage

Object storage will likely be required for:

- vouchers;
- invoices;
- receipts;
- itinerary documents;
- customer-uploaded travel documents;
- generated trip packs;
- supplier documents where retention is allowed.

Large binary content should not normally be stored directly in PostgreSQL.

## Trust boundaries

The important trust boundaries are:

1. Unauthenticated internet to Keycloak.
2. Unauthenticated internet to explicitly public API endpoints.
3. Authenticated client to Web API.
4. Web API to Keycloak metadata and token validation.
5. Web API to PostgreSQL.
6. Web API and workers to LiteAPI.
7. Web API and workers to notification providers.
8. Internal services to object storage.
9. Administrative and support tooling to privileged application functions.

Each boundary needs explicit authentication, authorisation, logging, timeout, and failure behaviour.

## Deployment shape

The first deployment may be a modular monolith with separately deployable background workers.

That is likely preferable to premature microservices because:

- the domain is still being discovered;
- booking consistency is important;
- one team can maintain a single solution more easily;
- supplier boundaries can remain logically isolated without network distribution;
- later extraction remains possible when scaling or ownership requires it.

Logical modules should still be clear, for example:

- Identity Integration;
- Customers;
- Travellers;
- Trips;
- Search;
- Accommodation;
- Flights;
- Pricing;
- Bookings;
- Payments;
- Reconciliation;
- Notifications;
- Documents;
- Support;
- Supplier Integrations.

## Architectural principle

The platform should treat supplier APIs as external, fallible fulfilment systems.

The platform's public contract must remain stable even when:

- a supplier changes response fields;
- a supplier is temporarily unavailable;
- an offer expires;
- a booking remains pending;
- a webhook is duplicated;
- a supplier returns unexpected data;
- another supplier is introduced;
- a mobile client has not yet upgraded.
