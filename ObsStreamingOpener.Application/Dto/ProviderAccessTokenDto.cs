namespace ObsStreamingOpener.Application.Dto;

public sealed record ProviderAccessTokenDto(Guid MonitoredAccountId, string AccessToken);
