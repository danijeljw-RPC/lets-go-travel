using Microsoft.AspNetCore.Http;

namespace ReadyToGoTravel.Web.Localization;

internal static class LocaleCatalog
{
    public const string CookieName = "rtgt.locale";
    public const string Default = "en-AU";

    public static IReadOnlyList<string> Supported { get; } = [Default];

    public static bool IsSupported(string locale) =>
        Supported.Contains(locale, StringComparer.OrdinalIgnoreCase);

    public static CookieOptions CreateCookieOptions(bool isHttps) => new()
    {
        HttpOnly = true,
        IsEssential = true,
        MaxAge = TimeSpan.FromDays(365),
        SameSite = SameSiteMode.Lax,
        Secure = isHttps,
    };
}
