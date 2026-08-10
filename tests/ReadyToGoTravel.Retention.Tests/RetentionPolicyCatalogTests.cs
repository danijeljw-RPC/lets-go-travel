using ReadyToGoTravel.Retention.Domain;

namespace ReadyToGoTravel.Retention.Tests;

public sealed class RetentionPolicyCatalogTests
{
    private static readonly DateTimeOffset Trigger = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(RetentionRecordClass.BookingRelatedSupportTicket, 7, RetentionPeriodUnit.Years)]
    [InlineData(RetentionRecordClass.GeneralSupportTicket, 2, RetentionPeriodUnit.Years)]
    [InlineData(RetentionRecordClass.SupportAttachment, 90, RetentionPeriodUnit.Days)]
    [InlineData(RetentionRecordClass.SecurityAuditRecord, 2, RetentionPeriodUnit.Years)]
    [InlineData(RetentionRecordClass.WebhookPayloadBody, 90, RetentionPeriodUnit.Days)]
    [InlineData(RetentionRecordClass.NotificationRenderedContent, 90, RetentionPeriodUnit.Days)]
    [InlineData(RetentionRecordClass.AbandonedCheckoutState, 30, RetentionPeriodUnit.Days)]
    [InlineData(RetentionRecordClass.CanonicalBookingEvidence, 7, RetentionPeriodUnit.Years)]
    [InlineData(RetentionRecordClass.SuccessfulSupplierPayload, 90, RetentionPeriodUnit.Days)]
    [InlineData(RetentionRecordClass.ExceptionalSupplierPayload, 1, RetentionPeriodUnit.Years)]
    [InlineData(RetentionRecordClass.TravellerSensitiveFieldMinimisation, 90, RetentionPeriodUnit.Days)]
    [InlineData(RetentionRecordClass.CustomerAccountClosure, 90, RetentionPeriodUnit.Days)]
    public void EveryRecordClassMatchesTheApprovedSchedule(RetentionRecordClass recordClass, int periodValue, RetentionPeriodUnit unit)
    {
        var definition = RetentionPolicyCatalog.Get(recordClass);

        Assert.Equal(periodValue, definition.PeriodValue);
        Assert.Equal(unit, definition.PeriodUnit);
        Assert.Equal(1, definition.PolicyVersion);
        Assert.False(string.IsNullOrWhiteSpace(definition.TriggerDescription));
    }

    [Fact]
    public void EveryEnumMemberHasADefinedPolicy()
    {
        foreach (var recordClass in Enum.GetValues<RetentionRecordClass>())
        {
            var definition = RetentionPolicyCatalog.Get(recordClass);
            Assert.Equal(recordClass, definition.RecordClass);
        }
    }

    [Fact]
    public void CalculateExpiryIsPureAndDeterministic()
    {
        var first = RetentionPolicyCatalog.CalculateExpiry(RetentionRecordClass.SupportAttachment, Trigger);
        var second = RetentionPolicyCatalog.CalculateExpiry(RetentionRecordClass.SupportAttachment, Trigger);

        Assert.Equal(first, second);
        Assert.Equal(Trigger.AddDays(90), first);
    }

    [Fact]
    public void YearBasedPeriodsUseCalendarYearArithmeticNotFixedDayCounts()
    {
        // 2028 is a leap year within the 7-year window from 2026-01-01; calendar-year arithmetic
        // must land on 2033-01-01 regardless of how many leap days fall in between.
        var expiry = RetentionPolicyCatalog.CalculateExpiry(RetentionRecordClass.CanonicalBookingEvidence, Trigger);

        Assert.Equal(new DateTimeOffset(2033, 1, 1, 0, 0, 0, TimeSpan.Zero), expiry);
    }

    [Fact]
    public void IsExpiredIsFalseOneSecondBeforeTheBoundary()
    {
        var expiry = RetentionPolicyCatalog.CalculateExpiry(RetentionRecordClass.SupportAttachment, Trigger);

        Assert.False(RetentionPolicyCatalog.IsExpired(RetentionRecordClass.SupportAttachment, Trigger, expiry.AddSeconds(-1)));
    }

    [Fact]
    public void IsExpiredIsTrueExactlyAtTheBoundary()
    {
        var expiry = RetentionPolicyCatalog.CalculateExpiry(RetentionRecordClass.SupportAttachment, Trigger);

        Assert.True(RetentionPolicyCatalog.IsExpired(RetentionRecordClass.SupportAttachment, Trigger, expiry));
    }

    [Fact]
    public void IsExpiredIsTrueAfterTheBoundary()
    {
        var expiry = RetentionPolicyCatalog.CalculateExpiry(RetentionRecordClass.SupportAttachment, Trigger);

        Assert.True(RetentionPolicyCatalog.IsExpired(RetentionRecordClass.SupportAttachment, Trigger, expiry.AddSeconds(1)));
    }

    [Theory]
    [InlineData(RetentionRecordClass.CanonicalBookingEvidence)]
    [InlineData(RetentionRecordClass.SuccessfulSupplierPayload)]
    [InlineData(RetentionRecordClass.ExceptionalSupplierPayload)]
    [InlineData(RetentionRecordClass.TravellerSensitiveFieldMinimisation)]
    public void PolicyOnlyClassesAreMarkedAsSuch(RetentionRecordClass recordClass)
    {
        Assert.Equal(RetentionAction.PolicyOnlyNoLiveSweep, RetentionPolicyCatalog.Get(recordClass).Action);
    }

    [Theory]
    [InlineData(RetentionRecordClass.BookingRelatedSupportTicket)]
    [InlineData(RetentionRecordClass.GeneralSupportTicket)]
    [InlineData(RetentionRecordClass.SupportAttachment)]
    [InlineData(RetentionRecordClass.SecurityAuditRecord)]
    [InlineData(RetentionRecordClass.WebhookPayloadBody)]
    [InlineData(RetentionRecordClass.NotificationRenderedContent)]
    [InlineData(RetentionRecordClass.AbandonedCheckoutState)]
    public void ConcretelySweptClassesUseDeleteAction(RetentionRecordClass recordClass)
    {
        Assert.Equal(RetentionAction.Delete, RetentionPolicyCatalog.Get(recordClass).Action);
    }

    [Fact]
    public void CustomerAccountClosureUsesDeIdentifyAction()
    {
        Assert.Equal(RetentionAction.DeIdentify, RetentionPolicyCatalog.Get(RetentionRecordClass.CustomerAccountClosure).Action);
    }
}
