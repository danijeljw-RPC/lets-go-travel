namespace ReadyToGoTravel.Booking.Webhooks;

public sealed class LiteApiWebhookOptions
{
    public const string SectionName = "Booking:Webhooks:LiteApi";

    public bool Enabled { get; set; }

    public string Environment { get; set; } = "Production";

    public string? CurrentSecret { get; set; }

    public string? PreviousSecret { get; set; }

    public int MaximumBodyBytes { get; set; } = 256 * 1024;
}
