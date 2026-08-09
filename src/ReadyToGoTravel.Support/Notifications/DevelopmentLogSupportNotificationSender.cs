using Microsoft.Extensions.Logging;

namespace ReadyToGoTravel.Support.Notifications;

// Development-only stand-in for a real email provider. Logs the full notification, including any
// guest-acknowledgement magic-link token embedded in the payload, so a developer can exercise the
// guest flow locally without a live sender or ever querying persisted (hash-only) credential
// storage for a raw token that is intentionally never stored there. SupportModule only ever
// constructs this when Support:Notifications:Sender is explicitly set to "DevelopmentLog" *and*
// the host environment reports Development - see SupportModule.AddSupportModule - so it can never
// be reached by a production configuration mistake alone.
internal sealed partial class DevelopmentLogSupportNotificationSender(ILogger<DevelopmentLogSupportNotificationSender> logger)
    : ISupportNotificationSender
{
    public Task<SupportNotificationSendResult> SendAsync(SupportNotification notification, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LogNotification(logger, notification.Template, notification.RecipientEmail, notification.DedupeKey, notification.PayloadJson);
        return Task.FromResult(SupportNotificationSendResult.Sent($"dev-log:{notification.DedupeKey}"));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "[DEV ONLY - not a real send] Support notification {Template} to {RecipientEmail} (dedupe {DedupeKey}): {PayloadJson}")]
    private static partial void LogNotification(ILogger logger, string template, string recipientEmail, string dedupeKey, string payloadJson);
}
