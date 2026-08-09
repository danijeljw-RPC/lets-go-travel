using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyToGoTravel.Support.Application;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;
using ReadyToGoTravel.Support.Persistence;
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
        services.AddScoped<IGuestAccessTokenService, GuestAccessTokenService>();
        services.AddScoped<SupportAttachmentService>();
        services.AddScoped<IAttachmentScanProcessor, AttachmentScanProcessor>();
        services.TryAddSingleton<ISupportNotificationSender, DisabledSupportNotificationSender>();
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
}
