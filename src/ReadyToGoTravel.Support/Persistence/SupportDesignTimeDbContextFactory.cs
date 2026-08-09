using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReadyToGoTravel.Support.Persistence;

internal sealed class SupportDesignTimeDbContextFactory : IDesignTimeDbContextFactory<SupportDbContext>
{
    public SupportDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SupportDbContext>()
            .UseNpgsql("Host=localhost;Database=rtgt;Username=rtgt")
            .Options;

        return new SupportDbContext(options);
    }
}
