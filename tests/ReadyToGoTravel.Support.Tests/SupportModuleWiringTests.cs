using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ReadyToGoTravel.Support.Guest;
using ReadyToGoTravel.Support.Notifications;

namespace ReadyToGoTravel.Support.Tests;

public sealed class SupportModuleWiringTests
{
    [Fact]
    public async Task ADevelopmentLogSenderIsSelectedWhenTheHostEnvironmentIsDevelopment()
    {
        await using var provider = BuildProvider(environmentName: Environments.Development, senderMode: "DevelopmentLog");

        var sender = provider.GetRequiredService<ISupportNotificationSender>();

        Assert.IsType<DevelopmentLogSupportNotificationSender>(sender);
    }

    [Fact]
    public void SelectingTheDevelopmentLogSenderOutsideDevelopmentThrowsRatherThanSilentlyLoggingCredentials()
    {
        using var provider = BuildProvider(environmentName: Environments.Production, senderMode: "DevelopmentLog");

        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<ISupportNotificationSender>());
    }

    [Fact]
    public async Task TheDisabledSenderIsSelectedByDefault()
    {
        await using var provider = BuildProvider(environmentName: Environments.Production, senderMode: null);

        var sender = provider.GetRequiredService<ISupportNotificationSender>();

        Assert.IsType<DisabledSupportNotificationSender>(sender);
    }

    [Fact]
    public void StartupValidationRejectsAMissingNotificationSigningKey()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(Environments.Production));
        services.AddLogging();
        services.AddSupportModule((_, options) => options.UseSqlite("Data Source=:memory:"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(
            () => scope.ServiceProvider.GetRequiredService<IGuestAccessTokenService>());
    }

    [Fact]
    public void StartupValidationRejectsANotificationSigningKeyShorterThanTheCryptographicMinimum()
    {
        var services = new ServiceCollection();
        var configurationValues = new Dictionary<string, string?>
        {
            // A single random byte, base64-encoded: nonempty, so a length check alone would accept
            // it, but with only 256 possible values a database reader could brute-force it against
            // a stored token hash and reconstruct every deterministic guest acknowledgement token.
            ["Support:GuestTokens:NotificationSigningKey"] = "YQ==",
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(Environments.Production));
        services.AddLogging();
        services.AddSupportModule((_, options) => options.UseSqlite("Data Source=:memory:"));
        services.Configure<GuestTokenOptions>(configuration.GetSection(GuestTokenOptions.SectionName));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(
            () => scope.ServiceProvider.GetRequiredService<IGuestAccessTokenService>());
    }

    [Fact]
    public void StartupValidationAcceptsAThirtyTwoByteNotificationSigningKey()
    {
        var services = new ServiceCollection();
        var configurationValues = new Dictionary<string, string?>
        {
            ["Support:GuestTokens:NotificationSigningKey"] = Convert.ToBase64String(new byte[32]),
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(Environments.Production));
        services.AddLogging();
        services.AddSupportModule((_, options) => options.UseSqlite("Data Source=:memory:"));
        services.Configure<GuestTokenOptions>(configuration.GetSection(GuestTokenOptions.SectionName));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGuestAccessTokenService>());
    }

    private static ServiceProvider BuildProvider(string environmentName, string? senderMode)
    {
        var services = new ServiceCollection();
        var configurationValues = new Dictionary<string, string?>
        {
            ["Support:GuestTokens:NotificationSigningKey"] = Convert.ToBase64String(new byte[32]),
        };
        if (senderMode is not null)
        {
            configurationValues["Support:Notifications:Sender"] = senderMode;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(configurationValues).Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(environmentName));
        services.AddLogging();
        services.AddSupportModule((_, options) => options.UseSqlite("Data Source=:memory:"));
        services.Configure<GuestTokenOptions>(configuration.GetSection(GuestTokenOptions.SectionName));
        services.Configure<SupportNotificationSenderOptions>(configuration.GetSection(SupportNotificationSenderOptions.SectionName));
        return services.BuildServiceProvider(validateScopes: true);
    }

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
