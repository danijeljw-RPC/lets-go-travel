# Search and Capability Registry Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Slice 3 supplier-neutral hotel and flight search, minimum-total pricing, LiteAPI sandbox fixtures and explicit environment/market/operation capability discovery without activating production supplier routes.

**Architecture:** Add one `ReadyToGoTravel.Search` feature module containing platform-owned search contracts, pricing rules, capability policy and a fixture-backed LiteAPI adapter. The API exposes public `/api/v1/search` routes; the Blazor host uses those routes only. Production configuration has no fixture provider and fails closed until later production evidence and provider registration exist.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, `System.Text.Json`, options validation, xUnit, ASP.NET Core TestServer, Blazor static SSR and sanitized embedded JSON fixtures.

## Global Constraints

- Product name is `readytogo.travel`; shortcode is `RTGT`; root namespace is `ReadyToGoTravel`.
- Keep one additional feature-module assembly and no additional deployable or database.
- Public clients receive platform contracts and opaque platform offer IDs, never supplier payloads, credentials or raw supplier references.
- Hotel and flight prices expose a minimum total, returned currency, requested display currency, currency provenance, included taxes and included fees.
- Offers are provisional and expose expiry plus a revalidation requirement.
- Capability is explicit by provider, environment, point of sale, product, operation and carrier where relevant.
- Qantas (`QF`), Jetstar (`JQ`) and Virgin Australia (`VA`) are recorded only as observed sandbox search carriers, not as production booking claims.
- Fixture search is available only in `Development` and `Testing`; production search fails closed.
- Booking, payment, prebook, persistence, conversion, discounts, combined-journey orchestration and live supplier calls are outside Slice 3.
- Public routes remain `/api/v1`; failures remain `ProblemDetails` with stable `code` and `correlationId`.
- Tests precede the production behavior they prove and each task ends with a local commit.

---

### Task 1: Search contracts, price invariants and capability registry

**Files:**
- Create: `src/ReadyToGoTravel.Search/ReadyToGoTravel.Search.csproj`
- Create: `src/ReadyToGoTravel.Search/SearchModule.cs`
- Create: `src/ReadyToGoTravel.Search/Contracts/SearchRequests.cs`
- Create: `src/ReadyToGoTravel.Search/Contracts/SearchResults.cs`
- Create: `src/ReadyToGoTravel.Search/Pricing/OfferPrice.cs`
- Create: `src/ReadyToGoTravel.Search/Capabilities/CapabilityRegistry.cs`
- Create: `src/ReadyToGoTravel.Search/Properties/AssemblyInfo.cs`
- Create: `tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Search.Tests/SearchDomainTests.cs`
- Modify: `ReadyToGoTravel.slnx`

**Interfaces:**
- Produces: `HotelSearchRequest`, `FlightSearchRequest`, `OfferPrice.Create(...)`, `SearchCapability`, `ICapabilityRegistry.GetSnapshot(...)` and `SearchEnvironment`.
- Consumes: `TimeProvider` and platform UUIDv7 identifiers.

- [x] **Step 1: Scaffold the module and test project**

Add both projects to `ReadyToGoTravel.slnx`, reference the module from its tests and expose internals to the test assembly.

- [x] **Step 2: Write failing domain and capability tests**

```csharp
[Fact]
public void MinimumTotalRejectsComponentsGreaterThanTheTotal()
{
    var result = OfferPrice.Create(100m, "AUD", "AUD", CurrencyProvenance.SupplierReturned, 90m, 12m, 0m);
    Assert.False(result.IsSuccess);
    Assert.Equal("price_components_exceed_total", result.ErrorCode);
}

[Fact]
public void AustralianSandboxRegistryRecordsObservedCarriersWithoutProductionClaims()
{
    var snapshot = CapabilityRegistry.CreateFixtureDefaults().GetSnapshot(SearchEnvironment.Sandbox, "AU");
    Assert.Equal(["JQ", "QF", "VA"], snapshot.Where(x => x.Operation == SearchOperation.FlightSearch).Select(x => x.CarrierCode).Order());
    Assert.All(snapshot, capability => Assert.False(capability.ProductionEnabled));
}
```

- [x] **Step 3: Run focused tests and confirm failure**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj`

Expected: FAIL because the search contracts, price model and registry do not exist.

- [x] **Step 4: Implement the minimum platform-owned model**

Validate IATA codes as three uppercase letters, ISO currencies as three uppercase letters, point-of-sale countries as two uppercase letters, positive traveller/room counts, ordered dates and included price components not exceeding the minimum total. Registry entries explicitly distinguish observed sandbox search from production enablement.

- [x] **Step 5: Run focused and architecture tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj && dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj`

