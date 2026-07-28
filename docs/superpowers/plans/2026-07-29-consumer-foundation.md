<!-- markdownlint-disable MD013 -->

# Consumer Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement authenticated consumer profiles, locale preferences, customer-owned trips and low-risk saved travellers without enabling reusable sensitive traveller data.

**Architecture:** Add one feature-oriented `ReadyToGoTravel.Consumer` module with internal Customers, Trips, Travellers and Persistence areas. The API composes the module with PostgreSQL and JWT bearer authentication; module HTTP tests compose the same endpoints with SQLite and a deterministic test authentication scheme. The Blazor SSR host adds Keycloak OIDC, locale-cookie handling and simple API-backed customer pages without referencing the module.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Keycloak/OIDC, EF Core 10, Npgsql 10, SQLite integration tests, xUnit, Blazor static SSR, PostgreSQL migrations and Docker Compose.

## Global Constraints

- Product name is `readytogo.travel`; shortcode is `RTGT`; root namespace is `ReadyToGoTravel`.
- Keep one additional feature-module assembly and no additional deployable.
- Keycloak owns credentials and authentication; PostgreSQL owns product profiles and owned records.
- Ownership comes only from the authenticated `sub` claim.
- Store an adult-purchaser attestation, not account-holder date of birth.
- Do not accept or store reusable date of birth, passport or identity-document fields.
- Supported locale is initially `en-AU`; display currency remains independently `AUD`.
- Public routes remain `/api/v1`; failures remain `ProblemDetails` with `code` and `correlationId`.
- Production integration evidence remains an activation gate, not an implementation blocker.
- Tests precede production behavior and each task ends with a local commit.

---

### Task 1: Consumer module and domain invariants

**Files:**
- Create: `src/ReadyToGoTravel.Consumer/ReadyToGoTravel.Consumer.csproj`
- Create: `src/ReadyToGoTravel.Consumer/ConsumerModule.cs`
- Create: `src/ReadyToGoTravel.Consumer/Customers/Customer.cs`
- Create: `src/ReadyToGoTravel.Consumer/Trips/Trip.cs`
- Create: `src/ReadyToGoTravel.Consumer/Travellers/Traveller.cs`
- Create: `src/ReadyToGoTravel.Consumer/Locale/SupportedLocales.cs`
- Create: `src/ReadyToGoTravel.Consumer/Properties/AssemblyInfo.cs`
- Create: `tests/ReadyToGoTravel.Consumer.Tests/ReadyToGoTravel.Consumer.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/DomainRulesTests.cs`
- Modify: `ReadyToGoTravel.slnx`
- Modify: `Directory.Packages.props`

**Interfaces:**
- Produces: `SupportedLocales.Default = "en-AU"`, `Customer.Create(subject, locale, adultConfirmed, clock)`, `Trip.Create(customerId, title, destination, startDate, endDate, clock)` and `Traveller.Create(customerId, givenName, familyName, relationship, isMinor, guardianAuthorityConfirmed, clock)`.
- Consumes: `TimeProvider` and UUIDv7 generation.

- [ ] **Step 1: Scaffold the module and tests with central package versions**

Add EF Core 10.0.8, EF Design 10.0.8, SQLite 10.0.8, Npgsql EF 10.0.3 and JWT bearer 10.0.8 package versions. Add both projects to the solution and reference the module from its tests.

- [ ] **Step 2: Write failing domain tests**

Tests require adult confirmation, reject unsupported locales, reject reversed trip dates, require guardian authority for minor travellers, normalize trimmed names and prove the traveller type has no DOB/passport/document property.

- [ ] **Step 3: Run the focused tests and confirm failure**

Run: `dotnet test tests/ReadyToGoTravel.Consumer.Tests/ReadyToGoTravel.Consumer.Tests.csproj`

Expected: FAIL because the domain types do not exist.

- [ ] **Step 4: Implement the minimum domain types**

Use internal sealed entities with private EF constructors, explicit factory methods returning a small `DomainResult<T>`, UUIDv7 IDs and UTC timestamps. Store customer status, trip status and minor guardian-attestation state as explicit values.

