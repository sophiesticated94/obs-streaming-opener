using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Database.Model;

namespace ObsStreamingOpener.Database;

public sealed class StreamingOpenerDbContext(DbContextOptions<StreamingOpenerDbContext> options) : DbContext(options)
{
    public DbSet<MonitoredAccount> MonitoredAccounts => Set<MonitoredAccount>();

    public DbSet<MonitoredChannel> MonitoredChannels => Set<MonitoredChannel>();

    public DbSet<AudienceMember> AudienceMembers => Set<AudienceMember>();

    public DbSet<AudienceRelationshipPeriod> AudienceRelationshipPeriods => Set<AudienceRelationshipPeriod>();

    public DbSet<StreamSession> StreamSessions => Set<StreamSession>();

    public DbSet<ProviderConnection> ProviderConnections => Set<ProviderConnection>();

    public DbSet<ProviderCursor> ProviderCursors => Set<ProviderCursor>();

    public DbSet<StreamEvent> StreamEvents => Set<StreamEvent>();

    public DbSet<MetricSnapshot> MetricSnapshots => Set<MetricSnapshot>();

    public DbSet<WidgetConfiguration> WidgetConfigurations => Set<WidgetConfiguration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MonitoredAccount>()
            .HasMany(x => x.Channels)
            .WithOne(x => x.MonitoredAccount)
            .HasForeignKey(x => x.MonitoredAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.ProviderConnections)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.StreamSessions)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.Events)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.MetricSnapshots)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.AudienceRelationshipPeriods)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.Events)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.MetricSnapshots)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AudienceMember>()
            .HasMany(x => x.RelationshipPeriods)
            .WithOne(x => x.AudienceMember)
            .HasForeignKey(x => x.AudienceMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderConnection>()
            .HasMany(x => x.Cursors)
            .WithOne(x => x.ProviderConnection)
            .HasForeignKey(x => x.ProviderConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MetricSnapshot>()
            .HasOne(x => x.ProviderConnection)
            .WithMany()
            .HasForeignKey(x => x.ProviderConnectionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamEvent>()
            .HasOne(x => x.AudienceMember)
            .WithMany()
            .HasForeignKey(x => x.AudienceMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<AudienceRelationshipPeriod>()
            .HasOne(x => x.SourceEvent)
            .WithMany()
            .HasForeignKey(x => x.SourceEventId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MonitoredAccount>()
            .HasIndex(x => x.IsDefault);

        modelBuilder.Entity<MonitoredChannel>()
            .HasIndex(x => new { x.Provider, x.ExternalChannelId })
            .IsUnique();

        modelBuilder.Entity<MonitoredChannel>()
            .HasIndex(x => x.IsDefault);

        modelBuilder.Entity<AudienceMember>()
            .HasIndex(x => new { x.Provider, x.ExternalAudienceId })
            .IsUnique();

        modelBuilder.Entity<AudienceRelationshipPeriod>()
            .HasIndex(x => new { x.MonitoredChannelId, x.AudienceMemberId, x.RelationshipKind, x.StartedAt });

        modelBuilder.Entity<StreamSession>()
            .HasIndex(x => new { x.MonitoredChannelId, x.IsActive });

        modelBuilder.Entity<ProviderConnection>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalChannelId, x.ExternalStreamId })
            .IsUnique();

        modelBuilder.Entity<ProviderCursor>()
            .HasIndex(x => new { x.ProviderConnectionId, x.CursorName })
            .IsUnique();

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalEventId })
            .IsUnique()
            .HasFilter("[ExternalEventId] IS NOT NULL");

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamSessionId, x.EventType });

        modelBuilder.Entity<MetricSnapshot>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamSessionId, x.Metric, x.CapturedAt });

        modelBuilder.Entity<WidgetConfiguration>()
            .HasIndex(x => x.WidgetKey)
            .IsUnique();
    }
}
