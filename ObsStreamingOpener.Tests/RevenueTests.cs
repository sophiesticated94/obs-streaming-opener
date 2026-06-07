using Microsoft.EntityFrameworkCore;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class RevenueTests
{
    [Fact]
    public void Money_DoesNotAddDifferentCurrencies()
    {
        var pln = new Money(10, "PLN");
        var eur = new Money(10, "EUR");

        Assert.Throws<InvalidOperationException>(() => pln.Add(eur));
        Assert.Equal(15, pln.Add(new Money(5, "PLN")).Amount);
    }

    [Fact]
    public void UtcRange_UsesInclusiveSinceExclusiveUntil()
    {
        var since = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        var until = since.AddDays(1);
        var range = new UtcRange(since, until);

        Assert.True(range.Contains(since));
        Assert.False(range.Contains(until));
    }

    [Fact]
    public async Task SupportIngestion_StoresRefundAsNegativeTipWithForeignKey()
    {
        await using var fixture = await RevenueFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var ingestion = fixture.CreateSupportIngestion();

        var original = await ingestion.IngestTipAsync(CreateTip(channel.Id, "donation-1", 100, TipKind.Donation));
        var refund = await ingestion.IngestTipAsync(CreateTip(channel.Id, "refund-1", 100, TipKind.Refund, refundedExternalTipId: "donation-1"));

        var originalTip = await fixture.DbContext.Tips.SingleAsync(x => x.Id == original.TipId);
        var refundTip = await fixture.DbContext.Tips.SingleAsync(x => x.Id == refund.TipId);

        Assert.Equal(100, originalTip.Amount);
        Assert.Equal(-100, refundTip.Amount);
        Assert.Equal(TipKind.Refund, refundTip.TipKind);
        Assert.Equal(originalTip.Id, refundTip.RefundedTipId);
    }

    [Fact]
    public async Task RevenueCalculator_SummarizesTipsPerCurrencyAndStatus()
    {
        await using var fixture = await RevenueFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        var ingestion = fixture.CreateSupportIngestion();
        await ingestion.IngestTipAsync(CreateTip(channel.Id, "donation-1", 100, TipKind.Donation));
        await ingestion.IngestTipAsync(CreateTip(channel.Id, "refund-1", 25, TipKind.Refund, refundedExternalTipId: "donation-1"));

        var calculator = new RevenueCalculator(fixture.Repository);
        var summary = await calculator.GetSummaryAsync(new RevenueSummaryQuery(channel.Id, null, null, null, null, null, null));

        var pln = Assert.Single(summary.Currencies);
        Assert.Equal(75, pln.Gross);
        Assert.Equal(1, pln.PositiveCount);
        Assert.Equal(1, pln.NegativeCount);
    }

    [Fact]
    public async Task Forecast_UsesOnlyActivePaidRelationshipsWithNextCharge()
    {
        await using var fixture = await RevenueFixture.CreateAsync();
        var channel = await fixture.Repository.GetDefaultChannelEntityAsync();
        fixture.DbContext.AudienceRelationshipPeriods.Add(new AudienceRelationshipPeriod
        {
            MonitoredChannelId = channel.Id,
            AudienceMemberId = (await CreateAudienceAsync(fixture.DbContext)).Id,
            RelationshipKind = AudienceRelationshipKind.Paid,
            Status = RelationshipStatus.Active,
            BillingCadence = BillingCadence.Monthly,
            Amount = 50,
            Currency = "PLN",
            StartedAt = fixture.Clock.UtcNow.AddMonths(-1),
            NextChargeAt = fixture.Clock.UtcNow.AddDays(1)
        });
        await fixture.DbContext.SaveChangesAsync();

        var forecast = await new RevenueForecastService(fixture.Repository, fixture.Clock).GetForecastAsync(channel.Id, 35);

        var pln = Assert.Single(forecast.Currencies);
        Assert.Equal(100, pln.EstimatedGross);
    }

    private static ProviderTipRecord CreateTip(Guid channelId, string externalId, decimal amount, TipKind kind, string? refundedExternalTipId = null)
        => new(
            channelId,
            null,
            ProviderKind.Tipply,
            kind,
            TipStatus.Settled,
            TipSource.Api,
            PaymentMethod.Card,
            externalId,
            refundedExternalTipId,
            "Supporter",
            "supporter-1",
            amount,
            "PLN",
            amount,
            null,
            null,
            [],
            "Thanks",
            new DateTimeOffset(2026, 6, 3, 12, 0, 0, TimeSpan.Zero),
            null,
            null,
            null,
            null,
            "{}");

    private static async Task<AudienceMember> CreateAudienceAsync(StreamingOpenerDbContext dbContext)
    {
        var audience = new AudienceMember
        {
            Provider = ProviderKind.Patronite,
            ExternalAudienceId = "patron-1",
            DisplayName = "Patron"
        };
        dbContext.AudienceMembers.Add(audience);
        await dbContext.SaveChangesAsync();
        return audience;
    }

    private sealed class RevenueFixture : IAsyncDisposable
    {
        private RevenueFixture(StreamingOpenerDbContext dbContext, TestClock clock)
        {
            DbContext = dbContext;
            Clock = clock;
            Repository = new StreamingOpenerRepository(dbContext, clock);
        }

        public StreamingOpenerDbContext DbContext { get; }

        public TestClock Clock { get; }

        public StreamingOpenerRepository Repository { get; }

        public ISupportIngestionService CreateSupportIngestion()
        {
            var eventIngestion = new EventIngestionService(Repository, Clock);
            var audienceIngestion = new AudienceIngestionService(Repository, eventIngestion, Clock);
            return new SupportIngestionService(eventIngestion, Repository, audienceIngestion, Clock);
        }

        public static async Task<RevenueFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<StreamingOpenerDbContext>()
                .UseInMemoryDatabase($"revenue-tests-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new StreamingOpenerDbContext(options);
            await new DatabaseInitializer(dbContext).InitializeAsync();
            return new RevenueFixture(dbContext, new TestClock());
        }

        public async ValueTask DisposeAsync()
            => await DbContext.DisposeAsync();
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    }
}
