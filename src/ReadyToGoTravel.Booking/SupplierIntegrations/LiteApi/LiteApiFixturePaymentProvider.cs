using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReadyToGoTravel.Booking.Providers;

namespace ReadyToGoTravel.Booking.SupplierIntegrations.LiteApi;

public sealed class LiteApiFixturePaymentProvider : ICustomerPaymentProvider
{
    private static readonly JsonSerializerOptions FixtureJsonOptions = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly TimeProvider timeProvider;

    public LiteApiFixturePaymentProvider(TimeProvider? timeProvider = null)
    {
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<HostedPaymentPreparation> PrepareAsync(
        CustomerPaymentPlan plan,
        string returnKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(returnKey);
        var fixture = await LoadFixtureAsync(cancellationToken);
        var scenario = fixture.Payments.Single(value => value.Scenario == "action-required");
        return new HostedPaymentPreparation(
            scenario.PaymentReference,
            scenario.BrowserToken ?? throw new InvalidDataException("Hosted payment fixture requires a browser token."),
            timeProvider.GetUtcNow().ToUniversalTime().AddMinutes(scenario.BrowserTokenLifetimeMinutes),
            ParseStatus(scenario.Status));
    }

    public async Task<CustomerPaymentStatusResult> RetrieveAsync(
        string paymentReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paymentReference);
        var fixture = await LoadFixtureAsync(cancellationToken);
        var scenario = fixture.Payments.SingleOrDefault(value =>
            string.Equals(value.PaymentReference, paymentReference, StringComparison.Ordinal));
        return scenario is null
            ? new CustomerPaymentStatusResult(paymentReference, PaymentProviderStatus.OutcomeUnknown, null, "payment_not_found")
            : new CustomerPaymentStatusResult(
                scenario.PaymentReference,
                ParseStatus(scenario.Status),
                scenario.ProviderReturnReference,
                scenario.ErrorCode);
    }

    public Task<CustomerPaymentStatusResult> CompleteReturnAsync(
        string paymentReference,
        string completionReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(completionReference);
        return RetrieveAsync(paymentReference, cancellationToken);
    }

    private static PaymentProviderStatus ParseStatus(string value) => value switch
    {
        "ActionRequired" => PaymentProviderStatus.ActionRequired,
        "Processing" => PaymentProviderStatus.Processing,
        "Captured" => PaymentProviderStatus.Captured,
        "Failed" => PaymentProviderStatus.Failed,
        "OutcomeUnknown" => PaymentProviderStatus.OutcomeUnknown,
        _ => throw new InvalidDataException($"Unsupported payment fixture status '{value}'."),
    };

    private static async Task<PaymentFixture> LoadFixtureAsync(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith("payment-scenarios.json", StringComparison.Ordinal));
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidDataException("Embedded payment fixture was not found.");
        return await JsonSerializer.DeserializeAsync<PaymentFixture>(stream, FixtureJsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Embedded payment fixture was empty.");
    }

    private sealed record PaymentFixture(string Provider, string Environment, IReadOnlyList<PaymentFixtureScenario> Payments);

    private sealed record PaymentFixtureScenario(
        string Scenario,
        string PaymentReference,
        string Status,
        string? BrowserToken,
        int BrowserTokenLifetimeMinutes,
        string? ProviderReturnReference,
        string? ErrorCode);
}
