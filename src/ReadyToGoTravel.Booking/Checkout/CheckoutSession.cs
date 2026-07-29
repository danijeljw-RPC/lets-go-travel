using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Domain;
using ReadyToGoTravel.Booking.Payments;

namespace ReadyToGoTravel.Booking.Checkout;

public enum CheckoutStatus
{
    AwaitingAcceptance,
    ReadyForPayment,
    PaymentPending,
    BookingPending,
    Completed,
    Failed,
    RequiresSupport,
    Expired,
}

public enum CheckoutProduct
{
    Hotel,
    Flight,
}

internal sealed record ResolvedCheckoutOffer(
    CheckoutProduct Product,
    string OfferId,
    string ProviderBinding,
    string ProductDetail,
    decimal MinimumTotal,
    string Currency,
    string TermsHash,
    string Revision,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResolvedAt);

public sealed class TravellerSnapshot
{
    public TravellerSnapshot(
        Guid travellerId,
        string givenName,
        string familyName,
        bool isMinor,
        DateTimeOffset? guardianAuthorityConfirmedAt)
        : this(Guid.Empty, travellerId, givenName, familyName, isMinor, guardianAuthorityConfirmedAt)
    {
    }

    private TravellerSnapshot(
        Guid id,
        Guid travellerId,
        string givenName,
        string familyName,
        bool isMinor,
        DateTimeOffset? guardianAuthorityConfirmedAt)
    {
        Id = id;
        TravellerId = travellerId;
        GivenName = givenName;
        FamilyName = familyName;
        IsMinor = isMinor;
        GuardianAuthorityConfirmedAt = guardianAuthorityConfirmedAt;
    }

    public Guid Id { get; }

    public Guid TravellerId { get; }

    public string GivenName { get; }

    public string FamilyName { get; }

    public bool IsMinor { get; }

    public DateTimeOffset? GuardianAuthorityConfirmedAt { get; }

    internal static TravellerSnapshot CopyOf(TravellerSnapshot source, DateTimeOffset now) => new(
        Guid.CreateVersion7(now),
        source.TravellerId,
        source.GivenName,
        source.FamilyName,
        source.IsMinor,
        source.GuardianAuthorityConfirmedAt);
}

public sealed record CheckoutAcceptance(
    int RevisionNumber,
    decimal AcceptedTotal,
    string Currency,
    string TermsHash,
    string PolicyVersion,
    DateTimeOffset AcceptedAt);

public sealed class BookingRecoveryCase
{
    internal BookingRecoveryCase(Guid id, Guid? componentBookingId, string reason, DateTimeOffset createdAt)
    {
        Id = id;
        ComponentBookingId = componentBookingId;
        Reason = reason;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid? ComponentBookingId { get; }

    public string Reason { get; }

    public DateTimeOffset CreatedAt { get; }
}

public sealed class CheckoutSession
{
    private readonly List<CheckoutRevision> revisions = [];
    private readonly List<TravellerSnapshot> travellerSnapshots = [];
    private readonly List<ComponentBooking> components = [];
    private readonly List<PaymentAttempt> paymentAttempts = [];
    private readonly List<BookingRecoveryCase> recoveryCases = [];

    private CheckoutSession(Guid id, Guid customerId, Guid tripId, CheckoutRevision revision, DateTimeOffset now)
    {
        Id = id;
        CustomerId = customerId;
        TripId = tripId;
        revisions.Add(revision);
        Status = CheckoutStatus.AwaitingAcceptance;
        CreatedAt = now;
        UpdatedAt = now;
        ExpiresAt = revision.ExpiresAt;
    }

    private CheckoutSession(
        Guid id,
        Guid customerId,
        Guid tripId,
        CheckoutStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        DateTimeOffset expiresAt)
    {
        Id = id;
        CustomerId = customerId;
        TripId = tripId;
        Status = status;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        ExpiresAt = expiresAt;
    }

    public Guid Id { get; }

    public Guid CustomerId { get; }

    public Guid TripId { get; }

    public CheckoutStatus Status { get; private set; }

    public IReadOnlyList<CheckoutRevision> Revisions => revisions;

