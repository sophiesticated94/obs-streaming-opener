using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IAlertRuleStore
{
    Task<IReadOnlyList<AlertRuleDto>> GetAlertRulesAsync(Guid? monitoredChannelId, CancellationToken cancellationToken = default);

    Task<AlertRuleDto?> GetAlertRuleAsync(Guid monitoredChannelId, StreamEventType eventType, CancellationToken cancellationToken = default);

    Task<AlertRuleDto> UpsertAlertRuleAsync(SaveAlertRuleRequest request, CancellationToken cancellationToken = default);
}
