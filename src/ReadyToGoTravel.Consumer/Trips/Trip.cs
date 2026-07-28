namespace ReadyToGoTravel.Consumer.Trips;

internal sealed class Trip
{
    private Trip()
    {
    }

    public Guid Id { get; private set; }

    public Guid CustomerId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? PrimaryDestination { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? EndDate { get; private set; }

    public TripStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public static DomainResult<Trip> Create(
        Guid customerId,
        string title,
        string? primaryDestination,
        DateOnly? startDate,
        DateOnly? endDate,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        var normalizedTitle = title?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedTitle) || normalizedTitle.Length > 120)
        {
            return DomainResult<Trip>.Failure("invalid_trip_title");
        }

        var normalizedDestination = string.IsNullOrWhiteSpace(primaryDestination)
            ? null
            : primaryDestination.Trim();
        if (normalizedDestination?.Length > 160)
        {
            return DomainResult<Trip>.Failure("invalid_trip_destination");
        }

        if (startDate.HasValue && endDate.HasValue && endDate < startDate)
        {
            return DomainResult<Trip>.Failure("invalid_trip_dates");
        }

        var now = timeProvider.GetUtcNow().ToUniversalTime();
        return DomainResult<Trip>.Success(new Trip
        {
            Id = Guid.CreateVersion7(now),
            CustomerId = customerId,
            Title = normalizedTitle,
            PrimaryDestination = normalizedDestination,
            StartDate = startDate,
            EndDate = endDate,
            Status = TripStatus.Planning,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    public void Archive(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        Status = TripStatus.Archived;
        UpdatedAt = timeProvider.GetUtcNow().ToUniversalTime();
    }
}

internal enum TripStatus
{
    Planning,
    Active,
    Archived,
}
