using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Consumer;
using ReadyToGoTravel.Consumer.Http;
using ReadyToGoTravel.Search;
using ReadyToGoTravel.Search.Capabilities;
using ReadyToGoTravel.Search.Checkout;

namespace ReadyToGoTravel.Booking.Tests;

public sealed class BookingApiTests
{
    private static readonly string[] HotelOfferIds = ["hotel-offer"];
    private static readonly string[] FlightOfferIds = ["flight-offer"];
    private static readonly string[] CombinedOfferIds = ["hotel-offer", "flight-offer"];

    [Fact]
    public async Task AnonymousCheckoutIsRejected()
    {
        await using var application = await TestApplication.CreateAsync();

        var response = await application.PostAsync(
            "/api/v1/checkouts",
            new
            {
                tripId = Guid.CreateVersion7(),
                offerIds = HotelOfferIds,
                travellerAssignments = Array.Empty<object>(),
            },
            "create-anonymous");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnotherCustomerCannotDiscoverCheckout()
    {
        await using var application = await TestApplication.CreateAsync();
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        application.SetSubject("stranger");
        await application.ActivateProfileAsync();

        var response = await application.Client.GetAsync($"/api/v1/checkouts/{checkout.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("checkout_not_found", (await response.Content.ReadFromJsonAsync<Problem>())!.Code);
    }

    public static TheoryData<string[]> HappyPathCompositions => new()
    {
        HotelOfferIds,
        FlightOfferIds,
        CombinedOfferIds,
    };

    [Theory]
    [MemberData(nameof(HappyPathCompositions))]
    public async Task HotelFlightAndCombinedJourneysComplete(string[] offerIds)
    {
        await using var application = await TestApplication.CreateAsync();

        var checkout = await application.CreateCheckoutAsync("owner", offerIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-happy");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-happy");
        checkout = await application.BookAsync(checkout.Id, "book-happy");

        Assert.Equal("Completed", checkout.Status);
        Assert.Equal(offerIds.Length, checkout.Components.Length);
        Assert.All(checkout.Components, component => Assert.Equal("Confirmed", component.Status));
    }

    [Fact]
    public async Task RepricingRequiresAcceptanceOfTheNewRevisionBeforePayment()
    {
        var resolver = new DeterministicOfferResolver { RepriceOnSecondResolution = true };
        await using var application = await TestApplication.CreateAsync(resolver: resolver);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);

        var response = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-session",
            new { },
            "payment-session-reprice");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("price_acceptance_required", payload.RootElement.GetProperty("code").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("checkout").GetProperty("currentRevision").GetProperty("number").GetInt32());
    }

    [Fact]
    public async Task PaymentSessionReplayReturnsTheFirstResponseWithoutASecondProviderCall()
    {
        var payment = new DeterministicPaymentProvider();
        await using var application = await TestApplication.CreateAsync(payment: payment);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);