Commit as `feat: add supplier-neutral search contracts`.

### Task 2: Sanitized LiteAPI fixture adapter

**Files:**
- Create: `src/ReadyToGoTravel.Search/Providers/SearchProviders.cs`
- Create: `src/ReadyToGoTravel.Search/SupplierIntegrations/LiteApi/LiteApiFixtureSearchProvider.cs`
- Create: `src/ReadyToGoTravel.Search/SupplierIntegrations/LiteApi/Fixtures/hotel-search.json`
- Create: `src/ReadyToGoTravel.Search/SupplierIntegrations/LiteApi/Fixtures/flight-search.json`
- Modify: `src/ReadyToGoTravel.Search/ReadyToGoTravel.Search.csproj`
- Test: `tests/ReadyToGoTravel.Search.Tests/LiteApiFixtureContractTests.cs`

**Interfaces:**
- Consumes: Task 1 requests, results, pricing and capability registry.
- Produces: `IHotelSearchProvider.SearchAsync(...)`, `IFlightSearchProvider.SearchAsync(...)` and deterministic `LiteApiFixtureSearchProvider` results with opaque `off_` identifiers.

- [x] **Step 1: Add sanitized fixture JSON and failing adapter tests**

Tests prove hotel mapping, QF/JQ/VA flight mapping, minimum totals, tax/fee categories, expiry, requested/returned currency provenance, absence of secrets and absence of supplier references in public result records.

- [x] **Step 2: Run focused tests and confirm adapter failure**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj --filter LiteApiFixtureContractTests`

Expected: FAIL because provider interfaces and the fixture adapter do not exist.

- [x] **Step 3: Implement deterministic fixture mapping**

Load embedded JSON with `JsonUnmappedMemberHandling.Disallow`, map supplier fields into platform records, create opaque offer IDs from a SHA-256 digest of provider/environment/reference and calculate expiry from the injected clock plus the sanitized fixture lifetime. Unsupported point-of-sale, currency, route or expired fixture data returns no offers rather than inventing capability.

- [x] **Step 4: Run search tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj`

Commit as `feat: map sanitized LiteAPI search fixtures`.

### Task 3: Capability-gated search HTTP API

**Files:**
- Create: `src/ReadyToGoTravel.Search/Http/SearchContracts.cs`
- Create: `src/ReadyToGoTravel.Search/Http/SearchEndpoints.cs`
- Create: `src/ReadyToGoTravel.Search/Http/SearchHttpResults.cs`
- Modify: `src/ReadyToGoTravel.Search/SearchModule.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs`
- Modify: `src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj`
- Modify: `src/ReadyToGoTravel.Api/appsettings.json`
- Modify: `src/ReadyToGoTravel.Api/appsettings.Development.json`
- Test: `tests/ReadyToGoTravel.Search.Tests/SearchApiTests.cs`

**Interfaces:**
- Produces: `GET /api/v1/search/capabilities`, `POST /api/v1/search/hotels` and `POST /api/v1/search/flights`.
- Consumes: the Task 2 provider interfaces and the existing API correlation, rate-limit and `ProblemDetails` behavior.

- [ ] **Step 1: Build an in-process host and write failing HTTP tests**

Cover invalid dates/IATA/currency, deterministic hotel and flight fixtures, explicit offer expiry/revalidation, QF/JQ/VA observed-only capability fields, unknown JSON rejection and production fail-closed behavior.

