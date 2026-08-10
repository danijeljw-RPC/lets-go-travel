using ReadyToGoTravel.Consumer.Locale;

namespace ReadyToGoTravel.Consumer.Customers;

internal sealed class Customer
{
    private Customer()
    {
    }

    public Guid Id { get; private set; }

    public string Subject { get; private set; } = string.Empty;

    public CustomerStatus Status { get; private set; }

    public string PreferredLocale { get; private set; } = SupportedLocales.Default;

    public string DisplayCurrency { get; private set; } = "AUD";

    public DateTimeOffset AdultConfirmedAt { get; private set; }

    public string AdultPolicyVersion { get; private set; } = "2026-07";

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public static DomainResult<Customer> Create(
        string subject,
        string locale,
        bool adultConfirmed,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!adultConfirmed)
        {
            return DomainResult<Customer>.Failure("adult_confirmation_required");
        }

        if (string.IsNullOrWhiteSpace(subject))
        {
            return DomainResult<Customer>.Failure("subject_required");
        }

        if (!SupportedLocales.Contains(locale))
        {
            return DomainResult<Customer>.Failure("unsupported_locale");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        return DomainResult<Customer>.Success(new Customer
        {
            Id = Guid.CreateVersion7(now),
            Subject = subject,
            Status = CustomerStatus.Active,
            PreferredLocale = SupportedLocales.Normalize(locale),
            AdultConfirmedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    public DomainResult<Customer> UpdateLocale(string locale, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (!SupportedLocales.Contains(locale))
        {
            return DomainResult<Customer>.Failure("unsupported_locale");
        }

        PreferredLocale = SupportedLocales.Normalize(locale);
        UpdatedAt = timeProvider.GetUtcNow().ToUniversalTime();
        return DomainResult<Customer>.Success(this);
    }

    /// <summary>
    /// Customer-initiated account closure. Idempotent: closing an already-closed account is a
    /// no-op success rather than an error, and never overwrites the original ClosedAtUtc.
    /// ConsumerBookingContextResolver already filters booking-context resolution to
    /// Status == Active, so this alone denies further customer-scoped access immediately - the
    /// concrete, testable control this repository owns (Keycloak owns credential/session
    /// termination separately; see docs/security/identity-and-access.md).
    /// </summary>
    public void Close(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status == CustomerStatus.Closed)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        Status = CustomerStatus.Closed;
        ClosedAtUtc = now;
        UpdatedAt = now;
    }

    /// <summary>
    /// Retention minimisation for an eligible closed account (see ConsumerRetentionSweepProcessor
    /// for the eligibility check - no protected booking/support evidence). Resets the only
    /// personal-preference fields this record holds; Id/Subject/Status/ClosedAtUtc are preserved
    /// exactly as the retention baseline requires ("stable internal identifiers preserve evidence
    /// relationships without retaining unnecessary data").
    /// </summary>
    internal void MinimiseProfile(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        PreferredLocale = SupportedLocales.Default;
        DisplayCurrency = "AUD";
        UpdatedAt = timeProvider.GetUtcNow().ToUniversalTime();
    }
}

internal enum CustomerStatus
{
    Active,
    Suspended,
    Closed,
}
