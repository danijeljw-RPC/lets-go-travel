using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Persistence;
using ReadyToGoTravel.Retention;
using ReadyToGoTravel.Retention.Application;
using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Consumer.Retention;

internal sealed class ConsumerRetentionSweepProcessor(
    ConsumerDbContext database,
    IEnumerable<IConsumerRetentionEvidencePort> evidencePorts,
    ILegalHoldGuard legalHoldGuard,
    IRetentionReceiptRecorder receiptRecorder,
    TimeProvider timeProvider,
    IOptions<RetentionSweepOptions> options) : IConsumerRetentionSweepProcessor
{
    public async Task<bool> ProcessCycleAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled)
        {
            return false;
        }

        const RetentionRecordClass recordClass = RetentionRecordClass.CustomerAccountClosure;
        var policy = RetentionPolicyCatalog.Get(recordClass);
        var now = timeProvider.GetUtcNow();

        // Self-limiting via already-minimised state: once a candidate's preference fields are
        // reset to their defaults, this WHERE clause excludes it from future candidacy, matching
        // the same idiom the other sweeps use (RawBody != "" / PayloadJson != "") without needing
        // a separate "already processed" flag column.
        var candidates = (await database.Customers
                .Where(customer =>
                    customer.Status == CustomerStatus.Closed &&
                    customer.ClosedAtUtc != null &&
                    (customer.PreferredLocale != Locale.SupportedLocales.Default || customer.DisplayCurrency != "AUD"))
                .Select(customer => new { customer.Id, customer.Subject, customer.ClosedAtUtc })
                .ToListAsync(cancellationToken))
            .Where(customer => RetentionPolicyCatalog.IsExpired(recordClass, customer.ClosedAtUtc!.Value, now))
            .OrderBy(customer => customer.ClosedAtUtc)
            .Take(RetentionSweepConstants.BatchSize)
            .ToList();
        if (candidates.Count == 0)
        {
            return false;
        }

        var customerIds = candidates.Select(value => value.Id).ToList();
        var held = await legalHoldGuard.ExcludeHeldAsync(recordClass, RetentionSubjectKind.Customer, customerIds, cancellationToken);

        var successCount = 0;
        var failureCount = 0;
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (held.Contains(candidate.Id))
            {
                continue;
            }

            try
            {
                if (await legalHoldGuard.IsHeldAsync(recordClass, RetentionSubjectKind.Customer, candidate.Id, cancellationToken))
                {
                    continue;
                }

                var hasProtectedEvidence = false;
                foreach (var port in evidencePorts)
                {
                    if (await port.HasProtectedEvidenceAsync(candidate.Id, candidate.Subject, cancellationToken))
                    {
                        hasProtectedEvidence = true;
                        break;
                    }
                }

                if (hasProtectedEvidence)
                {
                    // Not eligible, not a failure: the stable Customer.Id/Subject linkage is
                    // preserved exactly as required to explain retained booking/support evidence.
                    continue;
                }

                await database.Customers.Where(customer => customer.Id == candidate.Id)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(customer => customer.PreferredLocale, Locale.SupportedLocales.Default)
                        .SetProperty(customer => customer.DisplayCurrency, "AUD")
                        .SetProperty(customer => customer.UpdatedAt, now), cancellationToken);
                successCount++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                failureCount++;
                await receiptRecorder.RecordOperationalFailureAsync(recordClass, "customer-account-closure", "minimise_failed", now, cancellationToken);
            }
        }

        await receiptRecorder.RecordAsync(recordClass, policy.PolicyVersion, "DeIdentify", successCount, failureCount, now, failureCount > 0 ? "minimisation failures occurred" : null, cancellationToken);
        return successCount > 0;
    }
}
