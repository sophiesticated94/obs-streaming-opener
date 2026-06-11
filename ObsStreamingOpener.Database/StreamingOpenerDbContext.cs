using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

    public DbSet<ProviderCredential> ProviderCredentials => Set<ProviderCredential>();

    public DbSet<ProviderBrowserSession> ProviderBrowserSessions => Set<ProviderBrowserSession>();

    public DbSet<ProviderCursor> ProviderCursors => Set<ProviderCursor>();

    public DbSet<StreamEvent> StreamEvents => Set<StreamEvent>();

    public DbSet<Tip> Tips => Set<Tip>();

    public DbSet<StreamAlert> StreamAlerts => Set<StreamAlert>();

    public DbSet<AlertRule> AlertRules => Set<AlertRule>();

    public DbSet<ProviderMessage> ProviderMessages => Set<ProviderMessage>();

    public DbSet<MetricSnapshot> MetricSnapshots => Set<MetricSnapshot>();

    public DbSet<ProviderResource> ProviderResources => Set<ProviderResource>();

    public DbSet<WidgetConfiguration> WidgetConfigurations => Set<WidgetConfiguration>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder
            .Properties<Enum>()
            .HaveConversion<string>();

        configurationBuilder
            .Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToUtcDateTimeConverter>();

        base.ConfigureConventions(configurationBuilder);
    }

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
            .HasMany(x => x.Tips)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.Alerts)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.AlertRules)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.Messages)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.MetricSnapshots)
            .WithOne(x => x.MonitoredChannel)
            .HasForeignKey(x => x.MonitoredChannelId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MonitoredChannel>()
            .HasMany(x => x.ProviderResources)
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
            .HasMany(x => x.Tips)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.MetricSnapshots)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamSession>()
            .HasMany(x => x.Alerts)
            .WithOne(x => x.StreamSession)
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StreamSession>()
            .HasOne(x => x.ProviderResource)
            .WithMany()
            .HasForeignKey(x => x.ProviderResourceId)
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

        modelBuilder.Entity<MonitoredAccount>()
            .HasMany(x => x.ProviderCredentials)
            .WithOne(x => x.MonitoredAccount)
            .HasForeignKey(x => x.MonitoredAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<MetricSnapshot>()
            .HasOne(x => x.ProviderConnection)
            .WithMany()
            .HasForeignKey(x => x.ProviderConnectionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<MetricSnapshot>()
            .HasOne(x => x.ProviderResource)
            .WithMany(x => x.MetricSnapshots)
            .HasForeignKey(x => x.ProviderResourceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamEvent>()
            .HasOne(x => x.AudienceMember)
            .WithMany()
            .HasForeignKey(x => x.AudienceMemberId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamEvent>()
            .HasOne(x => x.ProviderResource)
            .WithMany()
            .HasForeignKey(x => x.ProviderResourceId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StreamEvent>()
            .HasOne(x => x.Tip)
            .WithOne(x => x.StreamEvent)
            .HasForeignKey<Tip>(x => x.StreamEventId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Tip>()
            .HasOne(x => x.RefundedTip)
            .WithMany(x => x.Refunds)
            .HasForeignKey(x => x.RefundedTipId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StreamAlert>()
            .HasOne(x => x.StreamEvent)
            .WithMany(x => x.Alerts)
            .HasForeignKey(x => x.StreamEventId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProviderMessage>()
            .HasOne(x => x.StreamSession)
            .WithMany()
            .HasForeignKey(x => x.StreamSessionId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ProviderMessage>()
            .HasOne(x => x.ProviderResource)
            .WithMany()
            .HasForeignKey(x => x.ProviderResourceId)
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

        modelBuilder.Entity<StreamSession>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalSessionId })
            .IsUnique();

        modelBuilder.Entity<ProviderConnection>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalChannelId, x.ExternalStreamId })
            .IsUnique();

        modelBuilder.Entity<ProviderCredential>()
            .HasIndex(x => new { x.MonitoredAccountId, x.Provider })
            .IsUnique();

        modelBuilder.Entity<ProviderCredential>()
            .HasIndex(x => new { x.Provider, x.ExternalAccountId })
            .IsUnique();

        modelBuilder.Entity<ProviderBrowserSession>()
            .HasIndex(x => x.Provider)
            .IsUnique();

        modelBuilder.Entity<ProviderCursor>()
            .HasIndex(x => new { x.ProviderConnectionId, x.CursorName })
            .IsUnique();

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalEventId })
            .HasFilter("[ExternalEventId] IS NOT NULL");

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.MonitoredChannelId, x.IdentityKey })
            .IsUnique();

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamSessionId, x.EventType });

        modelBuilder.Entity<StreamEvent>()
            .HasIndex(x => new { x.MonitoredChannelId, x.ProviderResourceId, x.EventType });

        modelBuilder.Entity<Tip>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamSessionId, x.OccurredAt });

        modelBuilder.Entity<Tip>()
            .HasIndex(x => x.StreamEventId)
            .IsUnique();

        modelBuilder.Entity<Tip>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalTipId })
            .HasFilter("[ExternalTipId] IS NOT NULL");

        modelBuilder.Entity<Tip>()
            .HasIndex(x => new { x.Provider, x.ExternalTipId })
            .HasFilter("[ExternalTipId] IS NOT NULL");

        modelBuilder.Entity<Tip>()
            .HasIndex(x => new { x.MonitoredChannelId, x.TipKind, x.Status, x.OccurredAt });

        modelBuilder.Entity<Tip>()
            .HasIndex(x => new { x.MonitoredChannelId, x.CampaignExternalId });

        modelBuilder.Entity<Tip>()
            .HasIndex(x => x.RefundedTipId);

        modelBuilder.Entity<StreamAlert>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamSessionId, x.DisplayFromUtc, x.DisplayUntilUtc });

        modelBuilder.Entity<StreamAlert>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamEventId });

        modelBuilder.Entity<AlertRule>()
            .HasIndex(x => new { x.MonitoredChannelId, x.EventType })
            .IsUnique();

        modelBuilder.Entity<ProviderMessage>()
            .HasIndex(x => new { x.MonitoredChannelId, x.IdentityKey })
            .IsUnique();

        modelBuilder.Entity<ProviderMessage>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Source, x.PublishedAt });

        modelBuilder.Entity<ProviderResource>()
            .HasIndex(x => new { x.MonitoredChannelId, x.Provider, x.ExternalResourceId })
            .IsUnique();

        modelBuilder.Entity<ProviderResource>()
            .HasIndex(x => new { x.MonitoredChannelId, x.ResourceKind, x.PublishedAt });

        modelBuilder.Entity<ProviderResource>()
            .HasIndex(x => new { x.MonitoredChannelId, x.ResourceKind, x.ScheduledStartAt });

        modelBuilder.Entity<MetricSnapshot>()
            .HasIndex(x => new { x.MonitoredChannelId, x.StreamSessionId, x.ProviderResourceId, x.Metric, x.CapturedAt });

        modelBuilder.Entity<WidgetConfiguration>()
            .HasIndex(x => x.WidgetKey)
            .IsUnique();
    }
}

public sealed class DateTimeOffsetToUtcDateTimeConverter
    : ValueConverter<DateTimeOffset, DateTime>
{
    public DateTimeOffsetToUtcDateTimeConverter()
        : base(
            v => v.UtcDateTime,
            v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc)))
    {
    }
}
