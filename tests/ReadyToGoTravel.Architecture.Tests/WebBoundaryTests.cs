using System.Xml.Linq;

namespace ReadyToGoTravel.Architecture.Tests;

public sealed class WebBoundaryTests
{
    [Fact]
    public void WebProjectReferencesNoServerAssembly()
    {
        var project = XDocument.Load(RepositoryFiles.FromRoot(
            "src/ReadyToGoTravel.Web/ReadyToGoTravel.Web.csproj"));
        var references = project.Descendants("ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty);

        Assert.Empty(references);
    }

    [Fact]
    public void WebConsumesPlatformIdentityThroughThePublicV1Api()
    {
        var clientSource = File.ReadAllText(RepositoryFiles.FromRoot(
            "src/ReadyToGoTravel.Web/Client/PlatformApiClient.cs"));

        Assert.Contains("/api/v1/platform", clientSource, StringComparison.Ordinal);
    }

    [Fact]
    public void WebConsumesConsumerDataThroughThePublicV1Api()
    {
        var clientSource = File.ReadAllText(RepositoryFiles.FromRoot(
            "src/ReadyToGoTravel.Web/Client/ConsumerApiClient.cs"));

        Assert.Contains("/api/v1/me", clientSource, StringComparison.Ordinal);
        Assert.Contains("/api/v1/trips", clientSource, StringComparison.Ordinal);
        Assert.Contains("/api/v1/travellers", clientSource, StringComparison.Ordinal);
    }
}
