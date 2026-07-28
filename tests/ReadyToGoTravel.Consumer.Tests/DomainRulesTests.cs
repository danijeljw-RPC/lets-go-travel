using ReadyToGoTravel.Consumer.Customers;
using ReadyToGoTravel.Consumer.Locale;
using ReadyToGoTravel.Consumer.Travellers;
using ReadyToGoTravel.Consumer.Trips;

namespace ReadyToGoTravel.Consumer.Tests;

public sealed class DomainRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FixedTimeProvider(Now);

    [Fact]
    public void CustomerRequiresAdultPurchaserConfirmation()
    {
        var result = Customer.Create("keycloak-subject", "en-AU", adultConfirmed: false, Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("adult_confirmation_required", result.ErrorCode);
    }

    [Fact]
    public void CustomerRejectsUnsupportedLocale()
    {
        var result = Customer.Create("keycloak-subject", "fr-FR", adultConfirmed: true, Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("unsupported_locale", result.ErrorCode);
        Assert.Equal("en-AU", SupportedLocales.Default);
    }

    [Fact]
    public void TripRejectsEndDateBeforeStartDate()
    {
        var result = Trip.Create(
            Guid.CreateVersion7(),
            "Japan",
            "Tokyo",
            new DateOnly(2026, 10, 10),
            new DateOnly(2026, 10, 9),
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_trip_dates", result.ErrorCode);
    }

    [Fact]
    public void MinorTravellerRequiresGuardianAuthority()
    {
        var result = Traveller.Create(
            Guid.CreateVersion7(),
            "Sam",
            "Taylor",
            "Child",
            isMinor: true,
            guardianAuthorityConfirmed: false,
            Clock);

        Assert.False(result.IsSuccess);
        Assert.Equal("guardian_authority_required", result.ErrorCode);
    }

    [Fact]
    public void TravellerNormalizesLowRiskProfileAndContainsNoSensitiveFields()
    {
        var result = Traveller.Create(
            Guid.CreateVersion7(),
            "  Sam  ",
            " Taylor ",
            " Family ",
            isMinor: false,
            guardianAuthorityConfirmed: false,
            Clock);

        Assert.True(result.IsSuccess);
        Assert.Equal("Sam", result.Value!.GivenName);
        Assert.Equal("Taylor", result.Value.FamilyName);
        Assert.Equal("Family", result.Value.RelationshipLabel);
        Assert.Equal(Now, result.Value.CreatedAt);

        var propertyNames = typeof(Traveller).GetProperties()
            .Select(property => property.Name)
            .ToArray();
        Assert.DoesNotContain(propertyNames, name => name.Contains("Birth", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Passport", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(propertyNames, name => name.Contains("Document", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