- [ ] **Step 5: Run focused tests and commit**

Run the consumer tests and full architecture tests. Commit as `feat: add consumer domain module`.

### Task 2: Consumer persistence and migration

**Files:**
- Create: `src/ReadyToGoTravel.Consumer/Persistence/ConsumerDbContext.cs`
- Create: `src/ReadyToGoTravel.Consumer/Persistence/ConsumerEntityConfigurations.cs`
- Create: `src/ReadyToGoTravel.Consumer/Persistence/Migrations/*`
- Create: `.config/dotnet-tools.json`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/PersistenceTests.cs`

**Interfaces:**
- Produces: schema `consumer`, `customers`, `trips` and `travellers`; `AddConsumerModule(Action<IServiceProvider, DbContextOptionsBuilder>)`.
- Consumes: the Task 1 entities and EF Core provider configured by the host.

- [ ] **Step 1: Write failing SQLite mapping tests**

Prove one customer per subject, customer-owned list isolation and mapped table absence of DOB/passport columns.

- [ ] **Step 2: Run tests and confirm mapping failure**

Expected: FAIL because `ConsumerDbContext` and mappings do not exist.

- [ ] **Step 3: Implement mappings and registration**

Map explicit column sizes, string enums, foreign keys, unique subject index and customer ownership indexes. Register `TimeProvider.System`, the database context and its readiness health check.

- [ ] **Step 4: Add and inspect the initial PostgreSQL migration**

Install the repository-local `dotnet-ef` 10.0.8 tool, generate `InitialConsumerSchema` against Npgsql and confirm no sensitive traveller columns exist.

- [ ] **Step 5: Run persistence tests and commit**

Commit as `feat: persist consumer profiles and trips`.

### Task 3: Authentication and consumer HTTP API

**Files:**
- Create: `src/ReadyToGoTravel.Consumer/Http/ConsumerEndpoints.cs`
- Create: `src/ReadyToGoTravel.Consumer/Http/ConsumerContracts.cs`
- Create: `src/ReadyToGoTravel.Consumer/Http/ConsumerHttpResults.cs`
- Create: `src/ReadyToGoTravel.Api/Infrastructure/AuthenticationExtensions.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs`
- Modify: `src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj`
- Modify: `src/ReadyToGoTravel.Api/appsettings.json`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/ConsumerApiTests.cs`

**Interfaces:**
- Produces: public `GET /api/v1/locales`; authenticated `GET`/`PUT /api/v1/me`; owned `GET`/`POST /api/v1/trips`, `GET /api/v1/trips/{id}`, `POST /api/v1/trips/{id}/archive`; owned `GET`/`POST /api/v1/travellers`, `DELETE /api/v1/travellers/{id}`; and `GET /api/v1/privacy/sensitive-traveller-storage`.
- Consumes: authenticated `sub`, module database, existing rate limit and `ProblemDetails` contract.

- [ ] **Step 1: Build an in-process authenticated test host and write failing HTTP tests**

Use `Microsoft.AspNetCore.TestHost`, SQLite in-memory and a test authentication handler that emits a selected `sub`. Cover anonymous `401`, idempotent profile provisioning, unsupported locale, cross-customer `404`, trip validation, minor guardian validation, unknown sensitive JSON rejection and disabled capability response.

- [ ] **Step 2: Run tests and confirm missing routes**

Expected: protected routes return `404` before implementation.

- [ ] **Step 3: Implement module endpoints**

Use route groups, `RequireAuthorization("consumer")`, asynchronous EF queries and stable problem codes. Every ownership query includes both resource ID and current customer ID.

- [ ] **Step 4: Compose PostgreSQL and JWT bearer in the API**

Configure `Authentication:Authority`, `Authentication:Audience`, `ConnectionStrings:Consumer` and `Database:ApplyMigrations`. Require the `sub` policy, reject unknown JSON members and apply migrations only when explicitly enabled.

- [ ] **Step 5: Run API, module and full tests; commit**

Commit as `feat: expose authenticated consumer api`.

### Task 4: Locale and Blazor SSR consumer shell

