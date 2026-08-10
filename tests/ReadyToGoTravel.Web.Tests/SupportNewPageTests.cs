namespace ReadyToGoTravel.Web.Tests;

public sealed class SupportNewPageTests
{
    [Fact]
    public void AnAuthenticatedSubmitterIsRoutedToTheirTicketDetail()
    {
        var ticketId = Guid.NewGuid();

        var redirect = Components.Pages.SupportNew.ResolvePostCreateRedirect(isAuthenticated: true, ticketId);

        Assert.Equal($"/support/{ticketId}", redirect);
    }

    [Fact]
    public void AnAnonymousSubmitterIsNeverRoutedToTheAuthenticatedTicketDetail()
    {
        var ticketId = Guid.NewGuid();

        var redirect = Components.Pages.SupportNew.ResolvePostCreateRedirect(isAuthenticated: false, ticketId);

        Assert.Null(redirect);
    }

    [Fact]
    public void TheAuthenticatedTicketDetailPageStillRequiresAuthorization()
    {
        var page = Read("src/ReadyToGoTravel.Web/Components/Pages/SupportTicket.razor");

        Assert.Contains("@attribute [Authorize]", page, StringComparison.Ordinal);
    }

    [Fact]
    public void TheNewTicketPageShowsAGuestConfirmationInsteadOfNavigatingAnonymousSubmitters()
    {
        var page = Read("src/ReadyToGoTravel.Web/Components/Pages/SupportNew.razor");

        Assert.Contains("guestConfirmation", page, StringComparison.Ordinal);
        Assert.Contains("emailed a secure link", page, StringComparison.Ordinal);
    }

    private static string Read(string path) => File.ReadAllText(Path.Combine(FindRoot(), path));

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
