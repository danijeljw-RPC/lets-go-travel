using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ReadyToGoTravel.Consumer.Retention;
using ReadyToGoTravel.Retention;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;
using ReadyToGoTravel.Support.Retention;
using ReadyToGoTravel.Support.Scanning;
using ReadyToGoTravel.Support.Storage;

namespace ReadyToGoTravel.Support;

public static class SupportModule
{
    public static IServiceCollection AddSupportModule(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder> configureDatabase)
    {
        ArgumentNullException.ThrowIfNull(configureDatabase);

        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<SupportDbContext>(configureDatabase);
        services.AddScoped<SupportTicketService>();

        services.AddOptions<GuestTokenOptions>()
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.NotificationSigningKey) && TryDecodeSigningKey(options.NotificationSigningKey, out _),
                $"Support:GuestTokens:NotificationSigningKey must be set to a base64-encoded secret of at least {MinimumSigningKeyBytes} random bytes (never persisted in the database). " +
                    "Guest acknowledgement tokens are deterministic HMAC outputs over ticket/outbox IDs that are visible to anyone with database read access, so a short or guessable key would let that reader brute-force it and reconstruct every active bearer token.")
            .ValidateOnStart();
        services.AddScoped<IGuestAccessTokenService>(provider =>
        {
            var options = provider.GetRequiredService<IOptions<GuestTokenOptions>>().Value;
            TryDecodeSigningKey(options.NotificationSigningKey, out var signingKey);
            return new GuestAccessTokenService(
                provider.GetRequiredService<SupportDbContext>(),
                provider.GetRequiredService<TimeProvider>(),
                signingKey);
        });

        services.AddScoped<SupportAttachmentService>();
        services.AddScoped<IAttachmentScanProcessor, AttachmentScanProcessor>();
        services.AddOptions<RetentionSweepOptions>().BindConfiguration(RetentionSweepOptions.SectionName);
        services.AddScoped<ISupportRetentionSweepProcessor, SupportRetentionSweepProcessor>();
        services.AddScoped<IConsumerRetentionEvidencePort, SupportConsumerRetentionEvidenceAdapter>();

        services.AddOptions<SupportNotificationSenderOptions>();
        services.TryAddSingleton<ISupportNotificationSender>(provider =>
        {
            var environment = provider.GetRequiredService<IHostEnvironment>();
            var mode = provider.GetRequiredService<IOptions<SupportNotificationSenderOptions>>().Value.Sender;
            if (mode == SupportNotificationSenderMode.DevelopmentLog)
            {
                // Belt-and-braces: even if a Production configuration somehow set this value, the
                // sender never activates outside a host that self-reports Development, so a
                // deployment mistake cannot silently start logging guest tokens instead of emailing
                // them.
                if (!environment.IsDevelopment())
                {
                    throw new InvalidOperationException(
                        "Support:Notifications:Sender=DevelopmentLog is only permitted when the host environment is Development.");
                }

                return ActivatorUtilities.CreateInstance<DevelopmentLogSupportNotificationSender>(provider);
            }

            return ActivatorUtilities.CreateInstance<DisabledSupportNotificationSender>(provider);
        });
        services.AddScoped<ISupportNotificationOutboxProcessor, SupportNotificationOutboxProcessor>();

        services.AddOptions<SupportStorageOptions>()
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.ServiceUrl), "An enabled Support storage requires Support:Storage:ServiceUrl.")
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.BucketName), "An enabled Support storage requires Support:Storage:BucketName.")
            .ValidateOnStart();
        services.AddScoped<IObjectStorage>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SupportStorageOptions>>().Value;
            return options.Enabled
                ? ActivatorUtilities.CreateInstance<S3ObjectStorage>(provider)
                : ActivatorUtilities.CreateInstance<DisabledObjectStorage>(provider);
        });

        services.AddOptions<ClamAvOptions>()
            .Validate(options => !options.Enabled || !string.IsNullOrWhiteSpace(options.Host), "An enabled ClamAV scanner requires Support:Scanning:ClamAv:Host.")
            .ValidateOnStart();
        services.AddScoped<IAttachmentScanner>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ClamAvOptions>>().Value;
            return options.Enabled
                ? ActivatorUtilities.CreateInstance<ClamAvScanner>(provider)
                : ActivatorUtilities.CreateInstance<DisabledAttachmentScanner>(provider);
        });

        services.AddHealthChecks()
            .AddDbContextCheck<SupportDbContext>("support_database", tags: ["ready"]);

        return services;
    }

    private const int MinimumSigningKeyBytes = 32;

    private static bool TryDecodeSigningKey(string value, out byte[] signingKey)
    {
        try
        {
            signingKey = Convert.FromBase64String(value);
            return signingKey.Length >= MinimumSigningKeyBytes;
        }
        catch (FormatException)
        {
            signingKey = [];
            return false;
        }
    }
}
