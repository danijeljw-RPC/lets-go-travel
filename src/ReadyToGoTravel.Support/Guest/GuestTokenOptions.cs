namespace ReadyToGoTravel.Support.Guest;

public sealed class GuestTokenOptions
{
    public const string SectionName = "Support:GuestTokens";

    // Base64-encoded secret used only to deterministically regenerate a guest-acknowledgement
    // token already minted for the outbox item retrying its delivery - never stored in the
    // database, never derivable from data an attacker with database read access could see. Must be
    // supplied out-of-band (environment variable, secret store) in Production; there is no safe
    // default to fall back to there.
    public string NotificationSigningKey { get; set; } = string.Empty;
}
