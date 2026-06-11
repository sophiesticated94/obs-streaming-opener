using System.Text.Json;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Infrastructure.Providers.Tipply;

public static class TipplyTipParser
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static IReadOnlyList<TipplyTipDto> ParseTips(string json)
    {
        var trimmed = json.TrimStart();
        if (trimmed.StartsWith("[", StringComparison.Ordinal))
        {
            return JsonSerializer.Deserialize<List<TipplyTipDto>>(json, JsonOptions) ?? [];
        }

        var envelope = JsonSerializer.Deserialize<TipplyTipsEnvelope>(json, JsonOptions);
        return envelope?.AllItems ?? [];
    }

    public static ProviderTipRecord ToProviderTip(Guid monitoredChannelId, TipplyTipDto tip, DateTimeOffset fallbackOccurredAt, string contextJson)
    {
        var externalId = FirstNonEmpty(tip.Id, tip.TipId, tip.TransactionId);
        var actorName = FirstNonEmpty(tip.DisplayName, tip.Username, tip.Nick, tip.Name);
        var amount = tip.Amount ?? tip.Value ?? 0;
        var currency = FirstNonEmpty(tip.CurrencyCode, tip.Currency) ?? "PLN";
        return new ProviderTipRecord(
            monitoredChannelId,
            null,
            ProviderKind.Tipply,
            TipKind.Donation,
            ParseStatus(tip.Status),
            TipSource.Browser,
            ParsePaymentMethod(tip.PaymentMethod),
            externalId,
            null,
            actorName,
            null,
            amount,
            currency.ToUpperInvariant(),
            amount,
            null,
            null,
            [],
            FirstNonEmpty(tip.Message, tip.Content, tip.Comment),
            tip.PaidAt ?? tip.CreatedAt ?? tip.Date ?? fallbackOccurredAt,
            null,
            null,
            null,
            null,
            contextJson,
            IsSyntheticExternalId: string.IsNullOrWhiteSpace(externalId));
    }

    private static TipStatus ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TipStatus.Settled;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "pending" or "new" => TipStatus.Pending,
            "failed" or "rejected" => TipStatus.Failed,
            "refunded" => TipStatus.Refunded,
            "chargeback" or "reversed" => TipStatus.Reversed,
            _ => TipStatus.Settled
        };
    }

    private static PaymentMethod ParsePaymentMethod(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PaymentMethod.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "blik" => PaymentMethod.Blik,
            "paypal" => PaymentMethod.PayPal,
            "payu" => PaymentMethod.PayU,
            "stripe" => PaymentMethod.Stripe,
            "card" or "credit_card" or "payment_card" => PaymentMethod.Card,
            "bank" or "transfer" or "bank_transfer" => PaymentMethod.BankTransfer,
            _ => PaymentMethod.Other
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
}
