using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class SupportIngestionService(
    IEventIngestionService eventIngestionService,
    ISupportTransactionStore supportStore,
    IAudienceIngestionService audienceIngestionService,
    IClock clock,
    IActivityPublisher? activityPublisher = null) : ISupportIngestionService
{
    private readonly IActivityPublisher _activityPublisher = activityPublisher ?? new NoOpActivityPublisher();

    public async Task<TipIngestionResult> IngestTipAsync(ProviderTipRecord record, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRecord(record);
        var externalId = normalized.ExternalTipId ?? CreateSyntheticExternalId(normalized);
        var refundedTip = !string.IsNullOrWhiteSpace(normalized.RefundedExternalTipId)
            ? await supportStore.GetTipByProviderExternalIdAsync(normalized.Provider, normalized.RefundedExternalTipId, cancellationToken)
            : null;
        var contextJson = normalized.ContextJson ?? JsonSerializer.Serialize(normalized);

        var result = await eventIngestionService.IngestAsync(new ProviderEvent(
            normalized.MonitoredChannelId,
            normalized.StreamSessionId,
            null,
            null,
            normalized.Provider,
            StreamEventType.Tip,
            externalId,
            normalized.ActorName,
            normalized.ActorExternalId,
            TitleFor(normalized),
            normalized.Message,
            normalized.Amount,
            normalized.Currency,
            normalized.OccurredAt == default ? clock.UtcNow : normalized.OccurredAt,
            contextJson,
            Value: normalized.Amount,
            Unit: normalized.Currency,
            ContextJson: contextJson), cancellationToken);

        if (result.EventId is null)
        {
            return new TipIngestionResult(null, null, Stored: false, Duplicate: true);
        }

        var tip = await supportStore.UpsertTipDetailsAsync(result.EventId.Value, normalized with
        {
            ExternalTipId = externalId,
            IsSyntheticExternalId = normalized.IsSyntheticExternalId || normalized.ExternalTipId is null,
            ContextJson = contextJson
        }, refundedTip?.Id, cancellationToken);

        if (result.Stored && !result.Duplicate)
        {
            await _activityPublisher.PublishTipCreatedAsync(new TipRealtimeDto(
                tip.Id,
                tip.MonitoredChannelId,
                tip.StreamSessionId,
                tip.Provider,
                tip.ActorName,
                tip.Amount,
                tip.Currency,
                tip.Message,
                tip.OccurredAt), cancellationToken);
        }

        return new TipIngestionResult(result.EventId, tip.Id, result.Stored, result.Duplicate);
    }

    public Task IngestPatronAsync(ProviderPatronRecord record, CancellationToken cancellationToken = default)
        => audienceIngestionService.IngestRelationshipAsync(new ProviderAudienceRelationship(
            record.MonitoredChannelId,
            record.Provider,
            record.ExternalAudienceId,
            record.DisplayName,
            record.ProfileUrl,
            AudienceRelationshipKind.Paid,
            record.StartedAt == default ? clock.UtcNow : record.StartedAt,
            IsEstimated: false,
            record.ContextJson,
            record.SupportExternalId,
            record.TierName,
            record.Status,
            record.BillingCadence,
            record.Amount,
            record.Currency,
            record.LastChargeAt,
            record.NextChargeAt,
            record.CancelledAt), cancellationToken);

    private static ProviderTipRecord NormalizeRecord(ProviderTipRecord record)
    {
        var amount = record.Amount;
        if ((record.TipKind is TipKind.Refund or TipKind.Chargeback or TipKind.PayoutFee) && amount > 0)
        {
            amount = -amount;
        }

        var gross = record.GrossAmount;
        if ((record.TipKind is TipKind.Refund or TipKind.Chargeback or TipKind.PayoutFee) && gross > 0)
        {
            gross = -gross;
        }

        return record with { Amount = amount, GrossAmount = gross ?? amount };
    }

    private static string TitleFor(ProviderTipRecord record)
        => record.TipKind switch
        {
            TipKind.Refund => "Support refund",
            TipKind.Chargeback => "Support chargeback",
            TipKind.Payout => "Support payout",
            TipKind.PayoutFee => "Support payout fee",
            TipKind.PatronPayment => "Patron payment",
            TipKind.CampaignDonation => "Campaign donation",
            _ => "Support received"
        };

    private static string CreateSyntheticExternalId(ProviderTipRecord record)
    {
        var raw = $"{record.Provider}|{record.TipKind}|{record.OccurredAt.ToUniversalTime():O}|{record.Amount}|{record.Currency}|{record.ActorExternalId}|{record.ActorName}|{record.Message}|{record.CampaignExternalId}|{record.SupportExternalId}";
        return $"synthetic:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant()[..32]}";
    }
}