    public CheckoutRevision CurrentRevision => revisions[^1];

    public CheckoutAcceptance? AcceptedRevision { get; private set; }

    public IReadOnlyList<TravellerSnapshot> TravellerSnapshots => travellerSnapshots;

    public IReadOnlyList<ComponentBooking> Components => components;

    public IReadOnlyList<PaymentAttempt> PaymentAttempts => paymentAttempts;

    public IReadOnlyList<BookingRecoveryCase> RecoveryCases => recoveryCases;

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    internal static DomainResult<CheckoutSession> Create(
        Guid customerId,
        Guid tripId,
        IReadOnlyCollection<ResolvedCheckoutOffer> offers,
        IReadOnlyCollection<TravellerSnapshot> travellers,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(travellers);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (!HasValidComposition(offers))
        {
            return DomainResult<CheckoutSession>.Failure("invalid_checkout_composition");
        }

        if (travellers.Count == 0 || travellers.Select(value => value.TravellerId).Distinct().Count() != travellers.Count)
        {
            return DomainResult<CheckoutSession>.Failure("invalid_checkout_travellers");
        }

        if (offers.Any(value => value.ExpiresAt <= now))
        {
            return DomainResult<CheckoutSession>.Failure("checkout_offer_expired");
        }

        if (offers.Select(value => value.Currency).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            return DomainResult<CheckoutSession>.Failure("checkout_currency_mismatch");
        }

        var revision = CheckoutRevision.Create(1, offers, timeProvider);
        var checkout = new CheckoutSession(Guid.CreateVersion7(now), customerId, tripId, revision, now);
        checkout.travellerSnapshots.AddRange(travellers.Select(value => TravellerSnapshot.CopyOf(value, now)));
        checkout.components.AddRange(revision.Components.Select(value => ComponentBooking.Create(value, now)));
        return DomainResult<CheckoutSession>.Success(checkout);
    }

