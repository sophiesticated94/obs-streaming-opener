using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.DataProtection;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Application.Options;
using ObsStreamingOpener.Application.Services;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Tests;

public sealed class ServiceUnitTests
{
    [Fact]
    public async Task EventIngestionService_DoesNotStoreDuplicateExternalEvent()
    {
        var eventStore = new FakeEventStore { ExistingExternalEventId = "duplicate" };
        var service = new EventIngestionService(eventStore, new TestClock());

        var result = await service.IngestAsync(new ProviderEvent(
            Guid.NewGuid(),
            null,
            null,
            null,
            ProviderKind.YouTube,
            StreamEventType.ChatMessage,
            "duplicate",
            "Viewer",
            "viewer-1",
            "Chat",
            "Hello",
            null,
            null,
            new TestClock().UtcNow,
            "{}"));

        Assert.True(result.Duplicate);
        Assert.Empty(eventStore.StoredEvents);
    }

    [Fact]
    public async Task EventIngestionService_StoresNormalizedEventWithClockStoredAt()
    {
        var eventStore = new FakeEventStore();
        var clock = new TestClock();
        var channelId = Guid.NewGuid();
        var service = new EventIngestionService(eventStore, clock);

        var result = await service.IngestAsync(new ProviderEvent(
            channelId,
            null,
            null,
            null,
            ProviderKind.Custom,
            StreamEventType.System,
            "event-1",
            null,
            null,
            "Title",
            "Message",
            null,
            null,
            clock.UtcNow.AddMinutes(-5),
            "{}"));

        Assert.True(result.Stored);
        var stored = Assert.Single(eventStore.StoredEvents);
        Assert.Equal(channelId, stored.MonitoredChannelId);
        Assert.Equal(clock.UtcNow, stored.StoredAt);
    }

    [Fact]
    public async Task AudienceIngestionService_DoesNotCreateSecondPeriodWhenRelationshipIsActive()
    {
        var channelId = Guid.NewGuid();
        var audienceMember = new AudienceMember
        {
            Id = Guid.NewGuid(),
            Provider = ProviderKind.YouTube,
            ExternalAudienceId = "audience-1"
        };
        var existingPeriod = new AudienceRelationshipPeriod
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = channelId,
            AudienceMemberId = audienceMember.Id,
            RelationshipKind = AudienceRelationshipKind.Free,
            StartedAt = new TestClock().UtcNow
        };
        var audienceStore = new FakeAudienceStore(audienceMember, existingPeriod);
        var service = new AudienceIngestionService(audienceStore, new FakeEventIngestionService(), new TestClock());

