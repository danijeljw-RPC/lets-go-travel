namespace ReadyToGoTravel.Support.Notifications;

public enum SupportNotificationSenderMode
{
    Disabled,
    DevelopmentLog,
}

public sealed class SupportNotificationSenderOptions
{
    public const string SectionName = "Support:Notifications";

    public SupportNotificationSenderMode Sender { get; set; } = SupportNotificationSenderMode.Disabled;
}
