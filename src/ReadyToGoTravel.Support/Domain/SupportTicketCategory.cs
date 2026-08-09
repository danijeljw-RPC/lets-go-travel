namespace ReadyToGoTravel.Support.Domain;

public enum SupportTicketCategory
{
    General,
    TravelWithin24Hours,
    PaymentBookingMismatch,
    SupplierCancellationOrRelocation,
    TravellerSafety,
    AccountOrOther,
}

public static class SupportTicketCategoryUrgency
{
    private static readonly HashSet<SupportTicketCategory> UrgentCategories =
    [
        SupportTicketCategory.TravelWithin24Hours,
        SupportTicketCategory.PaymentBookingMismatch,
        SupportTicketCategory.SupplierCancellationOrRelocation,
        SupportTicketCategory.TravellerSafety,
    ];

    public static bool IsUrgent(SupportTicketCategory category) => UrgentCategories.Contains(category);
}
