using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReadyToGoTravel.Consumer.Persistence;

internal sealed class ConsumerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ConsumerDbContext>
{
    public ConsumerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ConsumerDbContext>()
            .UseNpgsql("Host=localhost;Database=rtgt;Username=rtgt")
            .Options;

        return new ConsumerDbContext(options);
    }
}
