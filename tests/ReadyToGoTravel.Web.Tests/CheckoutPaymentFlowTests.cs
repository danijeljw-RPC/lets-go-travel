using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Microsoft.JSInterop;
using ReadyToGoTravel.Web.Client;
using ReadyToGoTravel.Web.Payments;

namespace ReadyToGoTravel.Web.Tests;

public sealed class CheckoutPaymentFlowTests
{
    [Fact]
    public async Task AcceptedPaymentReturnDoesNotBookUntilAuthoritativePaymentIsCaptured()
    {
        var handler = new SequencedHandler(
            Response(HttpStatusCode.Accepted, Checkout("ActionRequired")),
            Response(HttpStatusCode.OK, Checkout("Captured", status: "Completed")));
        var hosted = await CreateHostedPaymentAsync();
        var page = CreatePage(handler, hosted, Checkout("ActionRequired", includeSession: true));

        await InvokeAsync(page, "CompletePaymentAndBookAsync");

        Assert.Equal(["/api/v1/checkouts/00000000-0000-0000-0000-000000000001/payment-return"], handler.Paths);
    }

    [Fact]
    public async Task RecoveryToCapturedContinuesBookingWithAUsableIntentKey()
    {
        var handler = new SequencedHandler(
            Response(HttpStatusCode.OK, Checkout("Captured")),
            Response(HttpStatusCode.OK, Checkout("Captured", status: "Completed")));
        var page = CreatePage(handler, await CreateHostedPaymentAsync(), Checkout("Processing"));

        await InvokeAsync(page, "RecoverAsync");

        Assert.Equal(
            [
                "/api/v1/checkouts/00000000-0000-0000-0000-000000000001/recover",
                "/api/v1/checkouts/00000000-0000-0000-0000-000000000001/book",
            ],
            handler.Paths);
        Assert.False(string.IsNullOrWhiteSpace(handler.Requests[1].Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task ReloadedActionRequiredCheckoutCanReplayPaymentSessionSafely()
    {
        var handler = new SequencedHandler(
            Response(HttpStatusCode.OK, Checkout("ActionRequired", includeSession: true)));
        var page = CreatePage(handler, await CreateHostedPaymentAsync(), Checkout("ActionRequired"));

        await InvokeAsync(page, "ResumePaymentAsync");

        Assert.Equal(
            ["/api/v1/checkouts/00000000-0000-0000-0000-000000000001/payment-session"],
            handler.Paths);
        Assert.False(string.IsNullOrWhiteSpace(handler.Requests[0].Headers.GetValues("Idempotency-Key").Single()));
    }

    [Fact]
    public async Task HostedWrapperCanBeCreatedAgainAfterDisposal()
    {
        var runtime = new FakeJsRuntime();
        var wrapper = new HostedPaymentComponent(runtime);

        await wrapper.CreateAsync("payment-one", "browser-one");
        await wrapper.DisposeAsync();
        await wrapper.CreateAsync("payment-two", "browser-two");

        Assert.Equal(2, runtime.ImportCalls);
    }

    [Fact]
    public async Task AcceptanceRetryRetainsTheSameKeyForTheSameRevisionIntent()
    {
        var accepted = Checkout("Captured", status: "ReadyForPayment");
        var handler = new SequencedHandler(
            Response(HttpStatusCode.OK, accepted),
            Response(HttpStatusCode.OK, accepted));
        var page = CreatePage(handler, await CreateHostedPaymentAsync(), accepted);

        await InvokeAsync(page, "AcceptRevisionAsync");
        await InvokeAsync(page, "AcceptRevisionAsync");

        var first = handler.Requests[0].Headers.GetValues("Idempotency-Key").Single();
        var replay = handler.Requests[1].Headers.GetValues("Idempotency-Key").Single();
        Assert.Equal(first, replay);
    }

    private static object CreatePage(
        HttpMessageHandler handler,
        HostedPaymentComponent hosted,
        CheckoutView checkout)
    {
        var pageType = PageType();
        var page = Activator.CreateInstance(pageType)!;
        SetProperty(page, "BookingApi", new BookingApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.example.test"),
        }));
        SetProperty(page, "HostedPayment", hosted);
        SetField(page, "checkout", checkout);
        SetField(page, "hostedPaymentReady", true);
        return page;
    }

    private static async Task<HostedPaymentComponent> CreateHostedPaymentAsync()
    {
        var hosted = new HostedPaymentComponent(new FakeJsRuntime());
        await hosted.CreateAsync("hosted-payment", "browser-token");
        return hosted;
    }

    private static async Task InvokeAsync(object page, string methodName)
    {
        var method = PageType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);
        await Assert.IsAssignableFrom<Task>(method.Invoke(page, null));
    }

    private static void SetProperty(object target, string name, object value)
    {
        var property = PageType().GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.NotNull(property);
        property.SetValue(target, value);
    }

    private static void SetField(object target, string name, object value)
    {
        var field = PageType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        field.SetValue(target, value);
    }

    private static Type PageType() => typeof(BookingApiClient).Assembly.GetType(
        "ReadyToGoTravel.Web.Components.Pages.Checkout",
        throwOnError: true)!;

    private static CheckoutView Checkout(
        string paymentStatus,
        bool includeSession = false,
        string status = "PaymentPending") => new(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            status,
            new CheckoutRevisionView(1, 420m, "AUD", "terms-r1", DateTimeOffset.UtcNow.AddMinutes(15), []),
            new CheckoutAcceptanceView(1, 420m, "AUD", "terms-r1", "checkout-v1", DateTimeOffset.UtcNow),
            [],
            [new CheckoutComponentView(Guid.NewGuid(), "Hotel", "hotel-offer", status == "Completed" ? "Confirmed" : "PaymentPending", null)],
            [new CheckoutPaymentView(Guid.NewGuid(), 420m, "AUD", paymentStatus, null)],
            includeSession ? new HostedPaymentSessionView("pay-one", "browser-token", DateTimeOffset.UtcNow.AddMinutes(15)) : null,
            0,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMinutes(15),
            paymentStatus is "ActionRequired" or "Processing" ? 15 : null);

    private static HttpResponseMessage Response(HttpStatusCode status, CheckoutView checkout) => new(status)
    {
        Content = JsonContent.Create(checkout),
    };

    private sealed class SequencedHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> responses = new(responses);

        public List<string> Paths { get; } = [];

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Requests.Add(request);
            return Task.FromResult(responses.Dequeue());
        }
    }

    private sealed class FakeJsRuntime : IJSRuntime
    {
        public int ImportCalls { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, default, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Assert.Equal("import", identifier);
            ImportCalls++;
            return ValueTask.FromResult((TValue)(object)new FakeModule());
        }
    }

    private sealed class FakeModule : IJSObjectReference
    {
        private bool disposed;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, default, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (identifier == "disposeHostedPayment")
            {
                return ValueTask.FromResult(default(TValue)!);
            }

            object result = identifier switch
            {
                "createHostedPayment" => "session-handle",
                "completeHostedPayment" => new HostedPaymentResult("opaque-completion"),
                _ => new object(),
            };
            return ValueTask.FromResult((TValue)result);
        }

        public ValueTask DisposeAsync()
        {
            disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
