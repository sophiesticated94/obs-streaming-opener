using ObsStreamingOpener.Application.Dto;

namespace ObsStreamingOpener.Application.Contracts;

public interface IProviderEventIdentityService
{
    string CreateIdentityKey(ProviderEvent providerEvent);

    string CreatePayloadHash(ProviderEvent providerEvent);

    string CreateMessageIdentityKey(ProviderMessageUpsert message);
}
