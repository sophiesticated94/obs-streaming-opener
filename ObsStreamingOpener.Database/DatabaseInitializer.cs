using System.Data;
using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database;

public sealed class DatabaseInitializer(StreamingOpenerDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureProviderResourceColumnsAsync(cancellationToken);

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

    private async Task EnsureProviderResourceColumnsAsync(CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsSqlite())
        {
            return;
        }

        await EnsureColumnAsync("ProviderResources", "ThumbnailUrl", "TEXT", cancellationToken);
        await EnsureColumnAsync("ProviderResources", "DurationSeconds", "INTEGER", cancellationToken);
    }

    private async Task EnsureColumnAsync(string tableName, string columnName, string definition, CancellationToken cancellationToken)
    {
        if (await ColumnExistsAsync(tableName, columnName, cancellationToken))
        {
            return;
        }

        await ExecuteNonQueryAsync($"""ALTER TABLE "{tableName}" ADD COLUMN "{columnName}" {definition}""", cancellationToken);
    }

    private async Task ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"""PRAGMA table_info("{tableName}")""";
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
