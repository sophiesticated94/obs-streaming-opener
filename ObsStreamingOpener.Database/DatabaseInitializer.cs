using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database;

public sealed class DatabaseInitializer(StreamingOpenerDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);

        if (!await dbContext.MonitoredAccounts.AnyAsync(cancellationToken))
        {
            var account = new MonitoredAccount
            {
                DisplayName = "Default account",
                IsDefault = true,
                CreatedAt = DateTimeOffset.UtcNow
            };
            dbContext.MonitoredAccounts.Add(account);
            dbContext.MonitoredChannels.Add(new MonitoredChannel
            {
                MonitoredAccount = account,
                Provider = ProviderKind.YouTube,
                ExternalChannelId = "default-channel",
                DisplayName = "Default channel",
                IsDefault = true,
                IsEnabled = true,
                CreatedAt = DateTimeOffset.UtcNow
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
