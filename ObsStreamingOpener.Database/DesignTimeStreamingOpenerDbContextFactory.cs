using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ObsStreamingOpener.Database;

public sealed class DesignTimeStreamingOpenerDbContextFactory : IDesignTimeDbContextFactory<StreamingOpenerDbContext>
{
    public StreamingOpenerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StreamingOpenerDbContext>()
            .UseSqlite("Data Source=streaming-opener.db")
            .Options;

        return new StreamingOpenerDbContext(options);
    }
}