**Files:**
- Create: `src/ReadyToGoTravel.Web/Authentication/ApiAccessTokenHandler.cs`
- Create: `src/ReadyToGoTravel.Web/Localization/LocaleCatalog.cs`
- Create: `src/ReadyToGoTravel.Web/Resources/SharedResources.en-AU.resx`
- Create: `src/ReadyToGoTravel.Web/SharedResources.cs`
- Create: `src/ReadyToGoTravel.Web/Client/ConsumerApiClient.cs`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Account.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Trips.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Travellers.razor`
- Modify: `src/ReadyToGoTravel.Web/Program.cs`
- Modify: `src/ReadyToGoTravel.Web/Components/Pages/Home.razor`
- Modify: `src/ReadyToGoTravel.Web/appsettings.json`
- Modify: `src/ReadyToGoTravel.Web/wwwroot/app.css`
- Test: `tests/ReadyToGoTravel.Architecture.Tests/WebBoundaryTests.cs`

**Interfaces:**
- Produces: OIDC sign-in/sign-out endpoints, secure `rtgt.locale` cookie, `en-AU` resource fallback and static SSR account/trip/traveller pages.
- Consumes: the Task 3 HTTP API only; web retains no consumer-module project reference.

- [ ] **Step 1: Write failing web-boundary and locale tests**

Prove the web project has no consumer reference, locale catalogue defaults to `en-AU`, the cookie is essential/SameSite Lax/HttpOnly and the API token handler only targets the configured API origin.

- [ ] **Step 2: Implement OIDC, localisation and API clients**

Use cookie plus OpenID Connect authorization code/PKCE, `SaveTokens=true`, request-localization middleware and a delegating handler that forwards the current access token.

- [ ] **Step 3: Add compact customer pages**

Render authentication state, locale, owned trips and low-risk travellers. State that DOB and passport data are collected at checkout and are not saved for reuse. Keep static SSR as default and avoid a client-side state framework.

- [ ] **Step 4: Run tests/build and commit**

Commit as `feat: add consumer account web shell`.

### Task 5: Local identity/database runtime and slice completion

**Files:**
- Create: `deploy/local/compose.yaml`
- Create: `deploy/local/keycloak/rtgt-realm.json`
- Create: `deploy/local/.env.example`
- Create: `docs/deployment/local-development.md`
- Modify: `.gitignore`
- Modify: `README.md`
- Modify: `.github/workflows/ci.yml`
- Modify: `docs/plans/active/PLAN-0002-mvp-delivery.md`
- Modify: `docs/decisions/review-register.md`
- Modify: `docs/superpowers/plans/2026-07-29-consumer-foundation.md`

**Interfaces:**
- Produces: pinned Keycloak 26.7.0 and PostgreSQL local dependencies, deterministic realm/client import and documented local startup without committed secrets.
- Consumes: current API/web configuration and the repository verification workflow.

- [ ] **Step 1: Add local Compose and realm configuration**

Expose PostgreSQL only to localhost, require bootstrap secrets through an ignored `.env`, import an `rtgt` realm, configure public PKCE web client and API audience, and include health checks.

- [ ] **Step 2: Update CI and documentation**

Start PostgreSQL for migration verification, keep Keycloak integration optional, document commands and mark Slice 2 complete without changing production gates.

- [ ] **Step 3: Run fresh verification**

Run locked restore, formatter, release build, all tests, migration script inspection, documentation validation, vulnerability audit and all four Docker builds. Smoke-test anonymous/public API behavior and Keycloak-protected route rejection.

- [ ] **Step 4: Commit**

Commit as `feat: complete consumer foundation slice`.

## Plan Self-review

- Spec coverage: identity, locale, customer, trip, traveller, adult/minor policy, sensitive-data disablement, persistence, API, web and local runtime each have a task.
- Placeholder scan: no placeholder implementation step or unresolved product choice remains.
- Type consistency: one consumer module, one context, one `sub` policy and the same route set are used throughout.
- Scope: no supplier, booking, payment, sharing, enterprise or production-activation work enters Slice 2.