- [ ] **Step 2: Run tests and confirm missing routes**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj --filter SearchApiTests`

Expected: FAIL because `/api/v1/search/*` routes do not exist.

- [ ] **Step 3: Implement module registration and endpoints**

`AddSearchModule(environment, enableFixtures)` rejects fixture mode for `Production`, registers the capability snapshot and fixture provider only for `Development`/`Testing`, and validates requests before provider dispatch. Unavailable capability returns `503` with `search_capability_unavailable`; validation returns `400` with a stable specific code.

- [ ] **Step 4: Compose API configuration and a search-specific limiter**

Default `appsettings.json` uses `Production` with fixtures disabled. Development uses `Sandbox` with fixtures enabled. Apply a fixed-window `search` policy of 30 requests per minute per source to the three search routes.

- [ ] **Step 5: Run API, search and full tests; commit**

Run: `dotnet test ReadyToGoTravel.slnx`

Commit as `feat: expose capability-gated search api`.

### Task 4: Blazor SSR search experience

**Files:**
- Create: `src/ReadyToGoTravel.Web/Client/SearchApiClient.cs`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Search.razor`
- Modify: `src/ReadyToGoTravel.Web/Program.cs`
- Modify: `src/ReadyToGoTravel.Web/Components/Layout/SiteHeader.razor`
- Modify: `src/ReadyToGoTravel.Web/wwwroot/app.css`
- Modify: `tests/ReadyToGoTravel.Architecture.Tests/WebBoundaryTests.cs`
- Test: `tests/ReadyToGoTravel.Web.Tests/SearchClientTests.cs`

**Interfaces:**
- Consumes: the Task 3 public `/api/v1/search` routes only.
- Produces: `/search` hotel and flight forms, minimum-total offer cards, expiry/revalidation labels and an explicit sandbox/unavailable capability message.

- [ ] **Step 1: Write failing web-boundary and client tests**

Prove the web project still references no server module, `SearchApiClient` uses only `/api/v1/search`, and a failed/unavailable API call produces a typed unavailable result rather than supplier detail leakage.

- [ ] **Step 2: Run focused tests and confirm failure**

Run: `dotnet test tests/ReadyToGoTravel.Web.Tests/ReadyToGoTravel.Web.Tests.csproj && dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj`

Expected: FAIL because the search client/page do not exist.

- [ ] **Step 3: Implement the compact SSR experience**

Add public hotel and return-flight forms with Australia/AUD defaults. Render minimum totals and included tax/fee summaries returned by the API; do not calculate totals in the browser. Display fixture results as sandbox observations and provide no booking action.

- [ ] **Step 4: Run web, architecture and full tests; commit**

Run: `dotnet test ReadyToGoTravel.slnx && dotnet build ReadyToGoTravel.slnx --no-restore --configuration Release`

Commit as `feat: add hotel and flight search shell`.

### Task 5: Pull-request CI, documentation and Slice 3 outcome

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `README.md`
- Modify: `docs/README.md`
- Modify: `docs/api/README.md`
- Modify: `docs/decisions/review-register.md`
- Modify: `docs/plans/active/PLAN-0002-mvp-delivery.md`
- Modify: `docs/superpowers/plans/2026-07-29-search-capability-registry.md`
- Create: `docs/delivery/README.md`
- Create: `docs/delivery/2026-07-29-slice-3-search-capability-outcome.md`

**Interfaces:**
- Produces: PR verification for `dev`, local search instructions, authoritative Slice 3 completion state and the requested Markdown outcome report.
- Consumes: the complete Slice 3 solution and existing verification workflow.

- [ ] **Step 1: Make CI run for pull requests targeting `dev`**

Set `pull_request.branches` to `[dev]` while retaining push verification on `main`. The existing locked restore, formatting, release build, PostgreSQL migration, full tests, docs validation and four container builds remain the required PR gate.

- [ ] **Step 2: Update navigation, API documentation and plan state**

Document fixture-only local search, the three endpoints, production fail-closed behavior and the absence of booking actions. Mark Slice 3 complete and Slice 4 next without changing any production review outcome.

- [ ] **Step 3: Write the outcome report**

Record scope delivered, public contracts, capability/price behavior, red-green evidence, verification commands/results, production gates, exclusions, commit list and Slice 4 handoff in `docs/delivery/2026-07-29-slice-3-search-capability-outcome.md`.

- [ ] **Step 4: Run fresh verification**

Run locked restore, formatting, warning-as-error release build, all tests, documentation validation, dependency vulnerability audit and all four Docker builds. Smoke-test development capability/hotel/flight endpoints and confirm production configuration returns `search_capability_unavailable` without supplier credentials.

- [ ] **Step 5: Commit**

Commit as `feat: complete search and capability slice`.

## Plan Self-review

- Spec coverage: supplier-neutral requests/results, sanitized fixtures, capability dimensions, price minimum/currency/components, expiry, Australian-carrier observations, API, web, production gating, CI and documentation each have a task.
- Placeholder scan: no implementation placeholder or unresolved product choice remains.
- Type consistency: one Search module, one capability registry, two provider interfaces and the same three `/api/v1/search` routes are used throughout.
- Scope: no booking, payment, persistence, live supplier call, combined orchestration, discount or currency conversion enters Slice 3.
