using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Database;

public sealed class DatabaseInitializer(StreamingOpenerDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (!await dbContext.StreamSessions.AnyAsync(cancellationToken))
        {
            dbContext.StreamSessions.Add(new StreamSession
            {
                Title = "Current stream",
                IsActive = true,
                StartedAt = DateTimeOffset.UtcNow
            });
        }

        if (!await dbContext.WidgetConfigurations.AnyAsync(cancellationToken))
        {
            dbContext.WidgetConfigurations.AddRange(
                new WidgetConfiguration { WidgetKey = "stats", WidgetType = "stats", Theme = "default" },
                new WidgetConfiguration { WidgetKey = "recent-events", WidgetType = "recent-events", Theme = "default" },
                new WidgetConfiguration
                {
                    WidgetKey = "goal",
                    WidgetType = "goal",
                    Theme = "default",
                    SettingsJson = "{\"label\":\"Support goal\",\"target\":1000}"
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
