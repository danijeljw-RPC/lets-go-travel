<!-- markdownlint-disable MD013 -->

# Checkout, Hosted Payment and Booking Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement Slice 4 authenticated hotel, flight and combined checkout with server-side offer resolution, renewed price acceptance, provider-hosted payment, separately evidenced component bookings and safe immediate recovery while every production capability remains disabled.

**Architecture:** Add one `ReadyToGoTravel.Booking` feature module with internal Checkout, Payments, Bookings, Persistence and LiteAPI fixture areas. Consumer and Search expose narrow application contracts for owned trip/traveller data and server-side offer resolution; Booking owns durable financial and supplier-operation state in PostgreSQL. The API exposes authenticated `/api/v1/checkouts` commands and the Blazor host consumes those routes through an interactive checkout page plus a provider-scoped JavaScript wrapper.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Entity Framework Core 10, PostgreSQL 17, SQLite integration tests, Blazor interactive server rendering, JavaScript interop, `System.Text.Json`, xUnit and ASP.NET Core TestServer.

**Design:** [Checkout, Hosted Payment and Booking Design](../specs/2026-07-29-checkout-hosted-payment-booking-design.md)

**Status:** Implementation complete on `codex/slice-4-checkout-booking`; independent whole-branch review, push and `gh` pull-request creation into `dev` remain controller-owned publication steps.

## Global Constraints

- Product name is `readytogo.travel`; shortcode is `RTGT`; root namespace and project prefix are `ReadyToGoTravel`.
- Work only on `codex/slice-4-checkout-booking`; complete work is pushed and opened with `gh` as a pull request targeting `dev`.
- Add one feature-module assembly and no deployable; keep API, web, general worker and flight-reconciliation worker as the four processes.
- Booking owns schema `booking`; it does not query Consumer or Search tables.
- The web project references no server module and uses only `/api/v1`.
- Browser input contains platform offer IDs, never supplier references, provider selection, authoritative prices or booking state.
- Valid checkout composition is exactly one hotel, exactly one flight or one hotel plus one flight.
- Every offer is resolved on the server at checkout creation and immediately before hosted-payment preparation.
- A changed price, currency, material term or bookable product detail invalidates acceptance and blocks payment until the exact new revision is accepted.
- Payment and component booking states remain separate; only affirmative supplier evidence with an external reference produces `Confirmed`.
- Combined journeys retain independent component payment, confirmation, failure and recovery outcomes.
- Commands that can create an external effect require `Idempotency-Key`; a replay returns the recorded response and conflicting input returns `409 idempotency_conflict`.
- Raw PAN, CVV, sensitive authentication data, supplier credentials, reusable payment tokens and full identity-document values never enter API requests, logs or ordinary Booking tables.
- Sandbox uses sanitized deterministic LiteAPI fixtures. Production registers no payment or booking provider and returns `booking_capability_unavailable` even if a secret exists.
- Immediate provider retrieval and one internal recovery case belong to Slice 4. Webhooks, scheduled reconciliation, immutable canonical versions and notifications remain Slice 5.
- Tests precede every production behavior and each task ends in an independently reviewable local commit.

---

### Task 1: Cross-module checkout inputs and Booking domain

**Files:**

- Create: `src/ReadyToGoTravel.Consumer/Application/ConsumerBookingContext.cs`
- Modify: `src/ReadyToGoTravel.Consumer/ConsumerModule.cs`
- Test: `tests/ReadyToGoTravel.Consumer.Tests/ConsumerBookingContextTests.cs`
- Create: `src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj`
- Create: `src/ReadyToGoTravel.Booking/BookingModule.cs`
- Create: `src/ReadyToGoTravel.Booking/Domain/DomainResult.cs`
- Create: `src/ReadyToGoTravel.Booking/Checkout/CheckoutSession.cs`
- Create: `src/ReadyToGoTravel.Booking/Checkout/CheckoutRevision.cs`
- Create: `src/ReadyToGoTravel.Booking/Bookings/ComponentBooking.cs`
- Create: `src/ReadyToGoTravel.Booking/Payments/PaymentAttempt.cs`
- Create: `src/ReadyToGoTravel.Booking/Properties/AssemblyInfo.cs`
- Create: `tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj`
- Test: `tests/ReadyToGoTravel.Booking.Tests/CheckoutDomainTests.cs`
- Modify: `ReadyToGoTravel.slnx`

**Interfaces:**

- Produces: `IConsumerBookingContext.ResolveAsync(string subject, Guid tripId, IReadOnlyCollection<Guid> travellerIds, CancellationToken)` returning `ConsumerBookingContextResult` with active customer, owned active trip and owned low-risk traveller records.
- Produces: `CheckoutSession.Create(...)`, `CheckoutSession.AcceptRevision(...)`, `CheckoutSession.BeginPayment(...)`, `CheckoutSession.RecordPayment(...)`, `CheckoutSession.BeginBooking(...)`, `CheckoutSession.Recover(...)` and `CheckoutSession.Expire(...)`.
- Produces: `CheckoutStatus`, `PaymentStatus`, `ComponentBookingStatus`, `CheckoutProduct`, internal `ResolvedCheckoutOffer`, `TravellerSnapshot` and `BookingRecoveryCase`.
- Consumes: `TimeProvider`, UUIDv7 identifiers and Consumer-owned IDs only; no Consumer entity or database type crosses the contract.

