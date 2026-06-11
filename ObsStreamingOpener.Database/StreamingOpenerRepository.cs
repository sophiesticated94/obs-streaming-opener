using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Database.Model;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Database;

public sealed class StreamingOpenerRepository(StreamingOpenerDbContext dbContext, IClock clock, IProviderResourcePatchService? providerResourcePatchService = null, ILogger<StreamingOpenerRepository>? logger = null) :
    IChannelStore,
    IEventStore,
    IStatsStore,
    IConfigurationStore,
    IProviderCredentialStore,
    IProviderCursorStore,
    IProviderResourceStore,
    IProviderMessageStore,
    IStreamAlertStore,
    IAlertRuleStore,
    IStreamSessionStore,
    IAudienceStore,
    ISupportTransactionStore
{
    private readonly IProviderResourcePatchService resourcePatchService = providerResourcePatchService ?? new ProviderResourcePatchService();

    public async Task<IReadOnlyList<MonitoredAccountDto>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.MonitoredAccounts
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .Select(x => new MonitoredAccountDto(x.Id, x.DisplayName, x.IsDefault))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoredChannelDto>> GetChannelsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.MonitoredChannels
            .AsNoTracking()
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .Select(x => new MonitoredChannelDto(
                x.Id,
                x.MonitoredAccountId,
                x.Provider,
                x.ExternalChannelId,
                x.DisplayName,
                x.Url,
                x.IsDefault,
                x.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<MonitoredChannelDto?> GetChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        return await dbContext.MonitoredChannels
            .AsNoTracking()
            .Where(x => x.Id == monitoredChannelId)
            .Select(x => new MonitoredChannelDto(
                x.Id,
                x.MonitoredAccountId,
                x.Provider,
                x.ExternalChannelId,
                x.DisplayName,
                x.Url,
                x.IsDefault,
                x.IsEnabled))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<MonitoredChannel> GetDefaultChannelEntityAsync(CancellationToken cancellationToken = default)
    {
        var channels = await dbContext.MonitoredChannels.ToListAsync(cancellationToken);
        var channel = channels
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.CreatedAt)
            .FirstOrDefault();

        if (channel is not null)
        {
            return channel;
        }

        var account = new MonitoredAccount
        {
            DisplayName = "Default account",
            IsDefault = true,
            CreatedAt = clock.UtcNow
        };
        channel = new MonitoredChannel
        {
            MonitoredAccount = account,
            Provider = ProviderKind.YouTube,
            ExternalChannelId = "default-channel",
            DisplayName = "Default channel",
            IsDefault = true,
            IsEnabled = true,
            CreatedAt = clock.UtcNow
        };

        dbContext.MonitoredChannels.Add(channel);
        await dbContext.SaveChangesAsync(cancellationToken);
        return channel;
    }

    public async Task<MonitoredChannelDto> GetDefaultChannelAsync(CancellationToken cancellationToken = default)
    {
        var channel = await GetDefaultChannelEntityAsync(cancellationToken);
        return new MonitoredChannelDto(
            channel.Id,
            channel.MonitoredAccountId,
            channel.Provider,
            channel.ExternalChannelId,
            channel.DisplayName,
            channel.Url,
            channel.IsDefault,
            channel.IsEnabled);
    }

    public async Task<MonitoredAccountDto> CreateAccountAsync(SaveAccountRequest request, CancellationToken cancellationToken = default)
    {
        if (request.IsDefault)
        {
            await ClearDefaultAccountsAsync(cancellationToken);
        }

        var account = new MonitoredAccount
        {
            DisplayName = request.DisplayName.Trim(),
            IsDefault = request.IsDefault,
            CreatedAt = clock.UtcNow
        };

        dbContext.MonitoredAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MonitoredAccountDto(account.Id, account.DisplayName, account.IsDefault);
    }

    public async Task<MonitoredAccountDto?> UpdateAccountAsync(Guid accountId, SaveAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = await dbContext.MonitoredAccounts.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken);
        if (account is null)
        {
            return null;
        }

        if (request.IsDefault)
        {
            await ClearDefaultAccountsAsync(cancellationToken);
        }

        account.DisplayName = request.DisplayName.Trim();
        account.IsDefault = request.IsDefault;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new MonitoredAccountDto(account.Id, account.DisplayName, account.IsDefault);
    }

    public async Task<MonitoredChannelDto?> UpdateChannelAsync(Guid channelId, SaveChannelSettingsRequest request, CancellationToken cancellationToken = default)
    {
        var channel = await dbContext.MonitoredChannels.FirstOrDefaultAsync(x => x.Id == channelId, cancellationToken);
        if (channel is null)
        {
            return null;
        }

        if (request.IsDefault)
        {
            await ClearDefaultChannelsAsync(cancellationToken);
        }

        channel.DisplayName = request.DisplayName.Trim();
        channel.Url = NormalizeOptional(request.Url);
        channel.IsDefault = request.IsDefault;
        channel.IsEnabled = request.IsEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(channel);
    }

    public async Task<IReadOnlyList<ProviderConnectionDto>> GetEnabledConnectionsAsync(ProviderKind? provider = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProviderConnections.AsNoTracking().Where(x => x.IsEnabled);

        if (provider.HasValue)
        {
            query = query.Where(x => x.Provider == provider.Value);
        }

        return await query
            .Select(x => new ProviderConnectionDto(
                x.Id,
                x.MonitoredChannelId,
                x.Provider,
                x.ExternalChannelId,
                x.ExternalStreamId,
                x.DisplayName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderConnectionConfigDto>> GetProviderConnectionsAsync(Guid? channelId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ProviderConnections.AsNoTracking();
        if (channelId.HasValue)
        {
            query = query.Where(x => x.MonitoredChannelId == channelId.Value);
        }

        return await query
            .OrderBy(x => x.Provider)
            .ThenBy(x => x.DisplayName)
            .Select(x => new ProviderConnectionConfigDto(
                x.Id,
                x.MonitoredChannelId,
                x.Provider,
                x.ExternalChannelId,
                x.ExternalStreamId,
                x.DisplayName,
                x.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProviderConnectionConfigDto> CreateProviderConnectionAsync(SaveProviderConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var connection = new ProviderConnection
        {
            MonitoredChannelId = request.MonitoredChannelId,
            Provider = request.Provider,
            ExternalChannelId = request.ExternalChannelId.Trim(),
            ExternalStreamId = NormalizeOptional(request.ExternalStreamId),
            DisplayName = NormalizeOptional(request.DisplayName),
            IsEnabled = request.IsEnabled,
            CreatedAt = clock.UtcNow
        };

        dbContext.ProviderConnections.Add(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(connection);
    }

    public async Task<ProviderConnectionConfigDto?> UpdateProviderConnectionAsync(Guid providerConnectionId, SaveProviderConnectionRequest request, CancellationToken cancellationToken = default)
    {
        var connection = await dbContext.ProviderConnections.FirstOrDefaultAsync(x => x.Id == providerConnectionId, cancellationToken);
        if (connection is null)
        {
            return null;
        }

        connection.MonitoredChannelId = request.MonitoredChannelId;
        connection.Provider = request.Provider;
        connection.ExternalChannelId = request.ExternalChannelId.Trim();
        connection.ExternalStreamId = NormalizeOptional(request.ExternalStreamId);
        connection.DisplayName = NormalizeOptional(request.DisplayName);
        connection.IsEnabled = request.IsEnabled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(connection);
    }

    public async Task<bool> DeleteProviderConnectionAsync(Guid providerConnectionId, CancellationToken cancellationToken = default)
    {
        var connection = await dbContext.ProviderConnections.FirstOrDefaultAsync(x => x.Id == providerConnectionId, cancellationToken);
        if (connection is null)
        {
            return false;
        }

        dbContext.ProviderConnections.Remove(connection);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<WidgetConfigurationDto>> GetWidgetConfigurationsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.WidgetConfigurations
            .AsNoTracking()
            .OrderBy(x => x.WidgetKey)
            .Select(x => new WidgetConfigurationDto(x.Id, x.WidgetKey, x.WidgetType, x.Theme, x.SettingsJson, x.UpdatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<WidgetConfigurationDto> UpsertWidgetConfigurationAsync(SaveWidgetConfigurationRequest request, CancellationToken cancellationToken = default)
    {
        var widget = await dbContext.WidgetConfigurations.FirstOrDefaultAsync(x => x.WidgetKey == request.WidgetKey, cancellationToken);
        if (widget is null)
        {
            widget = new WidgetConfiguration
            {
                WidgetKey = request.WidgetKey.Trim()
            };
            dbContext.WidgetConfigurations.Add(widget);
        }

        widget.WidgetType = request.WidgetType.Trim();
        widget.Theme = request.Theme.Trim();
        widget.SettingsJson = request.SettingsJson;
        widget.UpdatedAt = clock.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return new WidgetConfigurationDto(widget.Id, widget.WidgetKey, widget.WidgetType, widget.Theme, widget.SettingsJson, widget.UpdatedAt);
    }

    public async Task<IReadOnlyList<AlertRuleDto>> GetAlertRulesAsync(Guid? monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AlertRules.AsNoTracking();
        if (monitoredChannelId.HasValue)
        {
            query = query.Where(x => x.MonitoredChannelId == monitoredChannelId.Value);
        }

        return await query
            .OrderBy(x => x.MonitoredChannelId)
            .ThenBy(x => x.EventType)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<AlertRuleDto?> GetAlertRuleAsync(Guid monitoredChannelId, StreamEventType eventType, CancellationToken cancellationToken = default)
    {
        return await dbContext.AlertRules
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.EventType == eventType)
            .Select(x => ToDto(x))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AlertRuleDto> UpsertAlertRuleAsync(SaveAlertRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await dbContext.AlertRules.FirstOrDefaultAsync(
            x => x.MonitoredChannelId == request.MonitoredChannelId && x.EventType == request.EventType,
            cancellationToken);
        if (rule is null)
        {
            rule = new AlertRule
            {
                MonitoredChannelId = request.MonitoredChannelId,
                EventType = request.EventType
            };
            dbContext.AlertRules.Add(rule);
        }

        rule.Enabled = request.Enabled;
        rule.MinimumAmount = request.MinimumAmount;
        rule.DurationSeconds = Math.Clamp(request.DurationSeconds, 1, 60);
        rule.VisualStyle = NormalizeOptional(request.VisualStyle) ?? "default";
        rule.TitleTemplate = NormalizeOptional(request.TitleTemplate);
        rule.MessageTemplate = NormalizeOptional(request.MessageTemplate);
        rule.MediaUrl = NormalizeOptional(request.MediaUrl);
        rule.SoundUrl = NormalizeOptional(request.SoundUrl);
        rule.UpdatedAtUtc = clock.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(rule);
    }

    public async Task<IReadOnlyList<ConnectedAccountDto>> GetConnectedAccountsAsync(CancellationToken cancellationToken = default)
    {
        var accounts = await dbContext.MonitoredAccounts
            .AsNoTracking()
            .Include(x => x.Channels)
            .Include(x => x.ProviderCredentials)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);

        return accounts.Select(account =>
        {
            var credential = account.ProviderCredentials.FirstOrDefault(x => x.Provider == ProviderKind.YouTube);
            var isLoggedIn = credential is not null
                && credential.DisconnectedAt is null
                && !string.IsNullOrWhiteSpace(credential.EncryptedAccessToken);
            var isExpired = credential?.AccessTokenExpiresAt is not null && credential.AccessTokenExpiresAt <= clock.UtcNow;
            return new ConnectedAccountDto(
                account.Id,
                account.DisplayName,
                ProviderKind.YouTube,
                credential?.ExternalAccountId,
                credential?.Email,
                account.Channels.Count,
                isLoggedIn,
                !string.IsNullOrWhiteSpace(credential?.EncryptedRefreshToken),
                credential?.AccessTokenExpiresAt,
                isExpired,
                credential?.LastRefreshedAt,
                credential?.DisconnectedAt,
                credential?.Scopes ?? string.Empty);
        }).ToList();
    }

    public async Task<StoredProviderCredentialDto?> GetYouTubeCredentialAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.ProviderCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonitoredAccountId == accountId && x.Provider == ProviderKind.YouTube, cancellationToken);

        return credential is null ? null : ToDto(credential);
    }

    public async Task<StoredProviderCredentialDto?> GetYouTubeCredentialForChannelAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var accountId = await dbContext.MonitoredChannels
            .AsNoTracking()
            .Where(x => x.Id == monitoredChannelId)
            .Select(x => x.MonitoredAccountId)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountId == Guid.Empty)
        {
            return null;
        }

        var credential = await dbContext.ProviderCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonitoredAccountId == accountId && x.Provider == ProviderKind.YouTube, cancellationToken);

        return credential is null ? null : ToDto(credential);
    }

    public async Task<Guid> UpsertYouTubeAccountAsync(UpsertYouTubeAccountRequest request, CancellationToken cancellationToken = default)
    {
        var account = request.ExistingAccountId.HasValue
            ? await dbContext.MonitoredAccounts.FirstOrDefaultAsync(x => x.Id == request.ExistingAccountId.Value, cancellationToken)
            : await dbContext.ProviderCredentials
                .Where(x => x.Provider == ProviderKind.YouTube && x.ExternalAccountId == request.UserInfo.ExternalAccountId)
                .Select(x => x.MonitoredAccount)
                .FirstOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            account = new MonitoredAccount
            {
                DisplayName = request.UserInfo.DisplayName ?? request.UserInfo.Email ?? "YouTube account",
                IsDefault = !await dbContext.MonitoredAccounts.AnyAsync(cancellationToken),
                CreatedAt = clock.UtcNow
            };
            dbContext.MonitoredAccounts.Add(account);
        }
        else
        {
            account.DisplayName = request.UserInfo.DisplayName ?? request.UserInfo.Email ?? account.DisplayName;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var credential = await dbContext.ProviderCredentials
            .FirstOrDefaultAsync(x => x.MonitoredAccountId == account.Id && x.Provider == ProviderKind.YouTube, cancellationToken);
        if (credential is null)
        {
            credential = new ProviderCredential
            {
                MonitoredAccountId = account.Id,
                Provider = ProviderKind.YouTube,
                ConnectedAt = clock.UtcNow
            };
            dbContext.ProviderCredentials.Add(credential);
        }

        credential.ExternalAccountId = request.UserInfo.ExternalAccountId;
        credential.Email = request.UserInfo.Email;
        credential.DisplayName = request.UserInfo.DisplayName;
        credential.EncryptedAccessToken = request.EncryptedAccessToken;
        credential.EncryptedRefreshToken = request.EncryptedRefreshToken ?? credential.EncryptedRefreshToken;
        credential.AccessTokenExpiresAt = request.AccessTokenExpiresAt;
        credential.TokenType = request.TokenType;
        credential.Scopes = request.Scopes;
        credential.LastRefreshedAt = clock.UtcNow;
        credential.DisconnectedAt = null;

        foreach (var channelInfo in request.Channels)
        {
            var channel = await dbContext.MonitoredChannels
                .FirstOrDefaultAsync(x => x.Provider == ProviderKind.YouTube && x.ExternalChannelId == channelInfo.ChannelId, cancellationToken);
            if (channel is null)
            {
                channel = new MonitoredChannel
                {
                    MonitoredAccountId = account.Id,
                    Provider = ProviderKind.YouTube,
                    ExternalChannelId = channelInfo.ChannelId,
                    CreatedAt = clock.UtcNow
                };
                dbContext.MonitoredChannels.Add(channel);
            }

            channel.MonitoredAccountId = account.Id;
            channel.DisplayName = channelInfo.DisplayName;
            channel.Url = channelInfo.Url;
            channel.IsEnabled = true;
            channel.IsDefault = !await dbContext.MonitoredChannels.AnyAsync(x => x.IsDefault, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var channelInfo in request.Channels)
        {
            var channel = await dbContext.MonitoredChannels
                .FirstAsync(x => x.Provider == ProviderKind.YouTube && x.ExternalChannelId == channelInfo.ChannelId, cancellationToken);
            var connection = await dbContext.ProviderConnections
                .FirstOrDefaultAsync(x => x.MonitoredChannelId == channel.Id && x.Provider == ProviderKind.YouTube && x.ExternalChannelId == channelInfo.ChannelId, cancellationToken);
            if (connection is null)
            {
                dbContext.ProviderConnections.Add(new ProviderConnection
                {
                    MonitoredChannelId = channel.Id,
                    Provider = ProviderKind.YouTube,
                    ExternalChannelId = channelInfo.ChannelId,
                    DisplayName = channelInfo.DisplayName,
                    IsEnabled = true,
                    CreatedAt = clock.UtcNow
                });
            }
            else
            {
                connection.DisplayName = channelInfo.DisplayName;
                connection.IsEnabled = true;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return account.Id;
    }

    public async Task UpdateYouTubeCredentialTokensAsync(
        Guid accountId,
        string encryptedAccessToken,
        string? encryptedRefreshToken,
        DateTimeOffset accessTokenExpiresAt,
        string? tokenType,
        string scopes,
        CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.ProviderCredentials
            .FirstAsync(x => x.MonitoredAccountId == accountId && x.Provider == ProviderKind.YouTube, cancellationToken);

        credential.EncryptedAccessToken = encryptedAccessToken;
        credential.EncryptedRefreshToken = encryptedRefreshToken ?? credential.EncryptedRefreshToken;
        credential.AccessTokenExpiresAt = accessTokenExpiresAt;
        credential.TokenType = tokenType;
        credential.Scopes = scopes;
        credential.LastRefreshedAt = clock.UtcNow;
        credential.DisconnectedAt = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DisconnectYouTubeCredentialAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var credential = await dbContext.ProviderCredentials
            .FirstOrDefaultAsync(x => x.MonitoredAccountId == accountId && x.Provider == ProviderKind.YouTube, cancellationToken);
        if (credential is null)
        {
            return false;
        }

        credential.EncryptedAccessToken = null;
        credential.EncryptedRefreshToken = null;
        credential.DisconnectedAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> EventExistsAsync(Guid monitoredChannelId, ProviderKind provider, string externalEventId, CancellationToken cancellationToken = default)
    {
        return await dbContext.StreamEvents.AnyAsync(
            x => x.MonitoredChannelId == monitoredChannelId
                && x.Provider == provider
                && x.ExternalEventId == externalEventId,
            cancellationToken);
    }

    public async Task AddEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
    {
        await UpsertEventAsync(streamEvent, cancellationToken);
    }

    public async Task<IngestedEventResult> UpsertEventAsync(StreamEvent streamEvent, CancellationToken cancellationToken = default)
    {
        var existing = await dbContext.StreamEvents
            .FirstOrDefaultAsync(x => x.MonitoredChannelId == streamEvent.MonitoredChannelId && x.IdentityKey == streamEvent.IdentityKey, cancellationToken);

        if (existing is not null)
        {
            var unchanged = existing.PayloadHash == streamEvent.PayloadHash;
            UpdateEvent(existing, streamEvent);
            existing.LastSeenAt = clock.UtcNow;
            if (!unchanged)
            {
                await UpsertTipForEventIfNeededAsync(existing, addMetricSnapshot: false, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return new IngestedEventResult(existing.Id, Stored: !unchanged, Duplicate: unchanged);
        }

        dbContext.StreamEvents.Add(streamEvent);
        await UpsertTipForEventIfNeededAsync(streamEvent, addMetricSnapshot: true, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new IngestedEventResult(streamEvent.Id, Stored: true, Duplicate: false);
    }

    public async Task<IReadOnlyList<RecentEventDto>> GetRecentEventsAsync(
        Guid? monitoredChannelId,
        ProviderKind? provider,
        StreamEventType? eventType,
        int limit,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        Guid? audienceMemberId = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 10_000);
        var query = dbContext.StreamEvents.AsNoTracking();

        if (monitoredChannelId.HasValue)
        {
            query = query.Where(x => x.MonitoredChannelId == monitoredChannelId.Value);
        }

        if (provider.HasValue)
        {
            query = query.Where(x => x.Provider == provider.Value);
        }

        if (eventType.HasValue)
        {
            query = query.Where(x => x.EventType == eventType.Value);
        }

        if (providerResourceId.HasValue)
        {
            query = query.Where(x => x.ProviderResourceId == providerResourceId.Value);
        }

        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        if (audienceMemberId.HasValue)
        {
            query = query.Where(x => x.AudienceMemberId == audienceMemberId.Value);
        }

        var events = await query.ToListAsync(cancellationToken);

        return events
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .Select(x => new RecentEventDto(
                x.Id,
                x.MonitoredChannelId,
                x.StreamSessionId,
                x.AudienceMemberId,
                x.ProviderResourceId,
                x.Provider,
                x.EventType,
                x.ActorName,
                x.Title,
                x.Message,
                x.Value,
                x.Unit,
                x.OccurredAt))
            .ToList();
    }

    public async Task<Tip?> GetTipByProviderExternalIdAsync(ProviderKind provider, string externalTipId, CancellationToken cancellationToken = default)
        => await dbContext.Tips
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ExternalTipId == externalTipId, cancellationToken);

    public async Task<Tip?> GetTipByIdAsync(Guid tipId, CancellationToken cancellationToken = default)
        => await dbContext.Tips.FirstOrDefaultAsync(x => x.Id == tipId, cancellationToken);

    public async Task<Tip> UpsertTipDetailsAsync(Guid streamEventId, ProviderTipRecord record, Guid? refundedTipId, CancellationToken cancellationToken = default)
    {
        var tip = await dbContext.Tips.FirstOrDefaultAsync(x => x.StreamEventId == streamEventId, cancellationToken);
        if (tip is null)
        {
            tip = new Tip
            {
                StreamEventId = streamEventId,
                MonitoredChannelId = record.MonitoredChannelId,
                StoredAt = clock.UtcNow
            };
            dbContext.Tips.Add(tip);
        }

        var feeLines = record.FeeLines.Count == 0
            ? null
            : JsonSerializer.Serialize(record.FeeLines);
        tip.MonitoredChannelId = record.MonitoredChannelId;
        tip.StreamSessionId = record.StreamSessionId;
        tip.Provider = record.Provider;
        tip.TipKind = record.TipKind;
        tip.Status = record.Status;
        tip.Source = record.Source;
        tip.PaymentMethod = record.PaymentMethod;
        tip.ExternalTipId = NormalizeOptional(record.ExternalTipId);
        tip.IsSyntheticExternalId = record.IsSyntheticExternalId;
        tip.RefundedTipId = refundedTipId;
        tip.ActorName = NormalizeOptional(record.ActorName);
        tip.ActorExternalId = NormalizeOptional(record.ActorExternalId);
        tip.Amount = record.Amount;
        tip.GrossAmount = record.GrossAmount ?? record.Amount;
        tip.KnownNetAmount = record.KnownNetAmount;
        tip.EstimatedNetAmount = record.EstimatedNetAmount;
        tip.PlatformFee = record.FeeLines.Where(x => x.Kind == FeeKind.Platform).Sum(x => (decimal?)x.Amount);
        tip.ProcessorFee = record.FeeLines.Where(x => x.Kind == FeeKind.Processor).Sum(x => (decimal?)x.Amount);
        tip.PayoutFee = record.FeeLines.Where(x => x.Kind == FeeKind.Payout).Sum(x => (decimal?)x.Amount);
        tip.Currency = record.Currency;
        tip.FeeLinesJson = feeLines;
        tip.CampaignExternalId = NormalizeOptional(record.CampaignExternalId);
        tip.PayoutExternalId = NormalizeOptional(record.PayoutExternalId);
        tip.SupportExternalId = NormalizeOptional(record.SupportExternalId);
        tip.PatronTierName = NormalizeOptional(record.PatronTierName);
        tip.Message = NormalizeOptional(record.Message);
        tip.OccurredAt = record.OccurredAt;
        tip.LastSeenAt = clock.UtcNow;
        tip.ContextJson = record.ContextJson;
        await dbContext.SaveChangesAsync(cancellationToken);
        return tip;
    }

    public async Task<IReadOnlyList<Tip>> QueryTipsAsync(RevenueSummaryQuery query, CancellationToken cancellationToken = default)
    {
        var tips = dbContext.Tips.AsNoTracking().AsQueryable();
        if (query.MonitoredChannelId.HasValue)
        {
            tips = tips.Where(x => x.MonitoredChannelId == query.MonitoredChannelId.Value);
        }

        if (query.Provider.HasValue)
        {
            tips = tips.Where(x => x.Provider == query.Provider.Value);
        }

        if (query.StreamSessionId.HasValue)
        {
            tips = tips.Where(x => x.StreamSessionId == query.StreamSessionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.CampaignExternalId))
        {
            tips = tips.Where(x => x.CampaignExternalId == query.CampaignExternalId);
        }

        if (!string.IsNullOrWhiteSpace(query.Currency))
        {
            tips = tips.Where(x => x.Currency == query.Currency);
        }

        if (query.Since.HasValue)
        {
            tips = tips.Where(x => x.OccurredAt >= query.Since.Value);
        }

        if (query.Until.HasValue)
        {
            tips = tips.Where(x => x.OccurredAt < query.Until.Value);
        }

        return await tips.OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AudienceRelationshipPeriod>> QueryPaidRelationshipsAsync(Guid? monitoredChannelId, DateTimeOffset until, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Where(x => x.RelationshipKind == AudienceRelationshipKind.Paid
                && x.StartedAt < until
                && x.EndedAt == null);
        if (monitoredChannelId.HasValue)
        {
            query = query.Where(x => x.MonitoredChannelId == monitoredChannelId.Value);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<StreamAlertDto> AddAlertAsync(StreamAlert alert, CancellationToken cancellationToken = default)
    {
        dbContext.StreamAlerts.Add(alert);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await QueryAlerts()
            .Where(x => x.Id == alert.Id)
            .Select(x => ToDto(x))
            .FirstAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StreamAlertDto>> GetActiveAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var query = QueryAlerts()
            .Where(x => x.MonitoredChannelId == monitoredChannelId
                && x.DisplayFromUtc <= now
                && x.DisplayUntilUtc > now
                && x.AcknowledgedAtUtc == null);
        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        return await query
            .OrderBy(x => x.DisplayFromUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StreamAlertDto>> GetWidgetCandidateAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken = default)
    {
        var query = QueryAlerts()
            .Where(x => x.MonitoredChannelId == monitoredChannelId
                && x.DisplayFromUtc <= toUtc
                && x.DisplayUntilUtc >= fromUtc
                && x.AcknowledgedAtUtc == null);
        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        return await query
            .OrderBy(x => x.DisplayFromUtc)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StreamAlertDto>> GetRecentAlertsAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var query = QueryAlerts().Where(x => x.MonitoredChannelId == monitoredChannelId);
        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        return await query
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(limit)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AcknowledgeAlertAsync(Guid monitoredChannelId, Guid alertId, DateTimeOffset acknowledgedAtUtc, CancellationToken cancellationToken = default)
    {
        var alert = await dbContext.StreamAlerts.FirstOrDefaultAsync(x => x.MonitoredChannelId == monitoredChannelId && x.Id == alertId, cancellationToken);
        if (alert is null)
        {
            return false;
        }

        alert.AcknowledgedAtUtc = acknowledgedAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<StreamEventAlertTraceDto>> GetEventAlertTraceAsync(Guid monitoredChannelId, Guid? streamSessionId, int limit, CancellationToken cancellationToken = default)
        => await GetEventAlertTraceAsync(monitoredChannelId, streamSessionId, null, limit, cancellationToken);

    public async Task<IReadOnlyList<StreamEventAlertTraceDto>> GetEventAlertTraceAsync(Guid monitoredChannelId, Guid? streamSessionId, Guid? providerResourceId, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var query = dbContext.StreamEvents
            .AsNoTracking()
            .Include(x => x.Alerts)
            .Where(x => x.MonitoredChannelId == monitoredChannelId);
        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        if (providerResourceId.HasValue)
        {
            query = query.Where(x => x.ProviderResourceId == providerResourceId.Value);
        }

        var events = await query
            .OrderByDescending(x => x.OccurredAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return events.Select(x =>
        {
            var alert = x.Alerts.OrderByDescending(a => a.CreatedAtUtc).FirstOrDefault();
            var status = x.StreamSessionId is null
                ? "Outside stream"
                : alert is not null
                    ? "Alert generated"
                    : "No alert";
            return new StreamEventAlertTraceDto(
                x.Id,
                x.MonitoredChannelId,
                x.StreamSessionId,
                x.Provider,
                x.EventType,
                x.ActorName,
                x.Title,
                x.Message,
                x.Value,
                x.Unit,
                x.OccurredAt,
                alert?.Id,
                status);
        }).ToList();
    }

    public async Task AddMetricSnapshotAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        dbContext.MetricSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AddMetricSnapshotIfChangedAsync(MetricSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var latest = await dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == snapshot.MonitoredChannelId
                && x.Provider == snapshot.Provider
                && x.Metric == snapshot.Metric
                && x.StreamSessionId == snapshot.StreamSessionId
                && x.ProviderConnectionId == snapshot.ProviderConnectionId
                && x.ProviderResourceId == snapshot.ProviderResourceId
                && x.Unit == snapshot.Unit)
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is not null && latest.Value == snapshot.Value)
        {
            return false;
        }

        dbContext.MetricSnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<MetricSnapshot?> GetLatestMetricAsync(Guid monitoredChannelId, MetricKind metric, CancellationToken cancellationToken = default)
    {
        var metrics = await dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.Metric == metric)
            .ToListAsync(cancellationToken);

        return metrics.OrderByDescending(x => x.CapturedAt).FirstOrDefault();
    }

    public async Task<MetricSnapshot?> GetLatestMetricAsync(
        Guid monitoredChannelId,
        MetricKind metric,
        Guid? providerResourceId,
        Guid? streamSessionId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.Metric == metric);

        if (providerResourceId.HasValue)
        {
            query = query.Where(x => x.ProviderResourceId == providerResourceId.Value);
        }

        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        return await query
            .OrderByDescending(x => x.CapturedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MetricSnapshot>> GetMetricsAsync(
        Guid monitoredChannelId,
        DateTimeOffset from,
        DateTimeOffset to,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.MetricSnapshots
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId);

        if (providerResourceId.HasValue)
        {
            query = query.Where(x => x.ProviderResourceId == providerResourceId.Value);
        }

        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        var metrics = await query.ToListAsync(cancellationToken);

        return metrics
            .Where(x => x.CapturedAt >= from && x.CapturedAt <= to)
            .OrderBy(x => x.CapturedAt)
            .ToList();
    }

    public async Task<ProviderResourceDto> UpsertResourceAsync(ProviderResourceUpsert resource, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ProviderResources
            .FirstOrDefaultAsync(x => x.MonitoredChannelId == resource.MonitoredChannelId
                && x.Provider == resource.Provider
                && x.ExternalResourceId == resource.ExternalResourceId, cancellationToken);

        if (entity is null)
        {
            entity = new ProviderResource
            {
                MonitoredChannelId = resource.MonitoredChannelId,
                Provider = resource.Provider,
                ResourceKind = resource.ResourceKind,
                ExternalResourceId = resource.ExternalResourceId.Trim()
            };
            dbContext.ProviderResources.Add(entity);
        }

        var patchResult = resourcePatchService.Apply(entity, resource, clock.UtcNow);
        entity.RawPayloadJson = resource.RawPayloadJson;
        entity.LastSyncedAt = clock.UtcNow;
        entity.PatchHistoryJson = patchResult.HistoryJson;

        if (patchResult.Fields.Count > 0)
        {
            logger?.LogInformation(
                "Patched provider resource {ResourceId} {Provider} {ExternalResourceId} with fields {Fields}",
                entity.Id,
                resource.Provider,
                resource.ExternalResourceId,
                string.Join(", ", patchResult.Fields.Select(x => x.Field)));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<ProviderResourceDto?> GetResourceAsync(Guid monitoredChannelId, Guid providerResourceId, CancellationToken cancellationToken = default)
    {
        var resource = await dbContext.ProviderResources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonitoredChannelId == monitoredChannelId && x.Id == providerResourceId, cancellationToken);
        return resource is null ? null : ToDto(resource);
    }

    public async Task<ProviderResourceDto?> GetResourceByExternalIdAsync(Guid monitoredChannelId, ProviderKind provider, string externalResourceId, CancellationToken cancellationToken = default)
    {
        var resource = await dbContext.ProviderResources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MonitoredChannelId == monitoredChannelId
                && x.Provider == provider
                && x.ExternalResourceId == externalResourceId, cancellationToken);
        return resource is null ? null : ToDto(resource);
    }

    public async Task<IReadOnlyList<ProviderResourceDto>> GetRecentResourcesAsync(
        Guid monitoredChannelId,
        ProviderResourceKind? resourceKind,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var query = dbContext.ProviderResources
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId);

        if (resourceKind.HasValue)
        {
            var kindName = resourceKind.Value.ToString();
            query = query.Where(x => x.ResourceKind == resourceKind.Value
                || (x.ObservedKindsJson != null && x.ObservedKindsJson.Contains(kindName)));
        }

        var resources = await query.ToListAsync(cancellationToken);
        return resources
            .OrderByDescending(x => x.PublishedAt ?? x.ActualStartAt ?? x.ScheduledStartAt ?? x.LastSyncedAt)
            .Take(limit)
            .Select(ToDto)
            .ToList();
    }

    public async Task<IReadOnlyList<ProviderResourceDto>> GetUpcomingResourcesAsync(Guid monitoredChannelId, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var now = clock.UtcNow;
        var resources = await dbContext.ProviderResources
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId
                && x.ResourceKind == ProviderResourceKind.LiveBroadcast
                && x.ScheduledStartAt != null
                && x.ScheduledStartAt >= now)
            .OrderBy(x => x.ScheduledStartAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return resources.Select(ToDto).ToList();
    }

    public async Task<StreamSessionDto?> GetCurrentStreamAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.StreamSessions
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.IsActive)
            .ToListAsync(cancellationToken);

        return sessions
            .OrderByDescending(x => x.StartedAt)
            .Select(ToDto)
            .FirstOrDefault();
    }

    public async Task<StreamSession> GetOrCreateCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.StreamSessions
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.IsActive)
            .ToListAsync(cancellationToken);
        var current = sessions.OrderByDescending(x => x.StartedAt).FirstOrDefault();

        if (current is not null)
        {
            return current;
        }

        current = new StreamSession
        {
            MonitoredChannelId = monitoredChannelId,
            Provider = ProviderKind.YouTube,
            ExternalSessionId = $"manual-current-{monitoredChannelId:N}",
            Title = "Current stream",
            IsActive = true,
            StartedAt = clock.UtcNow,
            LastSyncedAt = clock.UtcNow
        };

        dbContext.StreamSessions.Add(current);
        await dbContext.SaveChangesAsync(cancellationToken);
        return current;
    }

    public Task<StreamSessionDto?> GetCurrentSessionAsync(Guid monitoredChannelId, CancellationToken cancellationToken = default)
        => GetCurrentStreamAsync(monitoredChannelId, cancellationToken);

    public async Task<StreamSessionDto?> GetSessionByProviderResourceAsync(Guid monitoredChannelId, Guid providerResourceId, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.StreamSessions
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.ProviderResourceId == providerResourceId)
            .ToListAsync(cancellationToken);

        return sessions
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(x => x.ActualStartAt ?? x.StartedAt)
            .Select(ToDto)
            .FirstOrDefault();
    }

    public async Task<StreamSessionDto> UpsertSessionAsync(ProviderStreamSession session, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.StreamSessions
            .FirstOrDefaultAsync(x => x.MonitoredChannelId == session.MonitoredChannelId
                && x.Provider == session.Provider
                && x.ExternalSessionId == session.ExternalSessionId, cancellationToken);

        if (entity is null)
        {
            entity = new StreamSession
            {
                MonitoredChannelId = session.MonitoredChannelId,
                Provider = session.Provider,
                ExternalSessionId = session.ExternalSessionId.Trim(),
                StartedAt = session.ActualStartAt ?? session.ScheduledStartAt ?? clock.UtcNow
            };
            dbContext.StreamSessions.Add(entity);
        }

        entity.Title = string.IsNullOrWhiteSpace(session.Title) ? entity.Title : session.Title.Trim();
        entity.ExternalStreamId = NormalizeOptional(session.ExternalStreamId);
        entity.ExternalLiveChatId = NormalizeOptional(session.ExternalLiveChatId);
        entity.ProviderResourceId = session.ProviderResourceId;
        entity.ScheduledStartAt = session.ScheduledStartAt;
        entity.ActualStartAt = session.ActualStartAt;
        entity.ActualEndAt = session.ActualEndAt;
        entity.Status = NormalizeOptional(session.Status);
        entity.PayloadSummaryJson = session.PayloadSummaryJson;
        entity.LastSyncedAt = clock.UtcNow;
        entity.IsActive = IsProviderSessionActive(session.Status, session.ActualEndAt);
        entity.EndedAt = entity.IsActive ? null : session.ActualEndAt ?? entity.EndedAt ?? clock.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task EndMissingActiveSessionsAsync(Guid monitoredChannelId, ProviderKind provider, IReadOnlySet<string> activeExternalSessionIds, CancellationToken cancellationToken = default)
    {
        var sessions = await dbContext.StreamSessions
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.Provider == provider && x.IsActive)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions.Where(x => !activeExternalSessionIds.Contains(x.ExternalSessionId)))
        {
            session.IsActive = false;
            session.EndedAt ??= clock.UtcNow;
            session.ActualEndAt ??= session.EndedAt;
            session.LastSyncedAt = clock.UtcNow;
            session.Status ??= "ended";
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProviderMessageDto> UpsertMessageAsync(ProviderMessageUpsert message, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ProviderMessages
            .FirstOrDefaultAsync(x => x.MonitoredChannelId == message.MonitoredChannelId && x.IdentityKey == message.IdentityKey, cancellationToken);

        if (entity is null)
        {
            entity = new ProviderMessage
            {
                MonitoredChannelId = message.MonitoredChannelId,
                IdentityKey = !string.IsNullOrWhiteSpace(message.IdentityKey)
                    ? message.IdentityKey
                    : $"{message.Provider}:{message.Source}:{message.ExternalMessageId ?? message.PublishedAt.ToUniversalTime().ToString("O")}"
            };
            dbContext.ProviderMessages.Add(entity);
        }

        entity.StreamSessionId = message.StreamSessionId;
        entity.ProviderResourceId = message.ProviderResourceId;
        entity.Provider = message.Provider;
        entity.Source = message.Source;
        entity.ExternalMessageId = NormalizeOptional(message.ExternalMessageId);
        entity.AuthorExternalId = NormalizeOptional(message.AuthorExternalId);
        entity.AuthorDisplayName = NormalizeOptional(message.AuthorDisplayName);
        entity.AuthorProfileImageUrl = NormalizeOptional(message.AuthorProfileImageUrl);
        entity.MessageText = NormalizeOptional(message.MessageText);
        entity.PublishedAt = message.PublishedAt;
        entity.LikeCount = message.LikeCount;
        entity.IsOwner = message.IsOwner;
        entity.IsModerator = message.IsModerator;
        entity.IsVerified = message.IsVerified;
        entity.IsSponsor = message.IsSponsor;
        entity.Amount = message.Amount;
        entity.Currency = NormalizeOptional(message.Currency);
        entity.PayloadSummaryJson = message.PayloadSummaryJson;
        entity.LastSeenAt = clock.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<IReadOnlyList<ProviderMessageDto>> GetRecentMessagesAsync(
        Guid monitoredChannelId,
        MessageSource? source,
        int limit,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        Guid? audienceMemberId = null,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var query = dbContext.ProviderMessages.AsNoTracking().Where(x => x.MonitoredChannelId == monitoredChannelId);
        if (source.HasValue)
        {
            query = query.Where(x => x.Source == source.Value);
        }

        if (providerResourceId.HasValue)
        {
            query = query.Where(x => x.ProviderResourceId == providerResourceId.Value);
        }

        if (streamSessionId.HasValue)
        {
            query = query.Where(x => x.StreamSessionId == streamSessionId.Value);
        }

        if (audienceMemberId.HasValue)
        {
            var audience = await dbContext.AudienceMembers
                .AsNoTracking()
                .Where(x => x.Id == audienceMemberId.Value)
                .Select(x => new { x.Provider, x.ExternalAudienceId })
                .FirstOrDefaultAsync(cancellationToken);
            if (audience is null)
            {
                return [];
            }

            query = query.Where(x => x.Provider == audience.Provider && x.AuthorExternalId == audience.ExternalAudienceId);
        }

        return await query
            .OrderByDescending(x => x.PublishedAt)
            .Take(limit)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ProviderMessageDto>> SearchMessagesAsync(Guid monitoredChannelId, string? query, MessageSource? source, DateTimeOffset? from, DateTimeOffset? to, int limit, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var messages = dbContext.ProviderMessages.AsNoTracking().Where(x => x.MonitoredChannelId == monitoredChannelId);
        if (source.HasValue)
        {
            messages = messages.Where(x => x.Source == source.Value);
        }

        if (from.HasValue)
        {
            messages = messages.Where(x => x.PublishedAt >= from.Value);
        }

        if (to.HasValue)
        {
            messages = messages.Where(x => x.PublishedAt <= to.Value);
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            var normalized = query.Trim();
            messages = messages.Where(x =>
                (x.MessageText != null && x.MessageText.Contains(normalized)) ||
                (x.AuthorDisplayName != null && x.AuthorDisplayName.Contains(normalized)));
        }

        return await messages
            .OrderByDescending(x => x.PublishedAt)
            .Take(limit)
            .Select(x => ToDto(x))
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetCursorAsync(Guid providerConnectionId, string cursorName, CancellationToken cancellationToken = default)
    {
        return await dbContext.ProviderCursors
            .AsNoTracking()
            .Where(x => x.ProviderConnectionId == providerConnectionId && x.CursorName == cursorName)
            .Select(x => x.CursorValue)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task SetCursorAsync(Guid providerConnectionId, string cursorName, string? cursorValue, DateTimeOffset? expiresAt = null, string? metadataJson = null, CancellationToken cancellationToken = default)
    {
        var cursor = await dbContext.ProviderCursors
            .FirstOrDefaultAsync(x => x.ProviderConnectionId == providerConnectionId && x.CursorName == cursorName, cancellationToken);

        if (cursor is null)
        {
            cursor = new ProviderCursor
            {
                ProviderConnectionId = providerConnectionId,
                CursorName = cursorName
            };
            dbContext.ProviderCursors.Add(cursor);
        }

        cursor.CursorValue = cursorValue;
        cursor.ExpiresAt = expiresAt;
        cursor.MetadataJson = metadataJson;
        cursor.UpdatedAt = clock.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<(AudienceMember AudienceMember, bool Created)> UpsertAudienceMemberAsync(ProviderKind provider, string externalAudienceId, string? displayName, string? profileUrl, CancellationToken cancellationToken = default)
    {
        var audienceMember = await dbContext.AudienceMembers
            .FirstOrDefaultAsync(x => x.Provider == provider && x.ExternalAudienceId == externalAudienceId, cancellationToken);

        if (audienceMember is null)
        {
            audienceMember = new AudienceMember
            {
                Provider = provider,
                ExternalAudienceId = externalAudienceId,
                DisplayName = displayName,
                ProfileUrl = profileUrl,
                FirstSeenAt = clock.UtcNow,
                LastSeenAt = clock.UtcNow
            };
            dbContext.AudienceMembers.Add(audienceMember);
            await dbContext.SaveChangesAsync(cancellationToken);
            return (audienceMember, Created: true);
        }

        audienceMember.DisplayName = displayName ?? audienceMember.DisplayName;
        audienceMember.ProfileUrl = profileUrl ?? audienceMember.ProfileUrl;
        audienceMember.LastSeenAt = clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return (audienceMember, Created: false);
    }

    public async Task<AudienceRelationshipPeriod?> GetLatestRelationshipPeriodAsync(Guid monitoredChannelId, Guid audienceMemberId, AudienceRelationshipKind relationshipKind, CancellationToken cancellationToken = default)
    {
        var periods = await dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId
                && x.AudienceMemberId == audienceMemberId
                && x.RelationshipKind == relationshipKind)
            .ToListAsync(cancellationToken);

        return periods.OrderByDescending(x => x.StartedAt).FirstOrDefault();
    }

    public async Task AddRelationshipPeriodAsync(AudienceRelationshipPeriod period, CancellationToken cancellationToken = default)
    {
        dbContext.AudienceRelationshipPeriods.Add(period);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRecentRelationshipsAsync(Guid monitoredChannelId, int limit, bool includeRevenue = false, CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 1000);
        var periods = await dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Include(x => x.AudienceMember)
            .Where(x => x.MonitoredChannelId == monitoredChannelId)
            .ToListAsync(cancellationToken);

        return periods
            .OrderByDescending(x => x.StartedAt)
            .Take(limit)
            .Select(x => ToDto(x, includeRevenue ? GetAudienceRevenueSync(monitoredChannelId, x.AudienceMember!) : null))
            .ToList();
    }

    public async Task<IReadOnlyList<AudienceRelationshipPeriodDto>> GetRelationshipHistoryAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default)
    {
        var periods = await dbContext.AudienceRelationshipPeriods
            .AsNoTracking()
            .Include(x => x.AudienceMember)
            .Where(x => x.MonitoredChannelId == monitoredChannelId && x.AudienceMemberId == audienceMemberId)
            .ToListAsync(cancellationToken);

        return periods
            .OrderByDescending(x => x.StartedAt)
            .Select(x => ToDto(x))
            .ToList();
    }

    public async Task<AudienceRevenueSummaryDto> GetAudienceRevenueAsync(Guid monitoredChannelId, Guid audienceMemberId, CancellationToken cancellationToken = default)
    {
        var audience = await dbContext.AudienceMembers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == audienceMemberId, cancellationToken);
        return audience is null ? new AudienceRevenueSummaryDto(null, null, []) : GetAudienceRevenueSync(monitoredChannelId, audience);
    }

    private async Task ClearDefaultAccountsAsync(CancellationToken cancellationToken)
    {
        var defaultAccounts = await dbContext.MonitoredAccounts.Where(x => x.IsDefault).ToListAsync(cancellationToken);
        foreach (var account in defaultAccounts)
        {
            account.IsDefault = false;
        }
    }

    private async Task ClearDefaultChannelsAsync(CancellationToken cancellationToken)
    {
        var defaultChannels = await dbContext.MonitoredChannels.Where(x => x.IsDefault).ToListAsync(cancellationToken);
        foreach (var channel in defaultChannels)
        {
            channel.IsDefault = false;
        }
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static List<T> ReadJsonList<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<T>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static void PatchIfChanged<T>(
        string field,
        T oldValue,
        T newValue,
        Action<T> apply,
        ICollection<ProviderResourcePatchFieldDto> patchFields)
    {
        if (EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            return;
        }

        patchFields.Add(new ProviderResourcePatchFieldDto(field, ToPatchValue(oldValue), ToPatchValue(newValue)));
        apply(newValue);
    }

    private static string? ToPatchValue<T>(T value)
        => value switch
        {
            null => null,
            DateTimeOffset date => date.ToString("O"),
            _ => value.ToString()
        };

    private static ProviderResourceKind ChoosePrimaryKind(IEnumerable<ProviderResourceKind> kinds)
        => kinds
            .OrderByDescending(x => x switch
            {
                ProviderResourceKind.LiveBroadcast => 5,
                ProviderResourceKind.Video => 4,
                ProviderResourceKind.PlaylistItem => 3,
                ProviderResourceKind.LiveStream => 2,
                ProviderResourceKind.Channel => 1,
                _ => 0
            })
            .FirstOrDefault();

    private async Task UpsertTipForEventIfNeededAsync(StreamEvent streamEvent, bool addMetricSnapshot, CancellationToken cancellationToken)
    {
        if (streamEvent.EventType != StreamEventType.Tip || !streamEvent.Value.HasValue)
        {
            return;
        }

        var currency = NormalizeOptional(streamEvent.Unit) ?? "unknown";
        var tip = await dbContext.Tips.FirstOrDefaultAsync(x => x.StreamEventId == streamEvent.Id, cancellationToken);
        if (tip is null)
        {
            tip = new Tip
            {
                MonitoredChannelId = streamEvent.MonitoredChannelId,
                StreamEventId = streamEvent.Id,
                StoredAt = clock.UtcNow
            };
            dbContext.Tips.Add(tip);
        }

        tip.StreamSessionId = streamEvent.StreamSessionId;
        tip.Provider = streamEvent.Provider;
        tip.TipKind = streamEvent.Value.Value < 0 ? TipKind.Refund : TipKind.Donation;
        tip.Status = TipStatus.Settled;
        tip.Source = TipSource.Provider;
        tip.PaymentMethod = PaymentMethod.Unknown;
        tip.ExternalTipId = NormalizeOptional(streamEvent.ExternalEventId);
        tip.ActorName = NormalizeOptional(streamEvent.ActorName);
        tip.ActorExternalId = NormalizeOptional(streamEvent.ActorExternalId);
        tip.Amount = streamEvent.Value.Value;
        tip.GrossAmount = streamEvent.Value.Value;
        tip.Currency = currency;
        tip.Message = NormalizeOptional(streamEvent.Message);
        tip.OccurredAt = streamEvent.OccurredAt;
        tip.LastSeenAt = clock.UtcNow;
        tip.ContextJson = streamEvent.ContextJson;

        if (addMetricSnapshot)
        {
            await AddTipMetricSnapshotAsync(streamEvent, currency, cancellationToken);
        }
    }

    private async Task AddTipMetricSnapshotAsync(StreamEvent streamEvent, string currency, CancellationToken cancellationToken)
    {
        var currentTipTotal = await GetLatestMetricAsync(streamEvent.MonitoredChannelId, MetricKind.TipTotal, cancellationToken);
        dbContext.MetricSnapshots.Add(new MetricSnapshot
        {
            MonitoredChannelId = streamEvent.MonitoredChannelId,
            StreamSessionId = streamEvent.StreamSessionId,
            Provider = streamEvent.Provider,
            Metric = MetricKind.TipTotal,
            SnapshotReason = SnapshotReason.ProviderEvent,
            Value = (currentTipTotal?.Value ?? 0) + streamEvent.Value!.Value,
            Unit = currency,
            CapturedAt = clock.UtcNow,
            RawPayloadJson = streamEvent.ContextJson
        });
    }

    private static void UpdateEvent(StreamEvent target, StreamEvent source)
    {
        target.StreamSessionId = source.StreamSessionId ?? target.StreamSessionId;
        target.AudienceMemberId = source.AudienceMemberId ?? target.AudienceMemberId;
        target.ProviderResourceId = source.ProviderResourceId ?? target.ProviderResourceId;
        target.ExternalEventId = NormalizeOptional(source.ExternalEventId) ?? target.ExternalEventId;
        target.PayloadHash = source.PayloadHash;
        target.ActorName = NormalizeOptional(source.ActorName) ?? target.ActorName;
        target.ActorExternalId = NormalizeOptional(source.ActorExternalId) ?? target.ActorExternalId;
        target.Title = NormalizeOptional(source.Title) ?? target.Title;
        target.Message = NormalizeOptional(source.Message) ?? target.Message;
        target.Value = source.Value ?? target.Value;
        target.Unit = NormalizeOptional(source.Unit) ?? target.Unit;
        target.OccurredAt = source.OccurredAt;
        target.RawPayloadJson = NormalizeOptional(source.RawPayloadJson) ?? target.RawPayloadJson;
        target.ContextJson = NormalizeOptional(source.ContextJson) ?? target.ContextJson;
    }

    private static bool IsProviderSessionActive(string? status, DateTimeOffset? actualEndAt)
    {
        if (actualEndAt.HasValue)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return !status.Equals("complete", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("completed", StringComparison.OrdinalIgnoreCase)
            && !status.Equals("ended", StringComparison.OrdinalIgnoreCase);
    }

    private IQueryable<StreamAlert> QueryAlerts()
        => dbContext.StreamAlerts
            .AsNoTracking()
            .Include(x => x.StreamEvent);

    private static MonitoredChannelDto ToDto(MonitoredChannel channel)
        => new(
            channel.Id,
            channel.MonitoredAccountId,
            channel.Provider,
            channel.ExternalChannelId,
            channel.DisplayName,
            channel.Url,
            channel.IsDefault,
            channel.IsEnabled);

    private static ProviderConnectionConfigDto ToDto(ProviderConnection connection)
        => new(
            connection.Id,
            connection.MonitoredChannelId,
            connection.Provider,
            connection.ExternalChannelId,
            connection.ExternalStreamId,
            connection.DisplayName,
            connection.IsEnabled);

    private ProviderResourceDto ToDto(ProviderResource resource)
        => new(
            resource.Id,
            resource.MonitoredChannelId,
            resource.Provider,
            resource.ResourceKind,
            ReadJsonList<ProviderResourceKind>(resource.ObservedKindsJson).Count == 0
                ? [resource.ResourceKind]
                : ReadJsonList<ProviderResourceKind>(resource.ObservedKindsJson),
            resource.ExternalResourceId,
            resource.Title,
            resource.Description,
            resource.Url,
            resource.ThumbnailUrl,
            resource.Status,
            resource.PublishedAt,
            resource.ScheduledStartAt,
            resource.ActualStartAt,
            resource.ActualEndAt,
            resource.DurationSeconds,
            resource.LastSyncedAt,
            resourcePatchService.ReadCompactHistory(resource.PatchHistoryJson));

    private static AlertRuleDto ToDto(AlertRule rule)
        => new(
            rule.Id,
            rule.MonitoredChannelId,
            rule.EventType,
            rule.Enabled,
            rule.MinimumAmount,
            rule.DurationSeconds,
            rule.VisualStyle,
            rule.TitleTemplate,
            rule.MessageTemplate,
            rule.MediaUrl,
            rule.SoundUrl,
            rule.UpdatedAtUtc);

    private static StreamAlertDto ToDto(StreamAlert alert)
        => new(
            alert.Id,
            alert.MonitoredChannelId,
            alert.StreamSessionId,
            alert.StreamEventId,
            alert.AlertType,
            alert.Provider,
            alert.IsSystemAlert,
            alert.Title,
            alert.Message,
            alert.ActorName,
            alert.Amount,
            alert.Currency,
            alert.VisualStyle,
            alert.MediaUrl,
            alert.SoundUrl,
            alert.DisplayFromUtc,
            alert.DisplayUntilUtc,
            alert.AcknowledgedAtUtc,
            alert.CreatedAtUtc,
            alert.StreamEvent?.EventType,
            alert.StreamEvent?.Title,
            alert.StreamEvent?.Message,
            alert.StreamEvent?.OccurredAt);

    private static StreamSessionDto ToDto(StreamSession session)
        => new(
            session.Id,
            session.MonitoredChannelId,
            session.Title,
            session.IsActive,
            session.StartedAt,
            session.EndedAt,
            session.Provider,
            session.ExternalSessionId,
            session.ExternalStreamId,
            session.ExternalLiveChatId,
            session.ProviderResourceId,
            session.ScheduledStartAt,
            session.ActualStartAt,
            session.ActualEndAt);

    private static ProviderMessageDto ToDto(ProviderMessage message)
        => new(
            message.Id,
            message.MonitoredChannelId,
            message.StreamSessionId,
            message.ProviderResourceId,
            message.Provider,
            message.Source,
            message.ExternalMessageId,
            message.IdentityKey,
            message.AuthorExternalId,
            message.AuthorDisplayName,
            message.AuthorProfileImageUrl,
            message.MessageText,
            message.PublishedAt,
            message.LikeCount,
            message.IsOwner,
            message.IsModerator,
            message.IsVerified,
            message.IsSponsor,
            message.Amount,
            message.Currency,
            message.LastSeenAt);

    private static StoredProviderCredentialDto ToDto(ProviderCredential credential)
        => new(
            credential.Id,
            credential.MonitoredAccountId,
            credential.Provider,
            credential.ExternalAccountId,
            credential.Email,
            credential.DisplayName,
            credential.EncryptedAccessToken,
            credential.EncryptedRefreshToken,
            credential.AccessTokenExpiresAt,
            credential.TokenType,
            credential.Scopes,
            credential.ConnectedAt,
            credential.LastRefreshedAt,
            credential.DisconnectedAt);

    private AudienceRevenueSummaryDto GetAudienceRevenueSync(Guid monitoredChannelId, AudienceMember audience)
    {
        var tips = dbContext.Tips
            .AsNoTracking()
            .Where(x => x.MonitoredChannelId == monitoredChannelId
                && x.Provider == audience.Provider
                && x.ActorExternalId == audience.ExternalAudienceId)
            .ToList();

        if (tips.Count == 0)
        {
            return new AudienceRevenueSummaryDto(null, null, []);
        }

        var latestCurrency = tips
            .OrderByDescending(x => x.OccurredAt)
            .Select(x => x.Currency)
            .FirstOrDefault();
        var totals = tips
            .GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(x => new AudienceCurrencyTotalDto(x.Key, x.Sum(t => t.Amount)))
            .OrderBy(x => x.Currency)
            .ToList();
        var latestTotal = latestCurrency is null
            ? null
            : totals.FirstOrDefault(x => x.Currency.Equals(latestCurrency, StringComparison.OrdinalIgnoreCase))?.Total;
        return new AudienceRevenueSummaryDto(latestTotal, latestCurrency, totals);
    }

    private static AudienceRelationshipPeriodDto ToDto(AudienceRelationshipPeriod period, AudienceRevenueSummaryDto? revenue = null)
    {
        var isPatron = period.RelationshipKind == AudienceRelationshipKind.Paid
            && period.Status == RelationshipStatus.Active
            && period.EndedAt is null;
        return new AudienceRelationshipPeriodDto(
            period.Id,
            period.MonitoredChannelId,
            period.AudienceMemberId,
            period.RelationshipKind,
            period.StartedAt,
            period.EndedAt,
            period.IsEstimated,
            period.AudienceMember?.DisplayName,
            isPatron,
            period.TierName,
            revenue?.LatestCurrencyTotal,
            revenue?.LatestCurrency,
            revenue?.CurrencyTotals,
            new[] { period.StartedAt, period.LastChargeAt, period.NextChargeAt }.Where(x => x.HasValue).Max());
    }
}
