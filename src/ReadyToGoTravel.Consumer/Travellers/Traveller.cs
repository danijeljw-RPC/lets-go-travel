namespace ReadyToGoTravel.Consumer.Travellers;

internal sealed class Traveller
{
    private Traveller()
    {
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string GivenName { get; private set; } = string.Empty;

    public string FamilyName { get; private set; } = string.Empty;

    public string? RelationshipLabel { get; private set; }

    public bool IsMinor { get; private set; }

    public DateTimeOffset? GuardianAuthorityConfirmedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DomainResult<Traveller> Create(
        Guid customerId,
        string givenName,
        string familyName,
        string? relationshipLabel,
        bool isMinor,
        bool guardianAuthorityConfirmed,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var normalizedGivenName = givenName?.Trim();
        var normalizedFamilyName = familyName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedGivenName) || normalizedGivenName.Length > 100 ||
            string.IsNullOrWhiteSpace(normalizedFamilyName) || normalizedFamilyName.Length > 100)
        {
            return DomainResult<Traveller>.Failure("invalid_traveller_name");
        }

        var normalizedRelationship = string.IsNullOrWhiteSpace(relationshipLabel)
            ? null
            : relationshipLabel.Trim();
        if (normalizedRelationship?.Length > 60)
        {
            return DomainResult<Traveller>.Failure("invalid_relationship_label");
        }

        if (isMinor && !guardianAuthorityConfirmed)
        {
            return DomainResult<Traveller>.Failure("guardian_authority_required");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        return DomainResult<Traveller>.Success(new Traveller
        {
            Id = Guid.CreateVersion7(now),
            CustomerId = customerId,
            GivenName = normalizedGivenName,
            FamilyName = normalizedFamilyName,
            RelationshipLabel = normalizedRelationship,
            IsMinor = isMinor,
            GuardianAuthorityConfirmedAt = isMinor ? now : null,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }
}