- [ ] **Step 1: Add the Consumer booking-context failing tests**

```csharp
[Fact]
public async Task BookingContextReturnsOnlyAnActiveOwnedTripAndOwnedTravellers()
{
    await using var fixture = await ConsumerFixture.CreateAsync();
    var owner = await fixture.CreateActiveCustomerAsync("owner");
    var trip = await fixture.CreateTripAsync(owner.Id, "Melbourne");
    var traveller = await fixture.CreateTravellerAsync(owner.Id, "Ari", "Taylor");

    var result = await fixture.Context.ResolveAsync("owner", trip.Id, [traveller.Id], default);

    Assert.True(result.IsSuccess);
    Assert.Equal(owner.Id, result.Value!.CustomerId);
    Assert.Equal(trip.Id, result.Value.TripId);
    Assert.Equal([traveller.Id], result.Value.Travellers.Select(value => value.TravellerId));
}

[Fact]
public async Task BookingContextDoesNotRevealAnotherCustomersTrip()
{
    await using var fixture = await ConsumerFixture.CreateAsync();
    var owner = await fixture.CreateActiveCustomerAsync("owner");
    var ownersTrip = await fixture.CreateTripAsync(owner.Id, "Melbourne");
    await fixture.CreateActiveCustomerAsync("stranger");

    var result = await fixture.Context.ResolveAsync(
        "stranger",
        ownersTrip.Id,
        [],
        default);
    Assert.False(result.IsSuccess);
    Assert.Equal("trip_not_found", result.ErrorCode);
}
```

- [ ] **Step 2: Run the Consumer tests and observe the missing contract failure**

Run: `dotnet test tests/ReadyToGoTravel.Consumer.Tests/ReadyToGoTravel.Consumer.Tests.csproj --filter ConsumerBookingContextTests --no-restore --configuration Release --maxcpucount:1`

Expected: FAIL because `IConsumerBookingContext` and its implementation do not exist.

- [ ] **Step 3: Implement the narrow Consumer application contract**

Use these public contract shapes and keep the EF implementation internal:

```csharp
public interface IConsumerBookingContext
{
    Task<ConsumerBookingContextResult> ResolveAsync(
        string subject,
        Guid tripId,
        IReadOnlyCollection<Guid> travellerIds,
        CancellationToken cancellationToken = default);
}

public sealed record ConsumerBookingContext(
    Guid CustomerId,
    Guid TripId,
    IReadOnlyList<ConsumerTraveller> Travellers);

public sealed record ConsumerBookingContextResult(
    ConsumerBookingContext? Value,
    string? ErrorCode)
{
    public bool IsSuccess => ErrorCode is null;
}

public sealed record ConsumerTraveller(
    Guid TravellerId,
    string GivenName,
    string FamilyName,
    bool IsMinor,
    DateTimeOffset? GuardianAuthorityConfirmedAt);
```

Resolve the customer by verified subject and `Active` status, query the trip by customer ID and reject archived trips, then require every distinct traveller ID to belong to the same customer. Return `profile_required`, `trip_not_found` or `traveller_not_found` without revealing another owner's resource.

- [ ] **Step 4: Scaffold the Booking module and write failing domain tests**

Add the module and test projects to `ReadyToGoTravel.slnx`; reference Consumer and Search from Booking only when the application services need their public contracts. Grant internals visibility only to `ReadyToGoTravel.Booking.Tests`.

```csharp
[Fact]
public void CheckoutRejectsTwoHotelComponents()
{
    var result = CheckoutSession.Create(customerId, tripId, [hotelOffer, secondHotelOffer], travellers, clock);
    Assert.False(result.IsSuccess);
    Assert.Equal("invalid_checkout_composition", result.ErrorCode);
}

[Fact]
public void RepricingInvalidatesCustomerAcceptance()
{
    var checkout = CreateAcceptedCheckout();
    checkout.ApplyResolvedOffers([hotelOffer with { MinimumTotal = 440m, Revision = "hotel-r2" }], clock);
    Assert.Equal(CheckoutStatus.AwaitingAcceptance, checkout.Status);
    Assert.Null(checkout.AcceptedRevision);
}

[Fact]
public void CombinedJourneyIsCompleteOnlyWhenEveryComponentIsConfirmed()
{
    var checkout = CreateCombinedCheckout();
    checkout.RecordBookingResult(checkout.Components[0].Id, BookingProviderResult.Confirmed("hotel_123"), clock);
    checkout.RecordBookingResult(checkout.Components[1].Id, BookingProviderResult.Pending("flight_456"), clock);
    Assert.Equal(CheckoutStatus.BookingPending, checkout.Status);
}
```

