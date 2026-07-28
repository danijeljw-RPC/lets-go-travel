using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace ReadyToGoTravel.Api.Infrastructure;

internal static class AuthenticationExtensions
{
    public static IServiceCollection AddPlatformAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var authority = configuration["Authentication:Authority"]
            ?? throw new InvalidOperationException("Authentication:Authority is required.");
        var audience = configuration["Authentication:Audience"]
            ?? throw new InvalidOperationException("Authentication:Audience is required.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.Audience = audience;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = !configuration.GetValue<bool>("Authentication:AllowInsecureMetadata");
            });
        services.AddAuthorization(options =>
            options.AddPolicy("consumer", policy =>
                policy.RequireAuthenticatedUser().RequireClaim("sub")));

        return services;
    }
}
