using ReadyToGoTravel.Support.Notifications;

namespace ReadyToGoTravel.Support.Tests;

internal sealed class RecordingSupportNotificationSender : ISupportNotificationSender
{
    public List<SupportNotification> Sent { get; } = [];

    public Func<SupportNotification, SupportNotificationSendResult>? Behavior { get; set; }

    public Task<SupportNotificationSendResult> SendAsync(SupportNotification notification, CancellationToken cancellationToken = default)
    {
        Sent.Add(notification);
        var result = Behavior?.Invoke(notification) ?? SupportNotificationSendResult.Sent(Guid.NewGuid().ToString());
        return Task.FromResult(result);
    }
}