- [ ] **Step 5: Run the Booking tests and observe the missing domain failure**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --filter CheckoutDomainTests --configuration Release --maxcpucount:1`

Expected: FAIL because the Booking domain types and state transitions do not exist.

- [ ] **Step 6: Implement the minimum state model**

Use the exact enum values from the approved design. `CheckoutSession.Create` permits `[Hotel]`, `[Flight]` and `[Hotel, Flight]` only, snapshots traveller values, assigns UUIDv7 IDs and begins at `AwaitingAcceptance`. Store revision number, terms hash, price components, transaction currency, provider binding and expiry. State methods reject backward or unsafe transitions with stable error codes rather than public setters.

Only `BookingProviderResult.Confirmed(externalReference)` can set a component to `Confirmed`. One confirmed and one failed/unknown combined component produces `RequiresSupport`, retaining both component outcomes.

- [ ] **Step 7: Run focused and architecture tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Consumer.Tests/ReadyToGoTravel.Consumer.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj --no-restore --configuration Release --maxcpucount:1`

Commit as `feat: add checkout and booking domain`.

### Task 2: Booking persistence and durable idempotency

**Files:**

- Create: `src/ReadyToGoTravel.Booking/Persistence/BookingDbContext.cs`
- Create: `src/ReadyToGoTravel.Booking/Persistence/BookingEntityConfigurations.cs`
- Create: `src/ReadyToGoTravel.Booking/Persistence/BookingDesignTimeDbContextFactory.cs`
- Create: `src/ReadyToGoTravel.Booking/Idempotency/IdempotencyRecord.cs`
- Create: `src/ReadyToGoTravel.Booking/Idempotency/IdempotencyService.cs`
- Modify: `src/ReadyToGoTravel.Booking/BookingModule.cs`
- Modify: `src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj`
- Test: `tests/ReadyToGoTravel.Booking.Tests/BookingPersistenceTests.cs`
- Test: `tests/ReadyToGoTravel.Booking.Tests/IdempotencyTests.cs`
- Modify: `tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj`

**Interfaces:**

- Produces: `BookingDbContext`, module registration with `Action<IServiceProvider, DbContextOptionsBuilder>` and readiness health check `booking_database`.
- Produces: `IIdempotencyService.ExecuteAsync<TResponse>(Guid customerId, string operation, string key, string fingerprint, Func<CancellationToken, Task<IdempotentResponse<TResponse>>> action, CancellationToken)`.
- Consumes: Task 1 aggregates and `TimeProvider`.

- [ ] **Step 1: Write failing EF mapping and snapshot tests**

```csharp
[Fact]
public async Task TravellerSnapshotDoesNotChangeWhenConsumerDataChanges()
{
    await using var fixture = await BookingDatabaseFixture.CreateAsync();
    var checkout = CheckoutFactory.CreateWithTraveller("Ari", "Taylor");
    fixture.Context.Checkouts.Add(checkout);
    await fixture.Context.SaveChangesAsync();

    var stored = await fixture.Context.Checkouts.AsNoTracking()
        .Include(value => value.TravellerSnapshots)
        .SingleAsync();

    Assert.Equal("Ari", stored.TravellerSnapshots.Single().GivenName);
    var properties = fixture.Context.Model.FindEntityType(typeof(TravellerSnapshot))!
        .GetProperties()
        .Select(property => property.Name);
    Assert.DoesNotContain(properties, property =>
        property.Contains("Passport", StringComparison.OrdinalIgnoreCase)
        || property.Contains("IdentityDocument", StringComparison.OrdinalIgnoreCase));
}

[Fact]
public async Task ProviderReturnReferenceIsUnique()
{
    await using var fixture = await BookingDatabaseFixture.CreateAsync();
    var entity = fixture.Context.Model.FindEntityType(typeof(PaymentAttempt))!;
    var index = entity.GetIndexes().Single(value =>
        value.Properties.Select(property => property.Name)
            .SequenceEqual([nameof(PaymentAttempt.ProviderReturnReference)]));

    Assert.True(index.IsUnique);
}
```

- [ ] **Step 2: Run persistence tests and observe the missing context failure**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --filter BookingPersistenceTests --configuration Release --maxcpucount:1`

Expected: FAIL because `BookingDbContext` and its mappings do not exist.

- [ ] **Step 3: Map the Booking schema and aggregates**

Set default schema `booking`. Map checkout sessions, revisions/components, acceptances, traveller snapshots, payment attempts, component bookings, idempotency records and recovery cases with snake-case table/column names. Use string enum conversions, decimal precision `(18,2)`, UTC timestamps and required maximum lengths. Add unique indexes for `(customer_id, operation, key)`, non-null provider return references and non-null provider booking references.

Register `BookingDbContext`, its readiness check and `TimeProvider`. Keep migrations explicit and the context internal except for test visibility.

- [ ] **Step 4: Write failing idempotency replay and conflict tests**

```csharp
[Fact]
public async Task SameKeyAndFingerprintReturnsRecordedResponseWithoutRunningActionAgain()
{
    await using var fixture = await BookingDatabaseFixture.CreateAsync();
    var service = fixture.Idempotency;
    var customerId = Guid.CreateVersion7();
    var executions = 0;
    Task<IdempotentResponse<string>> Run(CancellationToken _)
    {
        executions++;
        return Task.FromResult(IdempotentResponse.Completed(200, "payment-response"));
    }

    var first = await service.ExecuteAsync(customerId, "payment-session", "key-1", "hash-a", Run, default);
    var replay = await service.ExecuteAsync(customerId, "payment-session", "key-1", "hash-a", Run, default);
    Assert.Equal(first.Value, replay.Value);
    Assert.Equal(1, executions);
}

