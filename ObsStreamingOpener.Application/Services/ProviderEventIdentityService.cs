using System.Security.Cryptography;
using System.Text;
using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Services;

public sealed class ProviderEventIdentityService : IProviderEventIdentityService
{
    public string CreateIdentityKey(ProviderEvent providerEvent)
    {
        if (!string.IsNullOrWhiteSpace(providerEvent.IdentityKey))
        {
            return providerEvent.IdentityKey;
        }

        if (!string.IsNullOrWhiteSpace(providerEvent.ExternalEventId))
        {
            return $"{providerEvent.Provider}:{providerEvent.EventType}:native:{providerEvent.ExternalEventId}";
        }

        var actor = Normalize(providerEvent.ActorExternalId ?? providerEvent.ActorName);
        var occurredAt = providerEvent.OccurredAt.ToUniversalTime().ToString("O");
        var contentHash = Sha256Short($"{providerEvent.Title}|{providerEvent.Message}|{providerEvent.Value ?? providerEvent.Amount}|{providerEvent.Unit ?? providerEvent.Currency}");
        return $"{providerEvent.Provider}:{providerEvent.EventType}:{actor}:{occurredAt}:{contentHash}";
    }

    public string CreatePayloadHash(ProviderEvent providerEvent)
        => Sha256Short($"{providerEvent.Title}|{providerEvent.Message}|{providerEvent.Value ?? providerEvent.Amount}|{providerEvent.Unit ?? providerEvent.Currency}|{providerEvent.RawPayloadJson}|{providerEvent.ContextJson}");

    public string CreateMessageIdentityKey(ProviderMessageUpsert message)
    {
        if (!string.IsNullOrWhiteSpace(message.IdentityKey))
        {
            return message.IdentityKey;
        }

        if (!string.IsNullOrWhiteSpace(message.ExternalMessageId))
        {
            return $"{message.Provider}:{message.Source}:native:{message.ExternalMessageId}";
        }

        var author = Normalize(message.AuthorExternalId ?? message.AuthorDisplayName);
        var publishedAt = message.PublishedAt.ToUniversalTime().ToString("O");
        var textHash = Sha256Short(message.MessageText ?? string.Empty);
        return $"{message.Provider}:{message.Source}:{author}:{publishedAt}:{textHash}";
    }

    private static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim().ToLowerInvariant();

    private static string Sha256Short(string value)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
}
