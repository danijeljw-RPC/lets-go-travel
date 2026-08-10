namespace ReadyToGoTravel.Web.Tests;

public sealed class HostedPaymentBoundaryTests
{
    [Fact]
    public void HostedWrapperAcceptsOnlyElementIdAndBrowserToken()
    {
        var wrapper = Read("src/ReadyToGoTravel.Web/Payments/HostedPaymentComponent.cs");
        var script = Read("src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor.js");

        Assert.Contains("CreateAsync(string elementId, string browserToken", wrapper, StringComparison.Ordinal);
        Assert.Contains("createHostedPayment", wrapper, StringComparison.Ordinal);
        Assert.Contains("export function createHostedPayment(elementId, browserToken)", script, StringComparison.Ordinal);
        Assert.Contains("export function completeHostedPayment(sessionHandle)", script, StringComparison.Ordinal);
        Assert.Contains("export function disposeHostedPayment(sessionHandle)", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutContainsNoCustomCardCaptureOrProductionProviderMaterial()
    {
        var source = string.Join('\n',
            Read("src/ReadyToGoTravel.Web/Payments/HostedPaymentComponent.cs"),
            Read("src/ReadyToGoTravel.Web/Payments/HostedPaymentResult.cs"),
            Read("src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor"),
            Read("src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor.js"));

        Assert.DoesNotContain("cardNumber", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvv", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cvc", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("liteapi.com", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input type=\"text\" name=\"pan\"", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BrowserCompletionReturnsToServerBeforeBooking()
    {
        var page = Read("src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor");
        var client = Read("src/ReadyToGoTravel.Web/Client/BookingApiClient.cs");

        Assert.Contains("BookingApi.ReturnPaymentAsync", page, StringComparison.Ordinal);
        Assert.Contains("BookingApi.BookAsync", page, StringComparison.Ordinal);
        Assert.True(
            page.IndexOf("BookingApi.ReturnPaymentAsync", StringComparison.Ordinal) <
            page.IndexOf("BookingApi.BookAsync", StringComparison.Ordinal));
        Assert.Contains("/payment-return", client, StringComparison.Ordinal);
    }

    [Fact]
    public void BookingConfirmedRequiresCompletedAggregateAndAllConfirmedComponents()
    {
        var page = Read("src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor");

        Assert.Contains("Booking confirmed", page, StringComparison.Ordinal);
        Assert.Contains("checkout.Status == \"Completed\"", page, StringComparison.Ordinal);
        Assert.Contains("checkout.Components.All(component => component.Status == \"Confirmed\")", page, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutIsTheOnlyAuthenticatedInteractiveCheckoutSurface()
    {
        var checkout = Read("src/ReadyToGoTravel.Web/Components/Pages/Checkout.razor");
        var search = Read("src/ReadyToGoTravel.Web/Components/Pages/Search.razor");

        Assert.Contains("@attribute [Authorize]", checkout, StringComparison.Ordinal);
        Assert.Contains("@rendermode InteractiveServer", checkout, StringComparison.Ordinal);
        Assert.DoesNotContain("@rendermode InteractiveServer", search, StringComparison.Ordinal);
        Assert.Contains("hotelOfferId", search, StringComparison.Ordinal);
        Assert.Contains("flightOfferId", search, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckoutRetainsTheSameIdempotencyKeyForARetriedUserIntent()
    {
        var pageType = typeof(ReadyToGoTravel.Web.Client.BookingApiClient).Assembly.GetType(
            "ReadyToGoTravel.Web.Components.Pages.Checkout",
            throwOnError: true)!;
        var page = Activator.CreateInstance(pageType)!;
        var keyFor = pageType.GetMethod(
            "KeyFor",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;

        var first = Assert.IsType<string>(keyFor.Invoke(page, ["payment-session:checkout:revision-1"]));
        var retry = Assert.IsType<string>(keyFor.Invoke(page, ["payment-session:checkout:revision-1"]));
        var nextIntent = Assert.IsType<string>(keyFor.Invoke(page, ["payment-session:checkout:revision-2"]));

        Assert.Equal(first, retry);
        Assert.NotEqual(first, nextIntent);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRoot(), path));

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ReadyToGoTravel.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
