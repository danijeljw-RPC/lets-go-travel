using System.Xml.Linq;

namespace ReadyToGoTravel.Architecture.Tests;

public sealed class SolutionConventionsTests
{
    [Fact]
    public void RepositoryBuildTreatsWarningsAsErrors()
    {
        var buildProperties = XDocument.Load(RepositoryFiles.FromRoot("Directory.Build.props"));

        var value = buildProperties
            .Descendants("TreatWarningsAsErrors")
            .SingleOrDefault()?
            .Value;

        Assert.Equal("true", value);
    }

    [Fact]
    public void AllProjectsTargetNet10()
    {
        var projects = RepositoryFiles.ProjectFiles();

        Assert.All(projects, project =>
        {
            var xml = XDocument.Load(project);
            Assert.Equal("net10.0", xml.Descendants("TargetFramework").Single().Value);
        });
    }

    [Fact]
    public void ProjectNamesUseReadyToGoTravelPrefix()
    {
        Assert.All(
            RepositoryFiles.ProjectFiles(),
            project => Assert.StartsWith(
                "ReadyToGoTravel.",
                Path.GetFileNameWithoutExtension(project),
                StringComparison.Ordinal));
    }
}

internal static class RepositoryFiles
{
    private static readonly string Root = FindRoot();

    public static string FromRoot(string path) => Path.Combine(Root, path);

    public static IReadOnlyList<string> ProjectFiles() =>
        Directory.GetFiles(Root, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ReadyToGoTravel.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the repository root.");
    }
}
