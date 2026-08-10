using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Localization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ReadyToGoTravel.Web.Authentication;
using ReadyToGoTravel.Web.Client;
using ReadyToGoTravel.Web.Components;
using ReadyToGoTravel.Web.Localization;
using ReadyToGoTravel.Web.Payments;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var cultures = LocaleCatalog.Supported
        .Select(code => new CultureInfo(code))
        .ToArray();

    options.DefaultRequestCulture = new RequestCulture(LocaleCatalog.Default);
    options.SupportedCultures = cultures;
    options.SupportedUICultures = cultures;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider { CookieName = LocaleCatalog.CookieName }
    ];
});

var authority = builder.Configuration["Authentication:Authority"]
    ?? throw new InvalidOperationException("Authentication:Authority is required.");
var clientId = builder.Configuration["Authentication:ClientId"]
    ?? throw new InvalidOperationException("Authentication:ClientId is required.");

JwtSecurityTokenHandler.DefaultMapInboundClaims = false;
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "rtgt.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.SlidingExpiration = true;
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = clientId;
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.UsePkce = true;
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Configuration.GetValue<bool>(
            "Authentication:AllowInsecureMetadata");
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "preferred_username",
            RoleClaimType = "roles"
        };
    });
builder.Services.AddAuthorization(options =>
    options.AddPolicy("support-agent", policy =>
        policy.RequireAuthenticatedUser().RequireAssertion(context => HasSupportAgentRole(context.User))));

var apiBaseUrl = builder.Configuration["PlatformApi:BaseUrl"]
    ?? throw new InvalidOperationException("PlatformApi:BaseUrl is required.");
builder.Services.AddTransient<ApiAccessTokenHandler>();
builder.Services.AddTransient<SupportClientIpForwardingHandler>();
builder.Services.AddHttpClient<PlatformApiClient>(client => ConfigureApiClient(client, apiBaseUrl));
builder.Services.AddHttpClient<SearchApiClient>(client => ConfigureApiClient(client, apiBaseUrl));
builder.Services.AddHttpClient<ConsumerApiClient>(client => ConfigureApiClient(client, apiBaseUrl))
    .AddHttpMessageHandler<ApiAccessTokenHandler>();
builder.Services.AddHttpClient<BookingApiClient>(client => ConfigureApiClient(client, apiBaseUrl))
    .AddHttpMessageHandler<ApiAccessTokenHandler>();
builder.Services.AddHttpClient<SupportApiClient>(client => ConfigureApiClient(client, apiBaseUrl))
    .AddHttpMessageHandler<ApiAccessTokenHandler>()
    .AddHttpMessageHandler<SupportClientIpForwardingHandler>();
builder.Services.AddHttpClient<SupportStaffApiClient>(client => ConfigureApiClient(client, apiBaseUrl))
    .AddHttpMessageHandler<ApiAccessTokenHandler>();
builder.Services.AddHttpClient<SupportGuestApiClient>(client => ConfigureApiClient(client, apiBaseUrl))
    .AddHttpMessageHandler<SupportClientIpForwardingHandler>();
builder.Services.AddTransient<HostedPaymentComponent>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseRequestLocalization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/account/sign-in", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = SafeReturnUrl(returnUrl) },
        [OpenIdConnectDefaults.AuthenticationScheme]));

app.MapPost("/account/sign-out", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    return Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
});

app.MapPost("/locale", async (HttpContext context, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(context);
    var form = await context.Request.ReadFormAsync();
    var locale = form["locale"].ToString();
    var returnUrl = SafeReturnUrl(form["returnUrl"].ToString());

    if (!LocaleCatalog.IsSupported(locale))
    {
        return Results.BadRequest();
    }

    context.Response.Cookies.Append(
        LocaleCatalog.CookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(locale)),
        LocaleCatalog.CreateCookieOptions(context.Request.IsHttps));

    return Results.LocalRedirect(returnUrl);
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static void ConfigureApiClient(HttpClient client, string baseUrl)
{
    client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
    client.Timeout = TimeSpan.FromSeconds(5);
}

static bool HasSupportAgentRole(System.Security.Claims.ClaimsPrincipal user)
{
    var realmAccess = user.FindFirst("realm_access")?.Value;
    if (string.IsNullOrWhiteSpace(realmAccess))
    {
        return false;
    }

    try
    {
        using var document = System.Text.Json.JsonDocument.Parse(realmAccess);
        if (!document.RootElement.TryGetProperty("roles", out var roles) ||
            roles.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return false;
        }

        foreach (var role in roles.EnumerateArray())
        {
            if (role.ValueKind == System.Text.Json.JsonValueKind.String &&
                string.Equals(role.GetString(), "support-agent", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
    catch (System.Text.Json.JsonException)
    {
        return false;
    }
}

static string SafeReturnUrl(string? returnUrl) =>
    !string.IsNullOrWhiteSpace(returnUrl) && Uri.IsWellFormedUriString(returnUrl, UriKind.Relative)
        && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            ? returnUrl
            : "/";
