namespace ReadyToGoTravel.Consumer.Retention;

/// <summary>
/// A port Consumer defines and other modules adapt to (dependency inversion), so the Consumer
/// account-closure minimisation sweep can ask "does this customer have protected evidence
/// elsewhere" without ever taking a compile-time reference to Booking or Support. Register
/// multiple implementations with the same DI service type (do not TryAdd/replace) - the sweep
/// resolves IEnumerable&lt;IConsumerRetentionEvidencePort&gt; and treats it as true if any
/// contributor reports protected evidence.
/// </summary>
public interface IConsumerRetentionEvidencePort
{
    /// <param name="customerId">The Consumer module's internal customer identifier.</param>
    /// <param name="subject">
    /// The customer's Keycloak subject - Support's tickets key off this string, not the internal
    /// Guid, so both are passed rather than requiring every adapter to resolve one from the other
    /// across a module boundary.
    /// </param>
    Task<bool> HasProtectedEvidenceAsync(Guid customerId, string subject, CancellationToken cancellationToken = default);
}
