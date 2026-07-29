using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ReadyToGoTravel.Booking.Persistence;

internal sealed class BookingDesignTimeDbContextFactory : IDesignTimeDbContextFactory<BookingDbContext>
{
    public BookingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<BookingDbContext>()
            .UseNpgsql("Host=localhost;Database=rtgt;Username=rtgt")
            .Options;

        return new BookingDbContext(options);
    }
}
