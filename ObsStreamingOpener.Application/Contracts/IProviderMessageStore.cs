using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderMessageStore
{
    Task<ProviderMessageDto> UpsertMessageAsync(ProviderMessageUpsert message, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderMessageDto>> GetRecentMessagesAsync(
        Guid monitoredChannelId,
        MessageSource? source,
        int limit,
        Guid? providerResourceId = null,
        Guid? streamSessionId = null,
        Guid? audienceMemberId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProviderMessageDto>> SearchMessagesAsync(
        Guid monitoredChannelId,
        string? query,
        MessageSource? source,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int limit,
        CancellationToken cancellationToken = default);
}