[Fact]
public async Task SameKeyWithDifferentFingerprintConflicts()
{
    await using var fixture = await BookingDatabaseFixture.CreateAsync();
    var service = fixture.Idempotency;
    var customerId = Guid.CreateVersion7();
    Task<IdempotentResponse<string>> Run(CancellationToken _) =>
        Task.FromResult(IdempotentResponse.Completed(200, "booking-response"));

    await service.ExecuteAsync(customerId, "book", "key-1", "hash-a", Run, default);
    var conflict = await service.ExecuteAsync(customerId, "book", "key-1", "hash-b", Run, default);
    Assert.Equal(IdempotencyOutcome.Conflict, conflict.Outcome);
}
```

- [ ] **Step 5: Implement canonical fingerprinting and durable replay**

Hash canonical UTF-8 request JSON with SHA-256. Persist `InProgress` before invoking the action, then persist status code and the non-secret platform response. A completed matching record replays; a different fingerprint conflicts; an `InProgress` record returns the current durable resource response without running the action. Never persist access tokens, supplier credentials or reusable payment tokens in the idempotency body. The sandbox browser token is short-lived, provider-scoped fixture material rather than a reusable payment credential.

- [ ] **Step 6: Run focused tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --no-restore --configuration Release --maxcpucount:1`

Commit as `feat: persist booking state and idempotency`.

### Task 3: Server-side offer resolution and sandbox providers

**Files:**

- Create: `src/ReadyToGoTravel.Search/Checkout/CheckoutOfferResolution.cs`
- Create: `src/ReadyToGoTravel.Search/SupplierIntegrations/LiteApi/LiteApiFixtureOfferResolver.cs`
- Create: `src/ReadyToGoTravel.Search/SupplierIntegrations/LiteApi/Fixtures/checkout-offers.json`
- Modify: `src/ReadyToGoTravel.Search/SearchModule.cs`
- Modify: `src/ReadyToGoTravel.Search/ReadyToGoTravel.Search.csproj`
- Test: `tests/ReadyToGoTravel.Search.Tests/CheckoutOfferResolverTests.cs`
- Create: `src/ReadyToGoTravel.Booking/Providers/BookingProviders.cs`
- Create: `src/ReadyToGoTravel.Booking/Payments/PaymentService.cs`
- Create: `src/ReadyToGoTravel.Booking/SupplierIntegrations/LiteApi/LiteApiFixturePaymentProvider.cs`
- Create: `src/ReadyToGoTravel.Booking/SupplierIntegrations/LiteApi/LiteApiFixtureBookingProvider.cs`
- Create: `src/ReadyToGoTravel.Booking/SupplierIntegrations/LiteApi/Fixtures/payment-scenarios.json`
- Create: `src/ReadyToGoTravel.Booking/SupplierIntegrations/LiteApi/Fixtures/booking-scenarios.json`
- Modify: `src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj`
- Modify: `src/ReadyToGoTravel.Booking/BookingModule.cs`
- Test: `tests/ReadyToGoTravel.Booking.Tests/LiteApiFixtureProviderTests.cs`

**Interfaces:**

- Produces: `ICheckoutOfferResolver.ResolveAsync(string offerId, SearchEnvironment environment, CancellationToken)` returning `CheckoutOfferResolutionResult` with a Search-owned provider-neutral `CheckoutOffer` and internal provider binding. Booking maps that contract into its internal `ResolvedCheckoutOffer`; Search never references Booking.
- Produces: `ICustomerPaymentProvider.PrepareAsync(...)`, `ICustomerPaymentProvider.RetrieveAsync(...)`, `ISupplierSettlementProvider.CreateInstructionAsync(...)`, `IBookingProvider.BookAsync(...)` and `IBookingProvider.RetrieveAsync(...)`.
- Produces: `IPaymentService.PrepareAsync(...)` and `IPaymentService.VerifyReturnAsync(...)`.
- Consumes: Task 1 state types and Task 2 persistence/idempotency.

- [ ] **Step 1: Write failing Search resolver tests**

```csharp
[Fact]
public async Task ResolverReturnsFreshPlatformRevisionForOpaqueHotelOffer()
{
    var search = await provider.SearchAsync(hotelRequest, SearchEnvironment.Sandbox);
    var result = await resolver.ResolveAsync(search.Offers.Single().OfferId, SearchEnvironment.Sandbox);
    Assert.True(result.IsSuccess);
    Assert.Equal(CheckoutOfferProduct.Hotel, result.Value!.Product);
    Assert.Equal(420m, result.Value.MinimumTotal);
    Assert.DoesNotContain("supplier", JsonSerializer.Serialize(result.Value), StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task ProductionResolutionFailsClosed()
{
    var result = await resolver.ResolveAsync("off_example", SearchEnvironment.Production);
    Assert.False(result.IsSuccess);
    Assert.Equal("booking_capability_unavailable", result.ErrorCode);
}
```