        var result = await service.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channelId,
            ProviderKind.YouTube,
            "audience-1",
            "Audience",
            null,
            AudienceRelationshipKind.Free,
            new TestClock().UtcNow,
            false,
            "{}"));

        Assert.False(result.CreatedRelationshipPeriod);
        Assert.False(result.Renewed);
        Assert.Empty(audienceStore.CreatedPeriods);
    }

    [Fact]
    public async Task AudienceIngestionService_CreatesRenewalWhenPreviousRelationshipEnded()
    {
        var channelId = Guid.NewGuid();
        var audienceMember = new AudienceMember
        {
            Id = Guid.NewGuid(),
            Provider = ProviderKind.YouTube,
            ExternalAudienceId = "audience-1"
        };
        var previousPeriod = new AudienceRelationshipPeriod
        {
            Id = Guid.NewGuid(),
            MonitoredChannelId = channelId,
            AudienceMemberId = audienceMember.Id,
            RelationshipKind = AudienceRelationshipKind.Free,
            StartedAt = new TestClock().UtcNow.AddDays(-10),
            EndedAt = new TestClock().UtcNow.AddDays(-1)
        };
        var eventIngestion = new FakeEventIngestionService();
        var audienceStore = new FakeAudienceStore(audienceMember, previousPeriod);
        var service = new AudienceIngestionService(audienceStore, eventIngestion, new TestClock());

        var result = await service.IngestRelationshipAsync(new ProviderAudienceRelationship(
            channelId,
            ProviderKind.YouTube,
            "audience-1",
            "Audience",
            null,
            AudienceRelationshipKind.Free,
            new TestClock().UtcNow,
            false,
            "{}"));

        Assert.True(result.Renewed);
        Assert.Single(audienceStore.CreatedPeriods);
        Assert.Single(eventIngestion.Events);
        Assert.Equal(StreamEventType.AudienceRelationshipRenewed, eventIngestion.Events[0].EventType);
    }

    [Fact]
    public async Task AccountDataPoller_ContinuesAfterMonitorFailure()
    {
        var first = new FakeTipProviderMonitor("first", shouldThrow: true);
        var second = new FakeTipProviderMonitor("second", shouldThrow: false);
        var poller = new AccountDataPoller([first, second], NullLogger<AccountDataPoller>.Instance);

        await poller.PollAsync();

        Assert.True(first.WasPolled);
        Assert.True(second.WasPolled);
    }

    [Fact]
    public void DataProtectionCredentialProtector_EncryptsAndDecryptsToken()
    {
        var directory = Directory.CreateTempSubdirectory("oso-data-protection-");
        var protector = new DataProtectionCredentialProtector(DataProtectionProvider.Create(directory));

        var encrypted = protector.Protect("raw-token");

        Assert.NotEqual("raw-token", encrypted);
        Assert.Equal("raw-token", protector.Unprotect(encrypted));
    }

    [Fact]
    public void YouTubeOAuthService_StartBuildsGoogleAuthorizationUrl()
    {
        var service = CreateOAuthService();

        var result = service.Start(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var uri = new Uri(result.AuthorizationUrl);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        Assert.Equal("accounts.google.com", uri.Host);
        Assert.Equal("test-client-id", query["client_id"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Contains("openid", query["scope"].ToString());
        Assert.Contains("https://www.googleapis.com/auth/youtube.readonly", query["scope"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
    }

    [Fact]
    public async Task YouTubeOAuthService_RefreshUpdatesStoredEncryptedToken()
    {
        var store = new FakeProviderCredentialStore
        {
            Credential = new StoredProviderCredentialDto(
                Guid.NewGuid(),
                Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
                ProviderKind.YouTube,
                "google-user",
                "creator@example.test",
                "Creator",
                "protected-old-access",
                "protected-refresh",
                new TestClock().UtcNow.AddMinutes(-1),
                "Bearer",
                "old-scope",
                new TestClock().UtcNow.AddDays(-1),
                null,
                null)
        };
        var protector = new FakeCredentialProtector();
        var service = CreateOAuthService(store: store, credentialProtector: protector);

        var result = await service.RefreshAsync(store.Credential.MonitoredAccountId);

        Assert.NotNull(result);
        Assert.Equal("protected-refreshed-access-token", store.UpdatedEncryptedAccessToken);
        Assert.True(store.UpdatedExpiresAt > new TestClock().UtcNow);
    }

    [Fact]
    public void ConnectedAccountDto_DoesNotExposeRawTokenFields()
    {
        var properties = typeof(ConnectedAccountDto).GetProperties().Select(x => x.Name).ToHashSet();

        Assert.DoesNotContain("AccessToken", properties);
        Assert.DoesNotContain("RefreshToken", properties);
        Assert.DoesNotContain("EncryptedAccessToken", properties);
        Assert.DoesNotContain("EncryptedRefreshToken", properties);
    }

    private static YouTubeOAuthService CreateOAuthService(
        FakeProviderCredentialStore? store = null,
        ICredentialProtector? credentialProtector = null)
    {
        var directory = Directory.CreateTempSubdirectory("oso-oauth-state-");
        return new YouTubeOAuthService(
            Options.Create(new YouTubeOAuthOptions
            {
                ClientId = "test-client-id",
                ClientSecret = "test-client-secret",
                RedirectUri = "http://localhost/api/auth/youtube/callback"
            }),
            new TestClock(),
            DataProtectionProvider.Create(directory),
            credentialProtector ?? new FakeCredentialProtector(),
            new FakeYouTubeOAuthClient(),
            store ?? new FakeProviderCredentialStore());
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 3, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeEventStore : IEventStore
    {
        public string? ExistingExternalEventId { get; init; }

        public List<StreamEvent> StoredEvents { get; } = [];

        public Task<bool> EventExistsAsync(Guid monitoredChannelId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default)
            => Task.FromResult(externalEventId == ExistingExternalEventId);

        public Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
        {
            StoredEvents.Add(streamEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(
            Guid? monitoredChannelId,
            ProviderKind? provider,
            StreamEventType? eventType,
            int limit,
            Guid? providerResourceId = null,
            Guid? streamSessionId = null,
            Guid? audienceMemberId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RecentEventDto>>([]);
    }

    private sealed class FakeEventIngestionService : IEventIngestionService
    {
        public List<ProviderEvent> Events { get; } = [];

        public Task<IngestedEventResult> IngestAsync(ProviderEvent providerEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(providerEvent);
            return Task.FromResult(new IngestedEventResult(Guid.NewGuid(), Stored: true, Duplicate: false));
        }
    }

    private sealed class FakeAudienceStore(AudienceMember audienceMember, AudienceRelationshipPeriod? latestPeriod) : IAudienceStore
    {
        public List<AudienceRelationshipPeriod> CreatedPeriods { get; } = [];

        public Task<(AudienceMember AudienceMember, bool Created)> UpsertAudienceMemberAsync(
            ProviderKind provider,
            string externalAudienceId,
            string? displayName,
            string? profileUrl,
            CancellationToken cancellationToken = default)
            => Task.FromResult((audienceMember, Created: false));

        public Task<AudienceRelationshipPeriod?> GetLatestRelationshipPeriodAsync(
            Guid monitoredChannelId,
            Guid audienceMemberId,
            AudienceRelationshipKind relationshipKind,
            CancellationToken cancellationToken = default)
            => Task.FromResult(latestPeriod);

        public Task AddRelationshipPeriodAsync(AudienceRelationshipPeriod period, CancellationToken cancellationToken = default)
        {
            CreatedPeriods.Add(period);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRecentRelationshipsAsync(Guid monitoredChannelId, int limit, bool includeRevenue = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudienceRelationshipPeriodDto>>([]);

        public Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRelationshipHistoryAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AudienceRelationshipPeriodDto>>([]);

        public Task<AudienceRevenueSummaryDto> GetAudienceRevenueAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default)
            => Task.FromResult(new AudienceRevenueSummaryDto(null, null, []));
    }

    private sealed class FakeTipProviderMonitor(string name, bool shouldThrow) : ITipProviderMonitor
    {
        public string Name { get; } = name;

        public bool WasPolled { get; private set; }

        public Task PollAsync(CancellationToken cancellationToken = default)
        {
            WasPolled = true;
            return shouldThrow ? throw new InvalidOperationException("monitor failed") : Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialProtector : ICredentialProtector
    {
        public string Protect(string value) => $"protected-{value}";

        public string Unprotect(string value) => value.Replace("protected-", string.Empty, StringComparison.Ordinal);
    }

    private sealed class FakeYouTubeOAuthClient : IYouTubeOAuthClient
    {
        public Task<YouTubeTokenResponse> ExchangeCodeAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeTokenResponse("access-token", "refresh-token", 3600, "Bearer", "scope"));

        public Task<YouTubeTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeTokenResponse("refreshed-access-token", null, 3600, "Bearer", "scope"));

        public Task<YouTubeUserInfo> GetUserInfoAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult(new YouTubeUserInfo("google-user", "creator@example.test", "Creator"));

        public Task<IReadOnlyList<YouTubeChannelInfo>> GetMyChannelsAsync(string accessToken, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<YouTubeChannelInfo>>([]);
    }

    private sealed class FakeProviderCredentialStore : IProviderCredentialStore
    {
        public StoredProviderCredentialDto? Credential { get; set; }

        public string? UpdatedEncryptedAccessToken { get; private set; }

        public DateTimeOffset? UpdatedExpiresAt { get; private set; }

        public Task<IReadOnlyList<ConnectedAccountDto>> GetConnectedAccountsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ConnectedAccountDto>>(
                Credential is null
                    ? []
                    :
                    [
                        new ConnectedAccountDto(
                            Credential.MonitoredAccountId,
                            Credential.DisplayName ?? "Creator",
                            ProviderKind.YouTube,
                            Credential.ExternalAccountId,
                            Credential.Email,
                            0,
                            Credential.DisconnectedAt is null && !string.IsNullOrWhiteSpace(Credential.EncryptedAccessToken),
                            !string.IsNullOrWhiteSpace(Credential.EncryptedRefreshToken),
                            Credential.AccessTokenExpiresAt,
                            false,
                            Credential.LastRefreshedAt,
                            Credential.DisconnectedAt,
                            Credential.Scopes)
                    ]);

        public Task<StoredProviderCredentialDto?> GetYouTubeCredentialAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(Credential);

        public Task<StoredProviderCredentialDto?> GetYouTubeCredentialForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
            => Task.FromResult(Credential);

        public Task<Guid> UpsertYouTubeAccountAsync(UpsertYouTubeAccountRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(Guid.NewGuid());

        public Task UpdateYouTubeCredentialTokensAsync(
            Guid accountId,
            string encryptedAccessToken,
            string? encryptedRefreshToken,
            DateTimeOffset accessTokenExpiresAt,
            string? tokenType,
            string scopes,
            CancellationToken cancellationToken = default)
        {
            UpdatedEncryptedAccessToken = encryptedAccessToken;
            UpdatedExpiresAt = accessTokenExpiresAt;
            Credential = Credential is null
                ? null
                : Credential with
                {
                    EncryptedAccessToken = encryptedAccessToken,
                    AccessTokenExpiresAt = accessTokenExpiresAt,
                    TokenType = tokenType,
                    Scopes = scopes
                };
            return Task.CompletedTask;
        }

        public Task<bool> DisconnectYouTubeCredentialAsync(Guid accountId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
