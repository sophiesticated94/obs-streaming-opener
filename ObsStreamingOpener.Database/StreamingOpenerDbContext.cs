using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Database;

public sealed class StreamingOpenerDbContext(DbContextOptions<StreamingOpenerDbContext> options) : DbContext(options)
{
    public DbSet<StreamSession> StreamSessions => Set<StreamSession>();

    public DbSet<ProviderConnection> ProviderConnections => Set<ProviderConnection>();

    public DbSet<ProviderCursor> ProviderCursors => Set<ProviderCursor>();

    public DbSet<StreamEvent> StreamEvents => Set<StreamEvent>();

    public DbSet<MetricSnapshot> MetricSnapshots => Set<MetricSnapshot>();

    public DbSet<WidgetConfiguration> WidgetConfigurations => Set<WidgetConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.ProviderConnections)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.Events)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.MetricSnapshots)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderConnection>()
            .HasMany(x => x.Cursors)
            .WithOne(x => x.ProviderConnection)
            .HasForeignKey(x => x.ProviderConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderConnection>()
            .HasIndex(x => new { x.StreamSessionId, x.Provider, x.ExternalChannelId, x.ExternalStreamId })
            .IsUnique();

        modelBuilder.Entity<ProviderCursor>()
            .HasIndex(x => new { x.ProviderConnectionId, x.CursorName })
            .IsUnique();

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.StreamSessionId, x.Provider, x.ExternalEventId })
            .IsUnique()
            .HasFilter("[ExternalEventId] IS NOT NULL");

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => x.OccurredAt);

        modelBuilder.Entity<MetricSnapshot>()
            .HasIndex(x => new { x.Metric, x.CapturedAt });

        modelBuilder.Entity<WidgetConfiguration>()
            .HasIndex(x => x.WidgetKey)
            .IsUnique();
    }
}