- [ ] **Step 2: Run resolver tests and observe the missing interface failure**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj --filter CheckoutOfferResolverTests --no-restore --configuration Release --maxcpucount:1`

Expected: FAIL because the checkout-resolution contract and fixture resolver do not exist.

- [ ] **Step 3: Implement deterministic unchanged and repriced resolution**

Reuse the existing opaque-offer hash rule. The embedded `checkout-offers.json` maps only sanitized fixture references to platform product detail, current price/terms revision and scenario. Return unchanged and repriced results deterministically; reject unknown, expired, disabled-market and Production requests. Register the resolver only when fixtures are enabled.

- [ ] **Step 4: Write failing hosted-payment and booking-provider tests**

```csharp
[Fact]
public async Task HostedPaymentPreparationReturnsOnlyOpaqueBrowserMaterial()
{
    var result = await paymentProvider.PrepareAsync(plan, "return-key", default);
    Assert.StartsWith("pay_", result.PaymentReference);
    Assert.StartsWith("browser_", result.BrowserToken);
    Assert.DoesNotContain("card", JsonSerializer.Serialize(result), StringComparison.OrdinalIgnoreCase);
}

[Fact]
public async Task PaymentSuccessAndBookingFailureRemainSeparate()
{
    var payment = await paymentProvider.RetrieveAsync("pay_captured_book_failed", default);
    var booking = await bookingProvider.BookAsync(flightCommand, default);
    Assert.Equal(PaymentProviderStatus.Captured, payment.Status);
    Assert.Equal(BookingProviderStatus.Failed, booking.Status);
}

[Fact]
public async Task DuplicateBookingCommandReturnsTheSameOpaqueReference()
{
    var first = await bookingProvider.BookAsync(command, default);
    var replay = await bookingProvider.BookAsync(command, default);
    Assert.Equal(first.ExternalReference, replay.ExternalReference);
}
```

- [ ] **Step 5: Implement provider-neutral payment and booking adapters**

Embed sanitized scenario files and parse with `JsonUnmappedMemberHandling.Disallow`. Payment preparation returns an opaque payment reference, provider-scoped short-lived browser token and `ActionRequired`; return verification retrieves `Processing`, `Captured`, `Failed` or `OutcomeUnknown`. Booking supports hotel/flight `Confirmed`, `Pending`, `Failed` and `Unknown` results and deterministic retrieval.

Register all fixture adapters only for Sandbox with fixtures enabled. Reject fixtures for Production and register no Production provider. Keep provider references internal to Booking responses and storage.

- [ ] **Step 6: Run Search, Booking and architecture tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Search.Tests/ReadyToGoTravel.Search.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj --no-restore --configuration Release --maxcpucount:1`

Commit as `feat: add sandbox checkout providers`.

### Task 4: Authenticated checkout orchestration and API

**Files:**

- Create: `src/ReadyToGoTravel.Booking/Application/CheckoutService.cs`
- Create: `src/ReadyToGoTravel.Booking/Http/BookingContracts.cs`
- Create: `src/ReadyToGoTravel.Booking/Http/BookingHttpResults.cs`
- Create: `src/ReadyToGoTravel.Booking/Http/BookingEndpoints.cs`
- Modify: `src/ReadyToGoTravel.Booking/BookingModule.cs`
- Modify: `src/ReadyToGoTravel.Api/Program.cs`
- Modify: `src/ReadyToGoTravel.Api/ReadyToGoTravel.Api.csproj`
- Modify: `src/ReadyToGoTravel.Api/appsettings.json`
- Modify: `src/ReadyToGoTravel.Api/appsettings.Development.json`
- Modify: `src/ReadyToGoTravel.Api/packages.lock.json`
- Test: `tests/ReadyToGoTravel.Booking.Tests/BookingApiTests.cs`
- Create: `tests/ReadyToGoTravel.Booking.Tests/TestAuthenticationHandler.cs`

**Interfaces:**

- Produces: authenticated `POST /api/v1/checkouts`, `GET /api/v1/checkouts/{checkoutId}`, `POST /api/v1/checkouts/{checkoutId}/acceptance`, `/payment-session`, `/payment-return`, `/book` and `/recover`.
- Consumes: Tasks 1–3 contracts, existing authentication policy, `ProblemDetails`, strict JSON handling and correlation IDs.

- [ ] **Step 1: Build the in-process API fixture and write failing ownership/composition tests**

Configure TestServer with test authentication, one shared SQLite connection, Consumer/Search/Booking modules, JSON unknown-member rejection and a no-op `checkout` rate limiter. Create an active profile, owned trip and travellers before checkout commands.

```csharp
[Fact]
public async Task AnonymousCheckoutIsRejected()
{
    var response = await application.Client.PostAsJsonAsync("/api/v1/checkouts", request);
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task AnotherCustomerCannotDiscoverCheckout()
{
    var checkout = await application.CreateCheckoutAsync("owner");
    application.SetSubject("stranger");
    await application.ActivateProfileAsync();
    var response = await application.Client.GetAsync($"/api/v1/checkouts/{checkout.Id}");
    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    Assert.Equal("checkout_not_found", (await response.Content.ReadFromJsonAsync<Problem>())!.Code);
}
```

- [ ] **Step 2: Write failing hotel, flight and combined happy-path tests**