        var first = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-session",
            new { },
            "payment-session-replay");
        var replay = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-session",
            new { },
            "payment-session-replay");

        first.EnsureSuccessStatusCode();
        replay.EnsureSuccessStatusCode();
        Assert.Equal(await first.Content.ReadAsStringAsync(), await replay.Content.ReadAsStringAsync());
        Assert.Equal(1, payment.PrepareCalls);
    }

    [Fact]
    public async Task ReusedCheckoutKeyWithChangedJsonReturnsConflict()
    {
        await using var application = await TestApplication.CreateAsync();
        var owned = await application.CreateOwnedContextAsync("owner");

        var first = await application.PostCheckoutAsync(owned, HotelOfferIds, "create-mismatch");
        var mismatch = await application.PostCheckoutAsync(owned, FlightOfferIds, "create-mismatch");

        first.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        Assert.Equal("idempotency_conflict", (await mismatch.Content.ReadFromJsonAsync<Problem>())!.Code);
    }

    [Fact]
    public async Task ReplayedCheckoutCreationReturnsOkWithTheOriginalResource()
    {
        await using var application = await TestApplication.CreateAsync();
        var owned = await application.CreateOwnedContextAsync("owner");

        var first = await application.PostCheckoutAsync(owned, HotelOfferIds, "create-replay");
        var replay = await application.PostCheckoutAsync(owned, HotelOfferIds, "create-replay");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(
            (await first.Content.ReadFromJsonAsync<Checkout>())!.Id,
            (await replay.Content.ReadFromJsonAsync<Checkout>())!.Id);
    }

    [Fact]
    public async Task DistinctPaymentKeysShareOneDurableExternalOperation()
    {
        var payment = new DeterministicPaymentProvider { BlockPreparation = true };
        await using var application = await TestApplication.CreateAsync(payment: payment);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);

        var firstTask = application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-session",
            new { },
            "payment-session-concurrent-one");
        await payment.PrepareStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var secondTask = application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-session",
            new { },
            "payment-session-concurrent-two");
        await Task.WhenAny(secondTask, Task.Delay(TimeSpan.FromSeconds(1)));
        payment.ReleasePreparation();
        _ = await firstTask;
        _ = await secondTask;

        Assert.Equal(1, payment.PrepareCalls);
        var replay = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-session",
            new { },
            "payment-session-concurrent-two");
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.NotNull((await replay.Content.ReadFromJsonAsync<Checkout>())!.PaymentSession);
    }

    [Fact]
    public async Task DuplicatePaymentReturnsConvergeOnOnePaymentAttempt()
    {
        var payment = new DeterministicPaymentProvider();
        await using var application = await TestApplication.CreateAsync(payment: payment);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-duplicate-return");

        await application.ReturnPaymentAsync(checkout.Id, "payment-return-first");
        await application.ReturnPaymentAsync(checkout.Id, "payment-return-duplicate");

        await using var scope = application.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(1, await database.PaymentAttempts.CountAsync());
        Assert.Equal(1, payment.RetrieveCalls);
    }

    [Fact]
    public async Task PaymentReturnAcceptsOpaqueCompletionReferenceAndRejectsClientStatus()
    {
        var payment = new DeterministicPaymentProvider();
        await using var application = await TestApplication.CreateAsync(payment: payment);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-completion-input");

        var accepted = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-return",
            new { completionReference = "opaque-hosted-completion" },
            "payment-return-completion-input");
        var rejected = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-return",
            new { completionReference = "opaque-hosted-completion", status = "Captured" },
            "payment-return-client-status");

        accepted.EnsureSuccessStatusCode();
        Assert.Equal("opaque-hosted-completion", payment.LastCompletionReference);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task ConcurrentDifferentKeyPaymentReturnsCannotRegressCapturedToProcessing()
    {
        var payment = new DeterministicPaymentProvider { BlockFirstRetrieval = true };
        payment.RetrieveResults.Enqueue(new CustomerPaymentStatusResult(
            "pay-captured",
            PaymentProviderStatus.Processing,
            "return-processing",
            null));
        payment.RetrieveResults.Enqueue(new CustomerPaymentStatusResult(
            "pay-captured",
            PaymentProviderStatus.Captured,
            "return-captured",
            null));
        await using var application = await TestApplication.CreateAsync(payment: payment);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-return-race");

        var stale = application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-return",
            new { completionReference = "return-processing" },
            "payment-return-race-stale");
        await payment.FirstRetrieveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var winner = await application.PostAsync(
            $"/api/v1/checkouts/{checkout.Id}/payment-return",
            new { completionReference = "return-captured" },
            "payment-return-race-winner");
        winner.EnsureSuccessStatusCode();
        payment.ReleaseFirstRetrieval();
        _ = await stale;

        var current = await application.Client.GetFromJsonAsync<Checkout>($"/api/v1/checkouts/{checkout.Id}");
        Assert.Equal("Captured", Assert.Single(current!.Payments).Status);
    }

    [Fact]
    public async Task CapturedPaymentAndPartialBookingFailureRequireRefundAndSupport()
    {
        var booking = new DeterministicBookingProvider();
        booking.BookResults[CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
            BookingProviderStatus.Failed,
            null,
            "flight_unavailable");
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", CombinedOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-partial");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-partial");

        checkout = await application.BookAsync(checkout.Id, "book-partial");

        Assert.Equal("RequiresSupport", checkout.Status);
        Assert.Contains(checkout.Components, component => component.Status == "Confirmed");
        Assert.Contains(checkout.Components, component => component.Status == "RefundRequired");
        Assert.Equal("RefundRequired", Assert.Single(checkout.Payments).Status);
    }

    [Fact]
    public async Task CapturedPaymentAndSingleBookingFailureRequireRefundAndSupport()
    {
        var booking = new DeterministicBookingProvider();
        booking.BookResults[CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
            BookingProviderStatus.Failed,
            null,
            "flight_unavailable");
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", FlightOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-single-failure");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-single-failure");

        checkout = await application.BookAsync(checkout.Id, "book-single-failure");

        Assert.Equal("RequiresSupport", checkout.Status);
        Assert.Equal("RefundRequired", Assert.Single(checkout.Components).Status);
        Assert.Equal("RefundRequired", Assert.Single(checkout.Payments).Status);
    }

    [Fact]
    public async Task PendingBookingRecoveryCanBecomeConfirmed()
    {
        var booking = new DeterministicBookingProvider();
        booking.BookResults[CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
            BookingProviderStatus.Pending,
            "book-flight-pending",
            null);
        booking.RetrieveResults["book-flight-pending"] = new BookingProviderExecutionResult(
            BookingProviderStatus.Confirmed,
            "book-flight-pending",
            null);
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", FlightOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-pending");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-pending");
        checkout = await application.BookAsync(checkout.Id, "book-pending");
        Assert.Equal("BookingPending", checkout.Status);

        var response = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        response.EnsureSuccessStatusCode();
        checkout = (await response.Content.ReadFromJsonAsync<Checkout>())!;

        Assert.Equal("Completed", checkout.Status);
        Assert.Equal("Confirmed", Assert.Single(checkout.Components).Status);
    }

    [Fact]
    public async Task ConfirmedRecoveryWithoutExternalReferenceCreatesOneSupportCase()
    {
        var booking = new DeterministicBookingProvider();
        booking.BookResults[CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
            BookingProviderStatus.Pending,
            "book-flight-pending-no-confirmation",
            null);
        booking.RetrieveResults["book-flight-pending-no-confirmation"] = new BookingProviderExecutionResult(
            BookingProviderStatus.Confirmed,
            null,
            null);
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", FlightOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-missing-confirmation");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-missing-confirmation");
        checkout = await application.BookAsync(checkout.Id, "book-missing-confirmation");

        var first = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        var second = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        var recovered = (await second.Content.ReadFromJsonAsync<Checkout>())!;
        Assert.Equal("RequiresSupport", recovered.Status);
        Assert.Equal(1, recovered.RecoveryCaseCount);
    }

    [Fact]
    public async Task ConcurrentRecoveryCreatesExactlyOneDurableRecoveryCase()
    {
        var booking = new DeterministicBookingProvider { BlockUntilTwoRetrieveCalls = true };
        booking.BookResults[CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
            BookingProviderStatus.Pending,
            "book-flight-pending-race",
            null);
        booking.RetrieveResults["book-flight-pending-race"] = new BookingProviderExecutionResult(
            BookingProviderStatus.Confirmed,
            "book-flight-contradiction",
            null);
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", FlightOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-recovery-race");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-recovery-race");
        checkout = await application.BookAsync(checkout.Id, "book-recovery-race");

        var first = application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        var second = application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        var responses = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(
            responses.All(response => response.IsSuccessStatusCode),
            string.Join("\n", await Task.WhenAll(responses.Select(response => response.Content.ReadAsStringAsync()))));
        await using var scope = application.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(1, await database.RecoveryCases.CountAsync());
    }

    [Fact]
    public async Task ContradictoryBookingRecoveryReferenceCreatesARecoveryCase()
    {
        var booking = new DeterministicBookingProvider();
        booking.BookResults[CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
            BookingProviderStatus.Pending,
            "book-flight-pending",
            null);
        booking.RetrieveResults["book-flight-pending"] = new BookingProviderExecutionResult(
            BookingProviderStatus.Confirmed,
            "book-other-customer",
            null);
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", FlightOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-booking-contradiction");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-booking-contradiction");
        checkout = await application.BookAsync(checkout.Id, "book-booking-contradiction");

        var response = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        response.EnsureSuccessStatusCode();
        checkout = (await response.Content.ReadFromJsonAsync<Checkout>())!;

        Assert.Equal("RequiresSupport", checkout.Status);
        Assert.Equal(1, checkout.RecoveryCaseCount);
    }

    [Fact]
    public async Task ContradictoryPaymentRecoveryReferenceCreatesARecoveryCase()
    {
        var payment = new DeterministicPaymentProvider();
        payment.RetrieveResults.Enqueue(new CustomerPaymentStatusResult(
            "pay-captured",
            PaymentProviderStatus.Processing,
            "return-processing",
            null));
        payment.RetrieveResults.Enqueue(new CustomerPaymentStatusResult(
            "pay-other-customer",
            PaymentProviderStatus.Captured,
            "return-other-customer",
            null));
        await using var application = await TestApplication.CreateAsync(payment: payment);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-payment-contradiction");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-payment-contradiction");
        Assert.Equal("PaymentPending", checkout.Status);

        var response = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        response.EnsureSuccessStatusCode();
        checkout = (await response.Content.ReadFromJsonAsync<Checkout>())!;

        Assert.Equal("RequiresSupport", checkout.Status);
        Assert.Equal(1, checkout.RecoveryCaseCount);
    }

    [Fact]
    public async Task BookingAgeContextIsDurablySnapshotted()
    {
        var booking = new DeterministicBookingProvider();
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);

        var response = await application.Client.GetAsync($"/api/v1/checkouts/{checkout.Id}");
        response.EnsureSuccessStatusCode();
        checkout = (await response.Content.ReadFromJsonAsync<Checkout>())!;

        Assert.Equal(30, Assert.Single(checkout.Travellers).AgeAtTravel);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-age-context");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-age-context");
        _ = await application.BookAsync(checkout.Id, "book-age-context");
        Assert.Equal(30, booking.LastAgeAtTravel);
    }

    [Fact]
    public async Task UnknownRecoveryCreatesExactlyOneInternalRecoveryCase()
    {
        var booking = new DeterministicBookingProvider();
        booking.BookResults[CheckoutOfferProduct.Hotel] = new BookingProviderExecutionResult(
            BookingProviderStatus.Unknown,
            "book-hotel-unknown",
            "booking_outcome_unknown");
        await using var application = await TestApplication.CreateAsync(booking: booking);
        var checkout = await application.CreateCheckoutAsync("owner", HotelOfferIds);
        checkout = await application.AcceptAsync(checkout);
        checkout = await application.PreparePaymentAsync(checkout.Id, "payment-session-unknown");
        checkout = await application.ReturnPaymentAsync(checkout.Id, "payment-return-unknown");
        checkout = await application.BookAsync(checkout.Id, "book-unknown");

        var first = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });
        var second = await application.Client.PostAsJsonAsync($"/api/v1/checkouts/{checkout.Id}/recover", new { });

        first.EnsureSuccessStatusCode();
        second.EnsureSuccessStatusCode();
        await using var scope = application.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
        Assert.Equal(1, await database.RecoveryCases.CountAsync());
    }

    [Fact]
    public async Task ClientOwnedPriceAndProviderFieldsAreRejected()
    {
        await using var application = await TestApplication.CreateAsync();
        var owned = await application.CreateOwnedContextAsync("owner");

        var response = await application.PostAsync(
            "/api/v1/checkouts",
            new
            {
                tripId = owned.TripId,
                offerIds = HotelOfferIds,
                travellerAssignments = new[] { new { travellerId = owned.TravellerId, ageAtTravel = 30 } },
                authoritativeTotal = 1,
                provider = "client-selected",
            },
            "create-client-owned-fields");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ProductionWithoutBookingCapabilitiesReturnsServiceUnavailable()
    {
        await using var application = await TestApplication.CreateAsync(
            environment: SearchEnvironment.Production,
            enableFixtures: false,
            registerTestCapabilities: false);
        var owned = await application.CreateOwnedContextAsync("owner");

        var response = await application.PostCheckoutAsync(owned, HotelOfferIds, "create-production-unavailable");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("booking_capability_unavailable", (await response.Content.ReadFromJsonAsync<Problem>())!.Code);
    }

    [Fact]
    public async Task CheckoutRateLimiterAllowsTwentyRequestsPerMinuteThenRejects()
    {
        await using var application = await TestApplication.CreateAsync(useRealCheckoutRateLimiter: true);
        application.SetSubject("owner");
        var checkoutId = Guid.CreateVersion7();

        for (var requestNumber = 0; requestNumber < 20; requestNumber++)
        {
            var allowed = await application.Client.GetAsync($"/api/v1/checkouts/{checkoutId}");
            Assert.NotEqual(HttpStatusCode.TooManyRequests, allowed.StatusCode);
        }

        var rejected = await application.Client.GetAsync($"/api/v1/checkouts/{checkoutId}");
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
    }

    private sealed record Problem(string Code, string CorrelationId);

    private sealed record OwnedContext(Guid TripId, Guid TravellerId);

    private sealed record Checkout(
        Guid Id,
        string Status,
        Revision CurrentRevision,
        Component[] Components,
        Payment[] Payments,
        PaymentSession? PaymentSession,
        Traveller[] Travellers,
        int RecoveryCaseCount);

    private sealed record Revision(int Number, decimal Total, string Currency, string TermsHash);

    private sealed record Component(string Product, string Status);

    private sealed record Payment(string Status);

    private sealed record PaymentSession(string PaymentReference, string BrowserToken);

    private sealed record Traveller(Guid TravellerId, bool IsMinor, int? AgeAtTravel);

    private sealed record IdResponse(Guid Id);

    private sealed class TestApplication : IAsyncDisposable
    {
        private readonly WebApplication application;
        private readonly string databasePath;

        private TestApplication(WebApplication application, string databasePath, HttpClient client)
        {
            this.application = application;
            this.databasePath = databasePath;
            Client = client;
        }

        public HttpClient Client { get; }

        public IServiceProvider Services => application.Services;

        public static async Task<TestApplication> CreateAsync(
            DeterministicOfferResolver? resolver = null,
            DeterministicPaymentProvider? payment = null,
            DeterministicBookingProvider? booking = null,
            SearchEnvironment environment = SearchEnvironment.Sandbox,
            bool enableFixtures = true,
            bool registerTestCapabilities = true,
            bool useRealCheckoutRateLimiter = false)
        {
            var databasePath = Path.Combine(
                Path.GetTempPath(),
                $"ready-to-go-travel-booking-api-{Guid.CreateVersion7():N}.db");
            var connectionString = $"Data Source={databasePath};Foreign Keys=True;Default Timeout=30";
            var clock = new FixedTimeProvider(new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
            if (registerTestCapabilities)
            {
                resolver ??= new DeterministicOfferResolver();
                payment ??= new DeterministicPaymentProvider();
                booking ??= new DeterministicBookingProvider();
            }

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
            builder.WebHost.UseTestServer();
            builder.Services.AddSingleton<TimeProvider>(clock);
            builder.Services.ConfigureHttpJsonOptions(options =>
                options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow);
            builder.Services.AddProblemDetails(options =>
                options.CustomizeProblemDetails = context =>
                {
                    context.ProblemDetails.Extensions.TryAdd("code", "request_failed");
                    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
                });
            builder.Services.AddAuthentication(TestAuthenticationHandler.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationHandler.AuthenticationScheme,
                    _ => { });
            builder.Services.AddAuthorization(options =>
                options.AddPolicy("consumer", policy => policy.RequireAuthenticatedUser().RequireClaim("sub")));
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddPolicy("checkout", context => useRealCheckoutRateLimiter
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "test",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            QueueLimit = 0,
                            Window = TimeSpan.FromMinutes(1),
                        })
                    : RateLimitPartition.GetNoLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "test"));
            });
            builder.Services.AddConsumerModule((_, options) => options.UseSqlite(connectionString));
            builder.Services.AddSearchModule(environment, enableFixtures);
            builder.Services.AddBookingModule(
                (_, options) => options.UseSqlite(connectionString),
                environment,
                enableFixtures);
            if (registerTestCapabilities)
            {
                builder.Services.Replace(ServiceDescriptor.Singleton<ICheckoutOfferResolver>(resolver!));
                builder.Services.Replace(ServiceDescriptor.Singleton<ICustomerPaymentProvider>(payment!));
                builder.Services.Replace(ServiceDescriptor.Singleton<IBookingProvider>(booking!));
            }

            var application = builder.Build();
            application.UseExceptionHandler();
            application.UseAuthentication();
            application.UseAuthorization();
            application.UseRateLimiter();
            var api = application.MapGroup("/api/v1");
            api.MapConsumerEndpoints();
            MapBookingEndpointsWhenPresent(api);
            await application.StartAsync();

            await using (var scope = application.Services.CreateAsyncScope())
            {
                var consumerType = typeof(ConsumerModule).Assembly.GetType(
                    "ReadyToGoTravel.Consumer.Persistence.ConsumerDbContext",
                    throwOnError: true)!;
                var consumer = (DbContext)scope.ServiceProvider.GetRequiredService(consumerType);
                var consumerCreator = consumer.GetService<IRelationalDatabaseCreator>();
                await consumerCreator.CreateTablesAsync();
                var bookingDatabase = scope.ServiceProvider.GetRequiredService<BookingDbContext>();
                var bookingCreator = bookingDatabase.GetService<IRelationalDatabaseCreator>();
                await bookingCreator.CreateTablesAsync();
            }

            return new TestApplication(application, databasePath, application.GetTestClient());
        }

        public void SetSubject(string subject)
        {
            Client.DefaultRequestHeaders.Remove(TestAuthenticationHandler.SubjectHeader);
            Client.DefaultRequestHeaders.Add(TestAuthenticationHandler.SubjectHeader, subject);
        }

        public async Task ActivateProfileAsync()
        {
            var response = await Client.PutAsJsonAsync(
                "/api/v1/me",
                new { preferredLocale = "en-AU", adultConfirmed = true });
            response.EnsureSuccessStatusCode();
        }

        public async Task<OwnedContext> CreateOwnedContextAsync(string subject)
        {
            SetSubject(subject);
            await ActivateProfileAsync();
            var tripResponse = await Client.PostAsJsonAsync(
                "/api/v1/trips",
                new
                {
                    title = "Melbourne",
                    primaryDestination = "Melbourne",
                    startDate = "2026-10-10",
                    endDate = "2026-10-12",
                });
            tripResponse.EnsureSuccessStatusCode();
            var trip = (await tripResponse.Content.ReadFromJsonAsync<IdResponse>())!;
            var travellerResponse = await Client.PostAsJsonAsync(
                "/api/v1/travellers",
                new
                {
                    givenName = "Ari",
                    familyName = "Taylor",
                    relationshipLabel = "Self",
                    isMinor = false,
                    guardianAuthorityConfirmed = false,
                });
            travellerResponse.EnsureSuccessStatusCode();
            var traveller = (await travellerResponse.Content.ReadFromJsonAsync<IdResponse>())!;
            return new OwnedContext(trip.Id, traveller.Id);
        }

        public async Task<HttpResponseMessage> PostCheckoutAsync(
            OwnedContext owned,
            string[] offerIds,
            string key) => await PostAsync(
                "/api/v1/checkouts",
                new
                {
                    tripId = owned.TripId,
                    offerIds,
                    travellerAssignments = new[]
                    {
                        new { travellerId = owned.TravellerId, ageAtTravel = (int?)30 },
                    },
                },
                key);

        public async Task<Checkout> CreateCheckoutAsync(string subject, string[] offerIds)
        {
            var owned = await CreateOwnedContextAsync(subject);
            var response = await PostCheckoutAsync(owned, offerIds, $"create-{Guid.CreateVersion7():N}");
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            return (await response.Content.ReadFromJsonAsync<Checkout>())!;
        }

        public async Task<Checkout> AcceptAsync(Checkout checkout)
        {
            var response = await Client.PostAsJsonAsync(
                $"/api/v1/checkouts/{checkout.Id}/acceptance",
                new
                {
                    revisionNumber = checkout.CurrentRevision.Number,
                    acceptedTotal = checkout.CurrentRevision.Total,
                    currency = checkout.CurrentRevision.Currency,
                    termsHash = checkout.CurrentRevision.TermsHash,
                    policyVersion = "checkout-v1",
                });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<Checkout>())!;
        }

        public async Task<Checkout> PreparePaymentAsync(Guid checkoutId, string key)
        {
            var response = await PostAsync($"/api/v1/checkouts/{checkoutId}/payment-session", new { }, key);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<Checkout>())!;
        }

        public async Task<Checkout> ReturnPaymentAsync(Guid checkoutId, string key)
        {
            var response = await PostAsync(
                $"/api/v1/checkouts/{checkoutId}/payment-return",
                new { completionReference = "opaque-hosted-completion" },
                key);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<Checkout>())!;
        }

        public async Task<Checkout> BookAsync(Guid checkoutId, string key)
        {
            var response = await PostAsync($"/api/v1/checkouts/{checkoutId}/book", new { }, key);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<Checkout>())!;
        }

        public async Task<HttpResponseMessage> PostAsync(string path, object body, string idempotencyKey)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(body),
            };
            request.Headers.Add("Idempotency-Key", idempotencyKey);
            return await Client.SendAsync(request);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await application.DisposeAsync();
            File.Delete(databasePath);
        }

        private static void MapBookingEndpointsWhenPresent(RouteGroupBuilder api)
        {
            var endpoints = Type.GetType(
                "ReadyToGoTravel.Booking.Http.BookingEndpoints, ReadyToGoTravel.Booking",
                throwOnError: false);
            var map = endpoints?.GetMethod(
                "MapBookingEndpoints",
                BindingFlags.Public | BindingFlags.Static,
                binder: null,
                types: [typeof(RouteGroupBuilder)],
                modifiers: null);
            map?.Invoke(null, [api]);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class DeterministicOfferResolver : ICheckoutOfferResolver
    {
        private readonly Dictionary<string, int> resolutions = [];

        public bool RepriceOnSecondResolution { get; init; }

        public Task<CheckoutOfferResolutionResult> ResolveAsync(
            string offerId,
            SearchEnvironment environment,
            CancellationToken cancellationToken = default)
        {
            resolutions.TryGetValue(offerId, out var count);
            resolutions[offerId] = ++count;
            var product = offerId.StartsWith("hotel", StringComparison.Ordinal)
                ? CheckoutOfferProduct.Hotel
                : CheckoutOfferProduct.Flight;
            var repriced = RepriceOnSecondResolution && count > 1;
            var price = product == CheckoutOfferProduct.Hotel ? 420m : 309.40m;
            if (repriced)
            {
                price += 25m;
            }

            var offer = new CheckoutOffer(
                product,
                offerId,
                product == CheckoutOfferProduct.Hotel ? "Harbour Lane Hotel" : "QF401 SYD-MEL",
                price,
                "AUD",
                repriced ? $"{offerId}-terms-v2" : $"{offerId}-terms-v1",
                repriced ? $"{offerId}-r2" : $"{offerId}-r1",
                new DateTimeOffset(2026, 7, 29, 13, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero));
            return Task.FromResult(CheckoutOfferResolutionResult.Success(offer, $"{product}-fixture"));
        }
    }

    private sealed class DeterministicPaymentProvider : ICustomerPaymentProvider
    {
        private readonly TaskCompletionSource preparationRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource firstRetrievalRelease = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int retrieveCalls;

        public int PrepareCalls { get; private set; }

        public int RetrieveCalls => Volatile.Read(ref retrieveCalls);

        public bool BlockPreparation { get; init; }

        public bool BlockFirstRetrieval { get; init; }

        public string? LastCompletionReference { get; private set; }

        public TaskCompletionSource PrepareStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource FirstRetrieveStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public Queue<CustomerPaymentStatusResult> RetrieveResults { get; } = [];

        public async Task<HostedPaymentPreparation> PrepareAsync(
            CustomerPaymentPlan plan,
            string returnKey,
            CancellationToken cancellationToken = default)
        {
            PrepareCalls++;
            PrepareStarted.TrySetResult();
            if (BlockPreparation)
            {
                await preparationRelease.Task.WaitAsync(cancellationToken);
            }

            return new HostedPaymentPreparation(
                "pay-captured",
                "browser-session-token",
                new DateTimeOffset(2026, 7, 29, 12, 15, 0, TimeSpan.Zero),
                PaymentProviderStatus.ActionRequired);
        }

        public async Task<CustomerPaymentStatusResult> RetrieveAsync(
            string paymentReference,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref retrieveCalls);
            if (RetrieveResults.Count > 0)
            {
                var result = RetrieveResults.Dequeue();
                if (BlockFirstRetrieval && call == 1)
                {
                    FirstRetrieveStarted.TrySetResult();
                    await firstRetrievalRelease.Task.WaitAsync(cancellationToken);
                }

                return result;
            }

            return new CustomerPaymentStatusResult(
                paymentReference,
                PaymentProviderStatus.Captured,
                "return-captured",
                null);
        }

        public Task<CustomerPaymentStatusResult> CompleteReturnAsync(
            string paymentReference,
            string completionReference,
            CancellationToken cancellationToken = default)
        {
            LastCompletionReference = completionReference;
            return RetrieveAsync(paymentReference, cancellationToken);
        }

        public void ReleasePreparation() => preparationRelease.TrySetResult();

        public void ReleaseFirstRetrieval() => firstRetrievalRelease.TrySetResult();
    }

    private sealed class DeterministicBookingProvider : IBookingProvider
    {
        private readonly TaskCompletionSource twoRetrievalsStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int retrieveCalls;

        public bool BlockUntilTwoRetrieveCalls { get; init; }

        public Dictionary<CheckoutOfferProduct, BookingProviderExecutionResult> BookResults { get; } = new()
        {
            [CheckoutOfferProduct.Hotel] = new BookingProviderExecutionResult(
                BookingProviderStatus.Confirmed,
                "book-hotel-confirmed",
                null),
            [CheckoutOfferProduct.Flight] = new BookingProviderExecutionResult(
                BookingProviderStatus.Confirmed,
                "book-flight-confirmed",
                null),
        };

        public Dictionary<string, BookingProviderExecutionResult> RetrieveResults { get; } = [];

        public int? LastAgeAtTravel { get; private set; }

        public Task<BookingProviderExecutionResult> BookAsync(
            BookingCommand command,
            CancellationToken cancellationToken = default)
        {
            LastAgeAtTravel = command.Travellers?.Single().AgeAtTravel;
            var product = command.Product == ReadyToGoTravel.Booking.Checkout.CheckoutProduct.Hotel
                ? CheckoutOfferProduct.Hotel
                : CheckoutOfferProduct.Flight;
            return Task.FromResult(BookResults[product]);
        }

        public async Task<BookingProviderExecutionResult> RetrieveAsync(
            string externalReference,
            CancellationToken cancellationToken = default)
        {
            if (BlockUntilTwoRetrieveCalls)
            {
                if (Interlocked.Increment(ref retrieveCalls) == 2)
                {
                    twoRetrievalsStarted.TrySetResult();
                }

                await twoRetrievalsStarted.Task.WaitAsync(cancellationToken);
            }

            return RetrieveResults[externalReference];
        }
    }
}
