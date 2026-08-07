using Microsoft.JSInterop;

namespace ReadyToGoTravel.Web.Payments;

public sealed class HostedPaymentComponent(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? module;
    private string? sessionHandle;

    public async Task CreateAsync(string elementId, string browserToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(elementId);
        ArgumentException.ThrowIfNullOrWhiteSpace(browserToken);

        module ??= await jsRuntime.InvokeAsync<IJSObjectReference>(
            "import",
            cancellationToken,
            "./Components/Pages/Checkout.razor.js");
        sessionHandle = await module.InvokeAsync<string>(
            "createHostedPayment",
            cancellationToken,
            elementId,
            browserToken);
    }

    public async Task<HostedPaymentResult> CompleteAsync(CancellationToken cancellationToken = default)
    {
        if (module is null || sessionHandle is null)
        {
            throw new InvalidOperationException("Hosted payment has not been created.");
        }

        return await module.InvokeAsync<HostedPaymentResult>(
            "completeHostedPayment",
            cancellationToken,
            sessionHandle);
    }

    public async ValueTask DisposeAsync()
    {
        var moduleToDispose = module;
        var sessionToDispose = sessionHandle;
        module = null;
        sessionHandle = null;

        if (moduleToDispose is not null && sessionToDispose is not null)
        {
            try
            {
                await moduleToDispose.InvokeVoidAsync("disposeHostedPayment", sessionToDispose);
            }
            catch (JSDisconnectedException)
            {
                // The interactive circuit has already ended.
            }
        }

        if (moduleToDispose is not null)
        {
            await moduleToDispose.DisposeAsync();
        }
    }
}
