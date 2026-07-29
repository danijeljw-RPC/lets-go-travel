using Microsoft.AspNetCore.Http;
using ReadyToGoTravel.Web.Authentication;
using ReadyToGoTravel.Web.Localization;

namespace ReadyToGoTravel.Web.Tests;

public sealed class WebSecurityAndLocaleTests
{
    [Fact]
    public void LocaleCookieIsEssentialHttpOnlySameSiteAndSecureOverHttps()
    {
        var options = LocaleCatalog.CreateCookieOptions(isHttps: true);

        Assert.True(options.IsEssential);
        Assert.True(options.HttpOnly);
        Assert.True(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.Equal(TimeSpan.FromDays(365), options.MaxAge);
    }

    [Fact]
    public void LocaleCatalogueDefaultsToEnglishAustralia()
    {
        Assert.Equal("en-AU", LocaleCatalog.Default);
        Assert.Equal(["en-AU"], LocaleCatalog.Supported);
        Assert.False(LocaleCatalog.IsSupported("fr-FR"));
    }

    [Theory]
    [InlineData("https://api.example.test/api/v1/me", true)]
    [InlineData("https://api.example.test.evil.invalid/api/v1/me", false)]
    [InlineData("https://other.example.test/api/v1/me", false)]
    public void AccessTokenIsRestrictedToConfiguredApiOrigin(string requestUrl, bool expected)
    {
        var result = ApiAccessTokenHandler.IsApiOrigin(
            new Uri(requestUrl),
            new Uri("https://api.example.test"));

        Assert.Equal(expected, result);
    }
}
