using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReadyToGoTravel.Retention.Persistence;

internal sealed class RetentionDesignTimeDbContextFactory : IDesignTimeDbContextFactory<RetentionDbContext>
{
    public RetentionDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<RetentionDbContext>()
            .UseNpgsql("Host=localhost;Database=rtgt;Username=rtgt")
            .Options;

        return new RetentionDbContext(options);
    }
}