For each composition: create checkout with `Idempotency-Key`, accept its returned revision, prepare payment with a new key, post the fixture return, submit booking with a new key and assert the final response. Hotel and flight end `Completed`; combined contains two independently `Confirmed` components and one aggregate `Completed` state.

- [ ] **Step 3: Write failing repricing, replay and mismatch tests**

Cover:

- payment preparation returning `409 price_acceptance_required` and a new revision after a fixture reprices;
- the same payment-session key returning the first response without a second provider call;
- a reused key with changed JSON returning `409 idempotency_conflict`;
- duplicate payment returns converging on one payment attempt;
- captured payment plus failed booking producing `RequiresSupport` and `RefundRequired` component/payment state;
- pending booking recovery becoming confirmed after safe retrieval; and
- unknown recovery creating exactly one internal recovery case.

- [ ] **Step 4: Run API tests and observe the missing routes failure**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --filter BookingApiTests --no-restore --configuration Release --maxcpucount:1`

Expected: FAIL because the Slice 4 routes and application orchestration do not exist.

- [ ] **Step 5: Implement CheckoutService and route contracts**

Use request records that accept trip ID, selected platform offer IDs, traveller assignments and booking-only age/minor context. Resolve `sub` to Consumer context; resolve offers through Search; create immutable snapshots and current revision; then persist through Booking.

Every mutating route validates state before provider work. Require `Idempotency-Key` for checkout creation, payment-session creation, payment return and booking submission; use stable operation names in the stored uniqueness tuple. Never accept an authoritative amount, provider or supplier reference from JSON.

Return `201` for checkout creation, `200` for reads and replayed commands, `400` for validation, `404 checkout_not_found`, `409` for state/acceptance/idempotency conflicts and `503 booking_capability_unavailable` when no configured provider exists. Include `code` and `correlationId`; include `retryAfterSeconds` for still-pending retrieval.

- [ ] **Step 6: Compose the API and fail-closed configuration**

Reference Booking from the API, configure its context with the existing `ConnectionStrings:Consumer` product database, add the `checkout` fixed-window policy at 20 requests per minute per source and map endpoints after authentication/authorization.

Default configuration uses `Booking:Environment=Production` and `Booking:EnableFixtures=false`. Development uses `Sandbox` and fixtures enabled. Module startup rejects `Production` with fixtures enabled.

- [ ] **Step 7: Run Booking, API and full tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Booking.Tests/ReadyToGoTravel.Booking.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Api.Tests/ReadyToGoTravel.Api.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test ReadyToGoTravel.slnx --no-restore --configuration Release --maxcpucount:1`

Commit as `feat: expose idempotent checkout api`.

### Task 5: Blazor checkout and hosted-payment wrapper

**Files:**

- Create: `src/ReadyToGoTravel.Web/Client/BookingApiClient.cs`
- Create: `src/ReadyToGoTravel.Web/Payments/HostedPaymentComponent.cs`
- Create: `src/ReadyToGoTravel.Web/Payments/HostedPaymentResult.cs`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor`
- Create: `src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor.js`
- Modify: `src/ReadyToGoTravel.Web/Program.cs`
- Modify: `src/ReadyToGoTravel.Web/Components/Pages/Search.razor`
- Modify: `src/ReadyToGoTravel.Web/Components/Layout/SiteHeader.razor`
- Modify: `src/ReadyToGoTravel.Web/wwwroot/app.css`
- Modify: `tests/ReadyToGoTravel.Architecture.Tests/WebBoundaryTests.cs`
- Create: `tests/ReadyToGoTravel.Web.Tests/BookingClientTests.cs`
- Create: `tests/ReadyToGoTravel.Web.Tests/HostedPaymentBoundaryTests.cs`

**Interfaces:**

- Consumes: Task 4 public API only.
- Produces: `BookingApiClient`, `/checkout`, authenticated offer selection, revision acceptance, hosted-payment JavaScript invocation, payment return verification, component booking submission and recovery display.
- Produces: JS functions `createHostedPayment(elementId, browserToken)`, `completeHostedPayment(sessionHandle)` and `disposeHostedPayment(sessionHandle)`.

- [ ] **Step 1: Write failing public-boundary and client tests**

```csharp
[Fact]
public void WebConsumesBookingOnlyThroughPublicV1Api()
{
    var source = File.ReadAllText(RepositoryFiles.FromRoot("src/ReadyToGoTravel.Web/Client/BookingApiClient.cs"));
    Assert.Contains("/api/v1/checkouts", source, StringComparison.Ordinal);
}

