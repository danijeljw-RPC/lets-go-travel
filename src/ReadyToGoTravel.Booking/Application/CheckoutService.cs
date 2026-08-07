using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReadyToGoTravel.Booking.Bookings;
using ReadyToGoTravel.Booking.Checkout;
using ReadyToGoTravel.Booking.Http;
using ReadyToGoTravel.Booking.Idempotency;
using ReadyToGoTravel.Booking.Payments;
using ReadyToGoTravel.Booking.Persistence;
using ReadyToGoTravel.Booking.Providers;
using ReadyToGoTravel.Consumer.Application;
using ReadyToGoTravel.Search.Checkout;

namespace ReadyToGoTravel.Booking.Application;

internal sealed record CheckoutServiceResult(
    int StatusCode,
    CheckoutResponse? Value = null,
    string? ErrorCode = null,
    int? RetryAfterSeconds = null);

internal sealed record CheckoutStoredResponse(
    CheckoutResponse? Value,
    string? ErrorCode,
    int? RetryAfterSeconds = null);

internal sealed class CheckoutService(
    BookingDbContext database,
    IConsumerBookingContext consumerContext,
    IIdempotencyService idempotency,
    IServiceProvider services,
    BookingRuntime runtime,
    TimeProvider timeProvider)
{
    private const int PendingRetryAfterSeconds = 15;
    private static readonly TimeSpan ProviderOperationRetryDelay = TimeSpan.FromMinutes(1);

    public async Task<CheckoutServiceResult> CreateAsync(
        string subject,
        CreateCheckoutRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return Failure(StatusCodes.Status400BadRequest, validationError);
        }

        var assignments = request.TravellerAssignments.Select(value => value with { OfferId = value.OfferId ?? request.OfferIds.SingleOrDefault() }).ToArray();
        var travellerIds = assignments.Select(value => value.TravellerId).Distinct().ToArray();
        var consumer = await consumerContext.ResolveAsync(subject, request.TripId, travellerIds, cancellationToken);
        if (!consumer.IsSuccess || consumer.Value is null)
        {
            return ConsumerFailure(consumer.ErrorCode);
        }

        var inProgress = new CheckoutStoredResponse(CheckoutResponse.CreationPending(request.TripId), null);
        var response = await idempotency.ExecuteAsync(
            consumer.Value.CustomerId,
            "checkout-create",
            idempotencyKey,
            IdempotencyFingerprint.Create(request),
            IdempotentResponse.InProgress(StatusCodes.Status202Accepted, inProgress),
            async actionCancellationToken =>
            {
                var offerResult = await ResolveOffersAsync(request.OfferIds, actionCancellationToken);
                if (offerResult.ErrorCode is not null)
                {
                    return Completed(offerResult.StatusCode, null, offerResult.ErrorCode);
                }

                var byId = consumer.Value.Travellers.ToDictionary(value => value.TravellerId);
                var travellers = assignments.Select(assignment =>
                {
                    var value = byId[assignment.TravellerId];
                    return new TravellerSnapshot(assignment.OfferId!, value.TravellerId, value.GivenName, value.FamilyName,
                        value.IsMinor, value.GuardianAuthorityConfirmedAt, assignment.AgeAtTravel);
                }).ToArray();
                var creation = CheckoutSession.Create(
                    consumer.Value.CustomerId,
                    consumer.Value.TripId,
                    offerResult.Offers!,
                    travellers,
                    timeProvider);
                if (!creation.IsSuccess)
                {
                    return Completed(
                        StatusCodes.Status400BadRequest,
                        null,
                        creation.ErrorCode ?? "checkout_invalid");
                }

                database.Checkouts.Add(creation.Value!);
                await database.SaveChangesAsync(actionCancellationToken);
                return Completed(
                    StatusCodes.Status201Created,
                    CheckoutResponseMapper.Map(creation.Value!),
                    null);
            },
            cancellationToken);

        return FromIdempotent(response, createdOnFirstExecution: true);
    }

    public async Task<CheckoutServiceResult> GetAsync(
        string subject,
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        var checkout = await FindOwnedAsync(subject, checkoutId, cancellationToken);
        return checkout is null
            ? Failure(StatusCodes.Status404NotFound, "checkout_not_found")
            : Success(StatusCodes.Status200OK, CheckoutResponseMapper.Map(checkout));
    }

    public async Task<CheckoutServiceResult> AcceptAsync(
        string subject,
        Guid checkoutId,
        AcceptCheckoutRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var checkout = await FindOwnedAsync(subject, checkoutId, cancellationToken);
        if (checkout is null)
        {
            return Failure(StatusCodes.Status404NotFound, "checkout_not_found");
        }

        var current = CheckoutResponseMapper.Map(checkout);
        var response = await idempotency.ExecuteAsync(
            checkout.CustomerId,
            $"checkout-acceptance:{checkout.Id:N}",
            idempotencyKey,
            IdempotencyFingerprint.Create(request),
            IdempotentResponse.InProgress(
                StatusCodes.Status202Accepted,
                new CheckoutStoredResponse(current, null, PendingRetryAfterSeconds)),
            async actionCancellationToken =>
            {
                if (string.IsNullOrWhiteSpace(request.Currency) ||
                    string.IsNullOrWhiteSpace(request.TermsHash) ||
                    string.IsNullOrWhiteSpace(request.PolicyVersion))
                {
                    return Completed(
                        StatusCodes.Status400BadRequest,
                        CheckoutResponseMapper.Map(checkout),
                        "checkout_acceptance_invalid");
                }

                if (MatchesExistingAcceptance(checkout, request))
                {
                    return Completed(StatusCodes.Status200OK, CheckoutResponseMapper.Map(checkout), null);
                }

                var acceptance = checkout.AcceptRevision(
                    request.RevisionNumber,
                    request.AcceptedTotal,
                    request.Currency,
                    request.TermsHash,
                    request.PolicyVersion,
                    timeProvider);
                if (!acceptance.IsSuccess)
                {
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        acceptance.ErrorCode ?? "checkout_acceptance_conflict");
                }

                await database.SaveChangesAsync(actionCancellationToken);
                return Completed(StatusCodes.Status200OK, CheckoutResponseMapper.Map(checkout), null);
            },
            cancellationToken,
            retryInProgress: true);

        return FromIdempotent(response);
    }

    public async Task<CheckoutServiceResult> PreparePaymentAsync(
        string subject,
        Guid checkoutId,
        EmptyCheckoutCommandRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var checkout = await FindOwnedAsync(subject, checkoutId, cancellationToken);
        if (checkout is null)
        {
            return Failure(StatusCodes.Status404NotFound, "checkout_not_found");
        }

        var current = CheckoutResponseMapper.Map(checkout);
        var response = await idempotency.ExecuteAsync(
            checkout.CustomerId,
            $"checkout-payment-session:{checkout.Id:N}",
            idempotencyKey,
            IdempotencyFingerprint.Create(request),
            IdempotentResponse.InProgress(
                StatusCodes.Status202Accepted,
                new CheckoutStoredResponse(current, null, PendingRetryAfterSeconds)),
            async actionCancellationToken =>
            {
                if (checkout.AcceptedRevision is null)
                {
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        "checkout_not_ready_for_payment");
                }

                var providerOperationFingerprint = IdempotencyFingerprint.Create(new
                {
                    CheckoutId = checkout.Id,
                    RevisionNumber = checkout.CurrentRevision.Number,
                    checkout.CurrentRevision.TermsHash,
                });
                return await idempotency.ExecuteAsync(
                    checkout.CustomerId,
                    $"provider-payment-session:{checkout.Id:N}:{checkout.CurrentRevision.Number}",
                    "single-operation",
                    providerOperationFingerprint,
                    IdempotentResponse.InProgress(
                        StatusCodes.Status202Accepted,
                        new CheckoutStoredResponse(
                            CheckoutResponseMapper.Map(checkout, retryAfterSeconds: PendingRetryAfterSeconds),
                            null,
                            PendingRetryAfterSeconds)),
                    async providerCancellationToken =>
                    {
                        if (checkout.Status != CheckoutStatus.ReadyForPayment)
                        {
                            return Completed(
                                StatusCodes.Status409Conflict,
                                CheckoutResponseMapper.Map(checkout),
                                "checkout_not_ready_for_payment");
                        }

                        var offerIds = checkout.CurrentRevision.Components.Select(value => value.OfferId).ToArray();
                        var offerResult = await ResolveOffersAsync(offerIds, providerCancellationToken);
                        if (offerResult.ErrorCode is not null)
                        {
                            return Completed(offerResult.StatusCode, null, offerResult.ErrorCode);
                        }

                        var acceptedRevisionNumber = checkout.AcceptedRevision.RevisionNumber;
                        var reprice = checkout.ApplyResolvedOffers(offerResult.Offers!, timeProvider);
                        if (!reprice.IsSuccess)
                        {
                            return Completed(
                                StatusCodes.Status409Conflict,
                                CheckoutResponseMapper.Map(checkout),
                                reprice.ErrorCode ?? "checkout_reprice_conflict");
                        }

                        if (checkout.AcceptedRevision is null ||
                            checkout.CurrentRevision.Number != acceptedRevisionNumber)
                        {
                            await database.SaveChangesAsync(providerCancellationToken);
                            return Completed(
                                StatusCodes.Status409Conflict,
                                CheckoutResponseMapper.Map(checkout),
                                "price_acceptance_required");
                        }

                        var paymentService = services.GetService<IPaymentService>();
                        if (paymentService is null)
                        {
                            return Completed(
                                StatusCodes.Status503ServiceUnavailable,
                                CheckoutResponseMapper.Map(checkout),
                                "booking_capability_unavailable");
                        }

                        var preparation = await paymentService.PrepareAsync(
                            new CustomerPaymentPlan(
                                checkout.CurrentRevision.Total,
                                checkout.CurrentRevision.TransactionCurrency,
                                checkout.Id.ToString("N")),
                            $"checkout-payment:{checkout.Id:N}:{checkout.CurrentRevision.Number}",
                            providerCancellationToken);
                        checkout.SetPaymentPlan(preparation.Plan);
                        var payment = checkout.BeginPayment(
                            "hosted-payment",
                            preparation.HostedSession.PaymentReference,
                            timeProvider);
                        if (!payment.IsSuccess)
                        {
                            return Completed(
                                StatusCodes.Status409Conflict,
                                CheckoutResponseMapper.Map(checkout),
                                payment.ErrorCode ?? "payment_session_conflict");
                        }

                        await database.SaveChangesAsync(providerCancellationToken);
                        var session = new HostedPaymentSessionResponse(
                            preparation.HostedSession.PaymentReference,
                            preparation.HostedSession.BrowserToken,
                            preparation.HostedSession.BrowserTokenExpiresAt);
                        return Completed(
                            StatusCodes.Status200OK,
                            CheckoutResponseMapper.Map(checkout, session),
                            null);
                    },
                    actionCancellationToken,
                    retryInProgress: true,
                    retryInProgressAfter: ProviderOperationRetryDelay);
            },
            cancellationToken,
            retryInProgress: true);

        return FromIdempotent(response);
    }

    public async Task<CheckoutServiceResult> ReturnPaymentAsync(
        string subject,
        Guid checkoutId,
        PaymentReturnRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var checkout = await FindOwnedAsync(subject, checkoutId, cancellationToken);
        if (checkout is null)
        {
            return Failure(StatusCodes.Status404NotFound, "checkout_not_found");
        }

        if (string.IsNullOrWhiteSpace(request.CompletionReference) || request.CompletionReference.Length > 512)
        {
            return Failure(StatusCodes.Status400BadRequest, "payment_completion_reference_invalid");
        }

        var response = await idempotency.ExecuteAsync(
            checkout.CustomerId,
            $"checkout-payment-return:{checkout.Id:N}",
            idempotencyKey,
            IdempotencyFingerprint.Create(request),
            IdempotentResponse.InProgress(
                StatusCodes.Status202Accepted,
                new CheckoutStoredResponse(
                    CheckoutResponseMapper.Map(checkout, retryAfterSeconds: PendingRetryAfterSeconds),
                    null,
                    PendingRetryAfterSeconds)),
            async actionCancellationToken =>
            {
                var attempt = LastPayment(checkout);
                if (attempt is null)
                {
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        "payment_not_pending");
                }

                if (attempt.Status is PaymentStatus.Authorised or PaymentStatus.Captured or PaymentStatus.RefundRequired)
                {
                    return Completed(StatusCodes.Status200OK, CheckoutResponseMapper.Map(checkout), null);
                }

                if (checkout.Status != CheckoutStatus.PaymentPending)
                {
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        "payment_not_pending");
                }

                var paymentService = services.GetService<IPaymentService>();
                if (paymentService is null)
                {
                    return Completed(
                        StatusCodes.Status503ServiceUnavailable,
                        CheckoutResponseMapper.Map(checkout),
                        "booking_capability_unavailable");
                }

                var providerResult = await paymentService.VerifyReturnAsync(
                    attempt.ProviderPaymentReference,
                    request.CompletionReference,
                    actionCancellationToken);
                if (!string.Equals(
                        providerResult.PaymentReference,
                        attempt.ProviderPaymentReference,
                        StringComparison.Ordinal))
                {
                    checkout.Recover(null, "payment_reference_mismatch", timeProvider);
                    await database.SaveChangesAsync(actionCancellationToken);
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        "payment_reference_mismatch");
                }

                if (providerResult.Status == PaymentProviderStatus.ActionRequired)
                {
                    return Completed(
                        StatusCodes.Status202Accepted,
                        CheckoutResponseMapper.Map(checkout, retryAfterSeconds: PendingRetryAfterSeconds),
                        null,
                        PendingRetryAfterSeconds);
                }

                var recorded = checkout.RecordPayment(
                    CheckoutOfferMapper.ToDomainResult(providerResult),
                    timeProvider);
                if (!recorded.IsSuccess)
                {
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        recorded.ErrorCode ?? "payment_return_conflict");
                }

                if (providerResult.Status == PaymentProviderStatus.OutcomeUnknown)
                {
                    checkout.Recover(null, "payment_outcome_unknown", timeProvider);
                }

                try
                {
                    await database.SaveChangesAsync(actionCancellationToken);
                }
                catch (DbUpdateConcurrencyException)
                {
                    DetachCheckoutGraph();
                    var winner = await LoadCheckoutSnapshotAsync(checkout.Id, actionCancellationToken);
                    var winnerPayment = LastPayment(winner);
                    var winnerPending = winnerPayment?.Status is PaymentStatus.ActionRequired or PaymentStatus.Processing;
                    return Completed(
                        winnerPending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK,
                        CheckoutResponseMapper.Map(
                            winner,
                            retryAfterSeconds: winnerPending ? PendingRetryAfterSeconds : null),
                        null,
                        winnerPending ? PendingRetryAfterSeconds : null);
                }

                var pending = providerResult.Status == PaymentProviderStatus.Processing;
                return Completed(
                    pending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK,
                    CheckoutResponseMapper.Map(
                        checkout,
                        retryAfterSeconds: pending ? PendingRetryAfterSeconds : null),
                    null,
                    pending ? PendingRetryAfterSeconds : null);
            },
            cancellationToken);

        return FromIdempotent(response);
    }

    public async Task<CheckoutServiceResult> BookAsync(
        string subject,
        Guid checkoutId,
        EmptyCheckoutCommandRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var checkout = await FindOwnedAsync(subject, checkoutId, cancellationToken);
        if (checkout is null)
        {
            return Failure(StatusCodes.Status404NotFound, "checkout_not_found");
        }

        var response = await idempotency.ExecuteAsync(
            checkout.CustomerId,
            $"checkout-book:{checkout.Id:N}",
            idempotencyKey,
            IdempotencyFingerprint.Create(request),
            IdempotentResponse.InProgress(
                StatusCodes.Status202Accepted,
                new CheckoutStoredResponse(
                    CheckoutResponseMapper.Map(checkout, retryAfterSeconds: PendingRetryAfterSeconds),
                    null,
                    PendingRetryAfterSeconds)),
            async actionCancellationToken =>
            {
                var paymentAttempt = LastPayment(checkout);
                if (paymentAttempt is null)
                {
                    return Completed(
                        StatusCodes.Status409Conflict,
                        CheckoutResponseMapper.Map(checkout),
                        "checkout_not_ready_for_booking");
                }

                var providerOperationFingerprint = IdempotencyFingerprint.Create(new
                {
                    CheckoutId = checkout.Id,
                    PaymentAttemptId = paymentAttempt.Id,
                });
                return await idempotency.ExecuteAsync(
                    checkout.CustomerId,
                    $"provider-book:{checkout.Id:N}:{paymentAttempt.Id:N}",
                    "single-operation",
                    providerOperationFingerprint,
                    IdempotentResponse.InProgress(
                        StatusCodes.Status202Accepted,
                        new CheckoutStoredResponse(
                            CheckoutResponseMapper.Map(checkout, retryAfterSeconds: PendingRetryAfterSeconds),
                            null,
                            PendingRetryAfterSeconds)),
                    async providerCancellationToken =>
                    {
                        var provider = services.GetService<IBookingProvider>();
                        var paymentService = services.GetService<IPaymentService>();
                        if (provider is null || paymentService is null || checkout.PaymentPlan is null)
                        {
                            return Completed(
                                StatusCodes.Status503ServiceUnavailable,
                                CheckoutResponseMapper.Map(checkout),
                                "booking_capability_unavailable");
                        }

                        try
                        {
                            if (checkout.Status == CheckoutStatus.PaymentPending)
                            {
                                var begin = checkout.BeginBooking(timeProvider);
                                if (!begin.IsSuccess)
                                {
                                    return Completed(
                                        StatusCodes.Status409Conflict,
                                        CheckoutResponseMapper.Map(checkout),
                                        begin.ErrorCode ?? "checkout_not_ready_for_booking");
                                }

                                await database.SaveChangesAsync(providerCancellationToken);
                            }
                            else if (checkout.Status != CheckoutStatus.BookingPending)
                            {
                                return Completed(
                                    StatusCodes.Status409Conflict,
                                    CheckoutResponseMapper.Map(checkout),
                                    "checkout_not_ready_for_booking");
                            }

                            foreach (var component in checkout.Components.OrderBy(value => value.Product))
                            {
                                if (checkout.Status != CheckoutStatus.BookingPending)
                                {
                                    break;
                                }

                                if (component.Status != ComponentBookingStatus.BookingPending)
                                {
                                    continue;
                                }

                                var revisionComponent = checkout.CurrentRevision.Components.Single(value => value.OfferId == component.OfferId);
                                var settlement = await paymentService.CreateSettlementAsync(checkout.PaymentPlan,
                                    component.ProviderBinding, revisionComponent.MinimumTotal,
                                    checkout.CurrentRevision.TransactionCurrency, paymentAttempt.ProviderPaymentReference,
                                    providerCancellationToken);
                                var execution = await provider.BookAsync(
                                    new BookingCommand(
                                        component.Product,
                                        component.OfferId,
                                        component.ProviderBinding,
                                        $"checkout-book:{checkout.Id:N}:{component.Id:N}",
                                        checkout.TravellerSnapshots.Where(value => value.OfferId == component.OfferId)
                                            .Select(value => new BookingTravellerContext(value.TravellerId, value.GivenName,
                                                value.FamilyName, value.IsMinor, value.AgeAtTravel)).ToArray(),
                                        settlement.InstructionReference),
                                    providerCancellationToken);
                                if (execution.Status == BookingProviderStatus.Confirmed &&
                                    string.IsNullOrWhiteSpace(execution.ExternalReference))
                                {
                                    checkout.Recover(
                                        component.Id,
                                        "booking_confirmation_reference_required",
                                        timeProvider);
                                    await database.SaveChangesAsync(providerCancellationToken);
                                    break;
                                }

                                var recorded = checkout.RecordBookingResult(
                                    component.Id,
                                    CheckoutOfferMapper.ToDomainResult(execution),
                                    timeProvider);
                                if (!recorded.IsSuccess)
                                {
                                    checkout.Recover(
                                        component.Id,
                                        recorded.ErrorCode ?? "booking_result_conflict",
                                        timeProvider);
                                    await database.SaveChangesAsync(providerCancellationToken);
                                    break;
                                }

                                if (execution.Status == BookingProviderStatus.Unknown)
                                {
                                    checkout.Recover(component.Id, "booking_outcome_unknown", timeProvider);
                                }

                                await database.SaveChangesAsync(providerCancellationToken);
                            }

                            var pending = checkout.Status == CheckoutStatus.BookingPending;
                            return Completed(
                                pending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK,
                                CheckoutResponseMapper.Map(
                                    checkout,
                                    retryAfterSeconds: pending ? PendingRetryAfterSeconds : null),
                                null,
                                pending ? PendingRetryAfterSeconds : null);
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            DetachCheckoutGraph();
                            var winner = await LoadCheckoutSnapshotAsync(checkout.Id, providerCancellationToken);
                            var winnerPending = winner.Status is CheckoutStatus.PaymentPending or CheckoutStatus.BookingPending;
                            return Completed(
                                winnerPending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK,
                                CheckoutResponseMapper.Map(
                                    winner,
                                    retryAfterSeconds: winnerPending ? PendingRetryAfterSeconds : null),
                                null,
                                winnerPending ? PendingRetryAfterSeconds : null);
                        }
                    },
                    actionCancellationToken,
                    retryInProgress: true,
                    retryInProgressAfter: ProviderOperationRetryDelay);
            },
            cancellationToken,
            retryInProgress: true);

        return FromIdempotent(response);
    }

    public async Task<CheckoutServiceResult> RecoverAsync(
        string subject,
        Guid checkoutId,
        EmptyCheckoutCommandRequest request,
        CancellationToken cancellationToken)
    {
        var checkout = await FindOwnedAsync(subject, checkoutId, cancellationToken);
        if (checkout is null)
        {
            return Failure(StatusCodes.Status404NotFound, "checkout_not_found");
        }

        if (checkout.Status == CheckoutStatus.BookingPending)
        {
            var provider = services.GetService<IBookingProvider>();
            if (provider is null)
            {
                return Failure(StatusCodes.Status503ServiceUnavailable, "booking_capability_unavailable");
            }

            foreach (var component in checkout.Components
                         .Where(value => value.Status == ComponentBookingStatus.BookingPending)
                         .OrderBy(value => value.Product))
            {
                if (string.IsNullOrWhiteSpace(component.ProviderBookingReference))
                {
                    checkout.Recover(component.Id, "booking_reference_unavailable", timeProvider);
                    break;
                }

                var execution = await provider.RetrieveAsync(
                    component.ProviderBookingReference,
                    cancellationToken);
                if (execution.ExternalReference is not null &&
                    !string.Equals(
                        execution.ExternalReference,
                        component.ProviderBookingReference,
                        StringComparison.Ordinal))
                {
                    checkout.Recover(component.Id, "booking_reference_mismatch", timeProvider);
                    break;
                }

                if (execution.Status == BookingProviderStatus.Confirmed &&
                    string.IsNullOrWhiteSpace(execution.ExternalReference))
                {
                    checkout.Recover(component.Id, "booking_confirmation_reference_required", timeProvider);
                    break;
                }

                var recorded = checkout.RecordBookingResult(
                    component.Id,
                    CheckoutOfferMapper.ToDomainResult(execution),
                    timeProvider);
                if (!recorded.IsSuccess)
                {
                    checkout.Recover(component.Id, recorded.ErrorCode ?? "booking_recovery_conflict", timeProvider);
                    break;
                }

                if (execution.Status == BookingProviderStatus.Unknown)
                {
                    checkout.Recover(component.Id, "booking_outcome_unknown", timeProvider);
                    break;
                }

                await database.SaveChangesAsync(cancellationToken);
            }
        }
        else if (checkout.Status == CheckoutStatus.PaymentPending)
        {
            var payment = LastPayment(checkout);
            var paymentService = services.GetService<IPaymentService>();
            if (payment is null || paymentService is null)
            {
                return Failure(StatusCodes.Status503ServiceUnavailable, "booking_capability_unavailable");
            }

            var providerResult = await paymentService.RetrieveAsync(
                payment.ProviderPaymentReference,
                cancellationToken);
            if (!string.Equals(
                    providerResult.PaymentReference,
                    payment.ProviderPaymentReference,
                    StringComparison.Ordinal))
            {
                checkout.Recover(null, "payment_reference_mismatch", timeProvider);
            }
            else if (providerResult.Status != PaymentProviderStatus.ActionRequired)
            {
                var recorded = checkout.RecordPayment(CheckoutOfferMapper.ToDomainResult(providerResult), timeProvider);
                if (!recorded.IsSuccess)
                {
                    checkout.Recover(null, recorded.ErrorCode ?? "payment_recovery_conflict", timeProvider);
                }
                else if (providerResult.Status == PaymentProviderStatus.OutcomeUnknown)
                {
                    checkout.Recover(null, "payment_outcome_unknown", timeProvider);
                }
            }
        }
        else if (checkout.Status == CheckoutStatus.RequiresSupport)
        {
            foreach (var component in checkout.Components.Where(value =>
                         value.Status == ComponentBookingStatus.RequiresSupport &&
                         checkout.RecoveryCases.All(recovery => recovery.ComponentBookingId != value.Id)))
            {
                checkout.Recover(component.Id, "booking_outcome_unknown", timeProvider);
            }

            if (LastPayment(checkout)?.Status == PaymentStatus.OutcomeUnknown)
            {
                checkout.Recover(null, "payment_outcome_unknown", timeProvider);
            }
        }
        else if (checkout.Status != CheckoutStatus.Completed)
        {
            return Failure(StatusCodes.Status409Conflict, "checkout_recovery_not_available");
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            database.ChangeTracker.Clear();
            checkout = await LoadCheckoutSnapshotAsync(checkout.Id, cancellationToken);
        }
        catch (DbUpdateException exception) when (IsRecoveryCaseRace(exception))
        {
            database.ChangeTracker.Clear();
            checkout = await LoadCheckoutSnapshotAsync(checkout.Id, cancellationToken);
        }

        var pending = checkout.Status is CheckoutStatus.BookingPending or CheckoutStatus.PaymentPending;
        return Success(
            pending ? StatusCodes.Status202Accepted : StatusCodes.Status200OK,
            CheckoutResponseMapper.Map(
                checkout,
                retryAfterSeconds: pending ? PendingRetryAfterSeconds : null),
            pending ? PendingRetryAfterSeconds : null);
    }

    private async Task<CheckoutSession?> FindOwnedAsync(
        string subject,
        Guid checkoutId,
        CancellationToken cancellationToken)
    {
        var checkout = await CheckoutGraph()
            .SingleOrDefaultAsync(value => value.Id == checkoutId, cancellationToken);
        if (checkout is null)
        {
            return null;
        }

        var customerId = await consumerContext.ResolveCustomerIdAsync(subject, cancellationToken);
        return customerId == checkout.CustomerId ? checkout : null;
    }

    private IQueryable<CheckoutSession> CheckoutGraph() => database.Checkouts
        .Include(value => value.Revisions)
            .ThenInclude(value => value.Components)
        .Include(value => value.TravellerSnapshots)
        .Include(value => value.Components)
        .Include(value => value.PaymentAttempts)
        .Include(value => value.RecoveryCases);

    private Task<CheckoutSession> LoadCheckoutSnapshotAsync(Guid checkoutId, CancellationToken cancellationToken) =>
        CheckoutGraph()
            .AsNoTracking()
            .SingleAsync(value => value.Id == checkoutId, cancellationToken);

    private void DetachCheckoutGraph()
    {
        foreach (var entry in database.ChangeTracker.Entries()
                     .Where(entry => entry.Entity is not IdempotencyRecord)
                     .ToArray())
        {
            entry.State = EntityState.Detached;
        }
    }

    private static bool IsRecoveryCaseRace(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current)?.ToString();
            var constraintName = current.GetType().GetProperty("ConstraintName")?.GetValue(current)?.ToString();
            if (string.Equals(sqlState, "23505", StringComparison.Ordinal) &&
                string.Equals(
                    constraintName,
                    "ux_booking_recovery_cases_checkout_dedupe_reason",
                    StringComparison.Ordinal))
            {
                return true;
            }

            var sqliteErrorCode = current.GetType().GetProperty("SqliteErrorCode")?.GetValue(current);
            if (sqliteErrorCode is int sqliteCode && sqliteCode == 19 &&
                current.Message.Contains("booking_recovery_cases.checkout_session_id", StringComparison.Ordinal) &&
                current.Message.Contains("booking_recovery_cases.dedupe_key", StringComparison.Ordinal) &&
                current.Message.Contains("booking_recovery_cases.reason", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<ResolvedOffersResult> ResolveOffersAsync(
        IReadOnlyCollection<string> offerIds,
        CancellationToken cancellationToken)
    {
        var resolver = services.GetService<ICheckoutOfferResolver>();
        if (resolver is null)
        {
            return new ResolvedOffersResult(
                null,
                StatusCodes.Status503ServiceUnavailable,
                "booking_capability_unavailable");
        }

        var offers = new List<ResolvedCheckoutOffer>(offerIds.Count);
        foreach (var offerId in offerIds)
        {
            var resolution = await resolver.ResolveAsync(offerId, runtime.Environment, cancellationToken);
            if (!resolution.IsSuccess)
            {
                var statusCode = resolution.ErrorCode == "booking_capability_unavailable"
                    ? StatusCodes.Status503ServiceUnavailable
                    : StatusCodes.Status400BadRequest;
                return new ResolvedOffersResult(null, statusCode, resolution.ErrorCode ?? "checkout_offer_invalid");
            }

            offers.Add(CheckoutOfferMapper.ToResolvedCheckoutOffer(resolution));
        }

        return new ResolvedOffersResult(offers, StatusCodes.Status200OK, null);
    }

    private static string? ValidateCreateRequest(CreateCheckoutRequest request)
    {
        if (request.TripId == Guid.Empty)
        {
            return "trip_id_required";
        }

        if (request.OfferIds is null ||
            request.OfferIds.Count is < 1 or > 2 ||
            request.OfferIds.Any(string.IsNullOrWhiteSpace) ||
            request.OfferIds.Distinct(StringComparer.Ordinal).Count() != request.OfferIds.Count)
        {
            return "invalid_checkout_composition";
        }

        if (request.TravellerAssignments is null ||
            request.TravellerAssignments.Count == 0 ||
            request.TravellerAssignments.Any(value =>
                value.TravellerId == Guid.Empty || value.AgeAtTravel is < 0 or > 120) ||
            request.TravellerAssignments.Any(value => request.OfferIds.Count > 1 && string.IsNullOrWhiteSpace(value.OfferId) ||
                value.OfferId is not null && !request.OfferIds.Contains(value.OfferId, StringComparer.Ordinal)) ||
            request.TravellerAssignments.Select(value => new { value.OfferId, value.TravellerId }).Distinct().Count() != request.TravellerAssignments.Count ||
            request.OfferIds.Any(offerId => request.TravellerAssignments.All(value => value.OfferId != offerId)))
        {
            return "invalid_checkout_travellers";
        }

        return null;
    }

    private static bool MatchesExistingAcceptance(CheckoutSession checkout, AcceptCheckoutRequest request) =>
        checkout.AcceptedRevision is not null &&
        checkout.AcceptedRevision.RevisionNumber == request.RevisionNumber &&
        checkout.AcceptedRevision.AcceptedTotal == request.AcceptedTotal &&
        string.Equals(checkout.AcceptedRevision.Currency, request.Currency, StringComparison.Ordinal) &&
        string.Equals(checkout.AcceptedRevision.TermsHash, request.TermsHash, StringComparison.Ordinal) &&
        string.Equals(checkout.AcceptedRevision.PolicyVersion, request.PolicyVersion, StringComparison.Ordinal);

    private static PaymentAttempt? LastPayment(CheckoutSession checkout) =>
        checkout.PaymentAttempts.Count == 0 ? null : checkout.PaymentAttempts[^1];

    private static CheckoutServiceResult ConsumerFailure(string? errorCode) => errorCode switch
    {
        "profile_required" => Failure(StatusCodes.Status409Conflict, "profile_required"),
        "trip_not_found" => Failure(StatusCodes.Status404NotFound, "trip_not_found"),
        "traveller_not_found" => Failure(StatusCodes.Status404NotFound, "traveller_not_found"),
        _ => Failure(StatusCodes.Status400BadRequest, errorCode ?? "checkout_context_invalid"),
    };

    private static IdempotentResponse<CheckoutStoredResponse> Completed(
        int statusCode,
        CheckoutResponse? value,
        string? errorCode,
        int? retryAfterSeconds = null) => IdempotentResponse.Completed(
            statusCode,
            new CheckoutStoredResponse(value, errorCode, retryAfterSeconds));

    private static CheckoutServiceResult FromIdempotent(
        IdempotentResponse<CheckoutStoredResponse> response,
        bool createdOnFirstExecution = false)
    {
        if (response.Outcome == IdempotencyOutcome.Conflict)
        {
            return Failure(StatusCodes.Status409Conflict, "idempotency_conflict");
        }

        var statusCode = createdOnFirstExecution && response.IsReplay &&
                         response.StatusCode == StatusCodes.Status201Created
            ? StatusCodes.Status200OK
            : response.StatusCode;
        return response.Value is null
            ? Failure(StatusCodes.Status409Conflict, "idempotency_response_unavailable")
            : new CheckoutServiceResult(
                statusCode,
                response.Value.Value,
                response.Value.ErrorCode,
                response.Value.RetryAfterSeconds);
    }

    private static CheckoutServiceResult Success(
        int statusCode,
        CheckoutResponse value,
        int? retryAfterSeconds = null) => new(statusCode, value, null, retryAfterSeconds);

    private static CheckoutServiceResult Failure(int statusCode, string errorCode) =>
        new(statusCode, null, errorCode);

    private sealed record ResolvedOffersResult(
        IReadOnlyCollection<ResolvedCheckoutOffer>? Offers,
        int StatusCode,
        string? ErrorCode);
}