    internal DomainResult<CheckoutSession> ApplyResolvedOffers(
        IReadOnlyCollection<ResolvedCheckoutOffer> offers,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(offers);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (Status is not (CheckoutStatus.AwaitingAcceptance or CheckoutStatus.ReadyForPayment))
        {
            return DomainResult<CheckoutSession>.Failure("checkout_cannot_be_repriced");
        }

        if (!HasValidComposition(offers) || offers.Select(value => value.Currency).Distinct(StringComparer.Ordinal).Count() != 1)
        {
            return DomainResult<CheckoutSession>.Failure("invalid_checkout_composition");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (offers.Any(value => value.ExpiresAt <= now))
        {
            return DomainResult<CheckoutSession>.Failure("checkout_offer_expired");
        }

        var candidate = CheckoutRevision.Create(CurrentRevision.Number + 1, offers, timeProvider);
        if (!HasMaterialOfferChange(candidate))
        {
            return DomainResult<CheckoutSession>.Success(this);
        }

        revisions.Add(candidate);
        ExpiresAt = candidate.ExpiresAt;
        AcceptedRevision = null;
        Status = CheckoutStatus.AwaitingAcceptance;
        UpdatedAt = now;
        return DomainResult<CheckoutSession>.Success(this);
    }

    public DomainResult<CheckoutSession> AcceptRevision(
        int revisionNumber,
        decimal acceptedTotal,
        string currency,
        string termsHash,
        string policyVersion,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status != CheckoutStatus.AwaitingAcceptance)
        {
            return DomainResult<CheckoutSession>.Failure("checkout_not_awaiting_acceptance");
        }

        if (revisionNumber != CurrentRevision.Number ||
            acceptedTotal != CurrentRevision.Total ||
            !string.Equals(currency, CurrentRevision.TransactionCurrency, StringComparison.Ordinal) ||
            !string.Equals(termsHash, CurrentRevision.TermsHash, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(policyVersion))
        {
            return DomainResult<CheckoutSession>.Failure("checkout_acceptance_mismatch");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (now >= ExpiresAt)
        {
            return DomainResult<CheckoutSession>.Failure("checkout_expired");
        }

        AcceptedRevision = new CheckoutAcceptance(
            CurrentRevision.Number,
            acceptedTotal,
            currency,
            termsHash,
            policyVersion,
            now);
        Status = CheckoutStatus.ReadyForPayment;
        UpdatedAt = now;
        return DomainResult<CheckoutSession>.Success(this);
    }

    public DomainResult<PaymentAttempt> BeginPayment(
        string provider,
        string providerPaymentReference,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status != CheckoutStatus.ReadyForPayment || AcceptedRevision is null)
        {
            return DomainResult<PaymentAttempt>.Failure("checkout_not_ready_for_payment");
        }

        if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(providerPaymentReference))
        {
            return DomainResult<PaymentAttempt>.Failure("invalid_payment_provider_reference");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (now >= ExpiresAt)
        {
            return DomainResult<PaymentAttempt>.Failure("checkout_expired");
        }

        var attempt = new PaymentAttempt(
            Guid.CreateVersion7(now),
            provider,
            providerPaymentReference,
            CurrentRevision.Total,
            CurrentRevision.TransactionCurrency,
            now);
        paymentAttempts.Add(attempt);
        foreach (var component in components)
        {
            component.MarkPaymentPending(now);
        }

        Status = CheckoutStatus.PaymentPending;
        UpdatedAt = now;
        return DomainResult<PaymentAttempt>.Success(attempt);
    }

    public DomainResult<PaymentAttempt> RecordPayment(PaymentProviderResult result, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(timeProvider);
        var attempt = paymentAttempts.LastOrDefault();
        if (Status != CheckoutStatus.PaymentPending || attempt is null)
        {
            return DomainResult<PaymentAttempt>.Failure("payment_not_pending");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var errorCode = attempt.Record(result, now);
        if (errorCode is not null)
        {
            return DomainResult<PaymentAttempt>.Failure(errorCode);
        }

        Status = attempt.Status switch
        {
            PaymentStatus.Failed => CheckoutStatus.Failed,
            PaymentStatus.OutcomeUnknown => CheckoutStatus.RequiresSupport,
            _ => CheckoutStatus.PaymentPending,
        };
        UpdatedAt = now;
        return DomainResult<PaymentAttempt>.Success(attempt);
    }

    public DomainResult<CheckoutSession> BeginBooking(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var attempt = paymentAttempts.LastOrDefault();
        if (Status != CheckoutStatus.PaymentPending ||
            attempt is null ||
            attempt.Status is not (PaymentStatus.Authorised or PaymentStatus.Captured))
        {
            return DomainResult<CheckoutSession>.Failure("checkout_not_ready_for_booking");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        foreach (var component in components)
        {
            component.BeginBooking(now);
        }

        Status = CheckoutStatus.BookingPending;
        UpdatedAt = now;
        return DomainResult<CheckoutSession>.Success(this);
    }

    public DomainResult<ComponentBooking> RecordBookingResult(
        Guid componentBookingId,
        BookingProviderResult result,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status != CheckoutStatus.BookingPending)
        {
            return DomainResult<ComponentBooking>.Failure("checkout_not_booking_pending");
        }

        var component = components.SingleOrDefault(value => value.Id == componentBookingId);
        if (component is null)
        {
            return DomainResult<ComponentBooking>.Failure("component_booking_not_found");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var errorCode = component.Record(result, now);
        if (errorCode is not null)
        {
            return DomainResult<ComponentBooking>.Failure(errorCode);
        }

        UpdateBookingStatus(now);
        return DomainResult<ComponentBooking>.Success(component);
    }

    public DomainResult<BookingRecoveryCase> Recover(
        Guid? componentBookingId,
        string reason,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status is CheckoutStatus.Completed or CheckoutStatus.Expired)
        {
            return DomainResult<BookingRecoveryCase>.Failure("checkout_recovery_not_available");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return DomainResult<BookingRecoveryCase>.Failure("recovery_reason_required");
        }

        if (componentBookingId.HasValue && components.All(value => value.Id != componentBookingId.Value))
        {
            return DomainResult<BookingRecoveryCase>.Failure("component_booking_not_found");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        var existing = recoveryCases.SingleOrDefault(value =>
            value.ComponentBookingId == componentBookingId &&
            string.Equals(value.Reason, reason, StringComparison.Ordinal));
        if (existing is not null)
        {
            return DomainResult<BookingRecoveryCase>.Success(existing);
        }

        var recoveryCase = new BookingRecoveryCase(Guid.CreateVersion7(now), componentBookingId, reason, now);
        recoveryCases.Add(recoveryCase);
        Status = CheckoutStatus.RequiresSupport;
        UpdatedAt = now;
        return DomainResult<BookingRecoveryCase>.Success(recoveryCase);
    }

    public DomainResult<CheckoutSession> Expire(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (Status is not (CheckoutStatus.AwaitingAcceptance or CheckoutStatus.ReadyForPayment))
        {
            return DomainResult<CheckoutSession>.Failure("checkout_cannot_expire");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (now < ExpiresAt)
        {
            return DomainResult<CheckoutSession>.Failure("checkout_not_expired");
        }

        Status = CheckoutStatus.Expired;
        UpdatedAt = now;
        return DomainResult<CheckoutSession>.Success(this);
    }

    private static bool HasValidComposition(IReadOnlyCollection<ResolvedCheckoutOffer> offers) =>
        offers.Count is 1 or 2 &&
        offers.All(value => value.Product is CheckoutProduct.Hotel or CheckoutProduct.Flight) &&
        offers.Select(value => value.Product).Distinct().Count() == offers.Count;

    private bool HasMaterialOfferChange(CheckoutRevision candidate)
    {
        if (CurrentRevision.Total != candidate.Total ||
            !string.Equals(CurrentRevision.TransactionCurrency, candidate.TransactionCurrency, StringComparison.Ordinal) ||
            !string.Equals(CurrentRevision.TermsHash, candidate.TermsHash, StringComparison.Ordinal) ||
            CurrentRevision.Components.Count != candidate.Components.Count)
        {
            return true;
        }

        return CurrentRevision.Components
            .OrderBy(value => value.Product)
            .Zip(candidate.Components.OrderBy(value => value.Product))
            .Any(value => value.First.Product != value.Second.Product ||
                          !string.Equals(value.First.OfferId, value.Second.OfferId, StringComparison.Ordinal) ||
                          !string.Equals(value.First.ProviderBinding, value.Second.ProviderBinding, StringComparison.Ordinal) ||
                          !string.Equals(value.First.ProductDetail, value.Second.ProductDetail, StringComparison.Ordinal) ||
                          value.First.MinimumTotal != value.Second.MinimumTotal ||
                          !string.Equals(value.First.ProviderRevision, value.Second.ProviderRevision, StringComparison.Ordinal) ||
                          !string.Equals(value.First.TermsHash, value.Second.TermsHash, StringComparison.Ordinal) ||
                          value.First.ExpiresAt != value.Second.ExpiresAt);
    }

    private void UpdateBookingStatus(DateTimeOffset now)
    {
        var confirmed = components.Any(value => value.Status == ComponentBookingStatus.Confirmed);
        var failed = components.Any(value => value.Status == ComponentBookingStatus.Failed);
        var requiresSupport = components.Any(value => value.Status == ComponentBookingStatus.RequiresSupport);

        if (confirmed && failed)
        {
            foreach (var component in components)
            {
                component.RequireRefund(now);
            }

            Status = CheckoutStatus.RequiresSupport;
        }
        else if (confirmed && requiresSupport)
        {
            Status = CheckoutStatus.RequiresSupport;
        }
        else if (components.All(value => value.Status == ComponentBookingStatus.Confirmed))
        {
            Status = CheckoutStatus.Completed;
        }
        else if (requiresSupport)
        {
            Status = CheckoutStatus.RequiresSupport;
        }
        else if (components.All(value => value.Status == ComponentBookingStatus.Failed))
        {
            Status = CheckoutStatus.Failed;
        }
        else
        {
            Status = CheckoutStatus.BookingPending;
        }

        UpdatedAt = now;
    }
}