[Fact]
public async Task BookingClientSendsIdempotencyKeyButNotAnAuthoritativePrice()
{
    var handler = new RecordingHttpMessageHandler();
    var client = new BookingApiClient(new HttpClient(handler)
    {
        BaseAddress = new Uri("https://api.test"),
    });
    var request = new CreateCheckoutInput(tripId, [offerId], [travellerId]);

    await client.CreateCheckoutAsync(request, "checkout-key", default);
    Assert.Equal("checkout-key", handler.LastRequest!.Headers.GetValues("Idempotency-Key").Single());
    Assert.DoesNotContain("minimumTotal", handler.LastBody, StringComparison.OrdinalIgnoreCase);
}
```

Keep the existing `WebProjectReferencesNoServerAssembly` test and add path checks for checkout, acceptance, payment-session, payment-return, book and recover.

- [ ] **Step 2: Write failing hosted-payment security-boundary tests**

Read the Razor, C# and JavaScript sources and assert:

- wrapper input is limited to element ID and browser token;
- no `cardNumber`, `cvv`, `cvc`, PAN field or custom card input exists;
- no production LiteAPI script URL or API key is present;
- browser completion is sent to `/payment-return` rather than treated as booking confirmation; and
- `Booking confirmed` rendering is conditional on aggregate `Completed` with all components confirmed.

- [ ] **Step 3: Run focused tests and observe missing client/wrapper failure**

Run: `dotnet test tests/ReadyToGoTravel.Web.Tests/ReadyToGoTravel.Web.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj --no-restore --configuration Release --maxcpucount:1`

Expected: FAIL because Booking API client, checkout page and hosted-payment wrapper do not exist.

- [ ] **Step 4: Implement authenticated API client and selection flow**

Register `BookingApiClient` with `ApiAccessTokenHandler`. Search selection actions send only offer IDs to `/checkout`; combined selection allows at most one hotel and one flight. Checkout loads owned trips/travellers through the existing Consumer client, then creates/loads the server-side checkout.

Generate a fresh UUID idempotency key per user intent and retain it across a retry of that intent. Never regenerate a key merely because an HTTP response was lost.

- [ ] **Step 5: Implement the interactive checkout and JS module**

Apply `@attribute [Authorize]` and `@rendermode InteractiveServer` only to Checkout. Display server-authoritative component price, currency, terms revision and sandbox notice. Acceptance posts the exact revision. The local fixture JS module returns an opaque fixture result; the page posts it to `/payment-return`, then calls `/book` and renders payment plus each component state separately.

Provide controls for retrying safe state retrieval through `/recover`. Never label payment success or a single confirmed component as complete combined booking.

- [ ] **Step 6: Run web, architecture and full tests; commit**

Run: `dotnet test tests/ReadyToGoTravel.Web.Tests/ReadyToGoTravel.Web.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test tests/ReadyToGoTravel.Architecture.Tests/ReadyToGoTravel.Architecture.Tests.csproj --no-restore --configuration Release --maxcpucount:1 && dotnet test ReadyToGoTravel.slnx --no-restore --configuration Release --maxcpucount:1`

Commit as `feat: add hosted checkout experience`.

### Task 6: PostgreSQL migration, documentation, verification and PR

**Files:**

- Create: `src/ReadyToGoTravel.Booking/Persistence/Migrations/20260729030000_InitialBookingSchema.cs`
- Create: `src/ReadyToGoTravel.Booking/Persistence/Migrations/20260729030000_InitialBookingSchema.Designer.cs`
- Create: `src/ReadyToGoTravel.Booking/Persistence/Migrations/BookingDbContextModelSnapshot.cs`
- Modify: `src/ReadyToGoTravel.Booking/packages.lock.json`
- Modify: `README.md`
- Modify: `docs/README.md`
- Modify: `docs/api/README.md`
- Modify: `docs/deployment/local-development.md`
- Modify: `docs/decisions/review-register.md`
- Modify: `docs/plans/active/PLAN-0002-mvp-delivery.md`
- Modify: `docs/delivery/README.md`
- Create: `docs/delivery/2026-07-29-slice-4-checkout-hosted-payment-booking-outcome.md`
- Modify: `docs/superpowers/plans/2026-07-29-checkout-hosted-payment-booking.md`

**Interfaces:**

- Produces: explicit Booking schema migration, local sandbox checkout instructions, authoritative Slice 4 completion state, requested Markdown outcome report and a GitHub pull request into `dev`.
- Consumes: complete Tasks 1–5 implementation and the repository CI verification surface.

- [x] **Step 1: Generate and inspect the initial Booking migration**

Run:

```bash
dotnet tool restore
dotnet ef migrations add InitialBookingSchema \
  --project src/ReadyToGoTravel.Booking/ReadyToGoTravel.Booking.csproj \
  --context ReadyToGoTravel.Booking.Persistence.BookingDbContext \
  --output-dir Persistence/Migrations
```

Rename the generated migration pair to the two exact `20260729030000_*` paths above and set the designer's `Migration` attribute to `20260729030000_InitialBookingSchema` so the committed migration identifier is deterministic.

Inspect the migration for schema `booking`, required foreign-key ownership inside the module, decimal precision, enum lengths and unique indexes for idempotency and provider return/booking references. It must not create or alter Consumer/Search tables.

- [x] **Step 2: Update public and operational documentation**

Document the seven authenticated checkout routes, required idempotency headers, separate payment/component states, sandbox scenarios, local startup commands and Production fail-closed behavior. Keep OI-0002, OI-0003 and OI-0006 open; do not claim merchant, carrier booking, PCI or live supplier approval.

Mark Slice 4 complete and Slice 5 next in PLAN-0002 and the review register. Do not implement or imply webhook, scheduled reconciliation, immutable history or notification completion.

- [x] **Step 3: Write the requested Slice 4 outcome report**

Create `docs/delivery/2026-07-29-slice-4-checkout-hosted-payment-booking-outcome.md` containing:

- delivered scope and module/data ownership;
- public API and web flow;
- offer revalidation and renewed acceptance behavior;
- hosted-payment and PCI boundary;
- separate payment/component booking states;
- hotel, flight and combined journey results;
- idempotency, duplicate-return and immediate recovery behavior;
- test-first evidence and exact verification results;
- commit sequence and branch/PR reference;
- unchanged production gates and explicit exclusions; and
- Slice 5 handoff for webhooks, scheduled reconciliation, immutable versions and notifications.

- [x] **Step 4: Run fresh repository verification**

Run:

```bash
dotnet restore ReadyToGoTravel.slnx --locked-mode
dotnet format ReadyToGoTravel.slnx --no-restore --verify-no-changes
dotnet build ReadyToGoTravel.slnx --no-restore --configuration Release --warnaserror --maxcpucount:1
dotnet test ReadyToGoTravel.slnx --no-restore --configuration Release --maxcpucount:1
bash scripts/validate-docs.sh
markdownlint-cli2 "docs/**/*.md"
dotnet list ReadyToGoTravel.slnx package --vulnerable --include-transitive
docker build -f src/ReadyToGoTravel.Api/Dockerfile -t rtgt-api:slice4 .
docker build -f src/ReadyToGoTravel.Web/Dockerfile -t rtgt-web:slice4 .
docker build -f src/ReadyToGoTravel.Worker/Dockerfile -t rtgt-worker:slice4 .
docker build -f src/ReadyToGoTravel.FlightReconciliation.Worker/Dockerfile -t rtgt-flight-worker:slice4 .
```

Apply both Consumer and Booking migrations to a fresh PostgreSQL database. Smoke-test the development hotel, flight, combined, repriced, duplicate-return and pending-recovery paths. Start the API with Production configuration and confirm checkout returns `503 booking_capability_unavailable` without supplier/payment credentials.

- [x] **Step 5: Record verification evidence and commit completion**

Update this plan's status and execution record plus the outcome report with actual command results, test totals and smoke outcomes. Run `git diff --check` and verify only intended Slice 4 paths are staged.

Commit as `feat: complete checkout and booking slice`.

- [ ] **Step 6: Push and create the pull request into `dev`**

Run:

```bash
git push -u origin codex/slice-4-checkout-booking
gh pr create \
  --base dev \
  --head codex/slice-4-checkout-booking \
  --title "feat: complete checkout and booking slice" \
  --body-file /tmp/rtgt-slice-4-pr.md
```

The PR body summarizes scope, tests, production gates, explicit Slice 5 exclusions and links to the outcome report. Confirm `gh pr checks` finds the repository CI run; do not merge the PR.

## Execution Record

Implementation completed on 2026-07-29 through delegated tasks with independent review/fix gates. The deterministic Booking migration `20260729030000_InitialBookingSchema` and the existing Consumer migration were applied to a fresh PostgreSQL 17.10 database. Inspection confirmed only the expected `consumer` and `booking` tables plus the Booking uniqueness constraints.

Fresh local verification passed locked restore, solution formatting, a warning-as-error Release build, all 149 tests, repository documentation validation, changed-document markdown lint and all four container builds. Live Development smoke recorded hotel `Completed`, QF flight `BookingPending`, combined `BookingPending`, duplicate return convergence, safe pending recovery and AUD 289.40-to-309.40 repricing; Production returned HTTP `503 booking_capability_unavailable`.

The full unconfigured `markdownlint-cli2 "docs/**/*.md"` command still reports 345 pre-existing repository violations. The vulnerable-package audit was not run because the approval boundary rejected transmitting repository package metadata to the external advisory service. These are recorded verification limitations, not silently treated as passes.

Live PostgreSQL smoke exposed three sanitized-fixture integration defects: moving expiry falsely invalidated unchanged acceptance, provider booking references collided between checkouts and provider payment/return references collided between checkouts. Each received an observed focused RED, minimum fix and focused/full GREEN verification. The [Slice 4 outcome report](../../delivery/2026-07-29-slice-4-checkout-hosted-payment-booking-outcome.md) contains the complete evidence and exclusions.

## Plan Self-review

- Spec coverage: module ownership, Consumer/Search contracts, checkout composition, offer revalidation, renewed acceptance, traveller snapshots, hosted payment, separate states, component orchestration, idempotency, duplicate returns, immediate recovery, API, web, persistence, production gating, documentation and PR delivery each have a task.
- Placeholder scan: no unfinished marker, deferred implementation instruction or unspecified error-handling step remains.
- Type consistency: the same `IConsumerBookingContext`, Search-owned `ICheckoutOfferResolver`/`CheckoutOffer`, Booking-owned domain types, provider contracts, checkout/payment/component states and seven `/api/v1/checkouts` routes are used throughout without a circular project reference.
- Scope: cancellation/refund execution, webhooks, scheduled reconciliation, immutable canonical versions, notifications, support tickets, live suppliers, Stripe, Duffel, native mobile and production activation remain outside Slice 4.
