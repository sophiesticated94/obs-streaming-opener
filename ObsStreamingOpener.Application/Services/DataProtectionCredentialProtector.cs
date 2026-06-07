using Microsoft.AspNetCore.DataProtection;
using ObsStreamingOpener.Application.Contracts;

namespace ObsStreamingOpener.Application.Services;

public sealed class DataProtectionCredentialProtector(IDataProtectionProvider dataProtectionProvider) : ICredentialProtector
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("ObsStreamingOpener.ProviderCredentials.v1");

    public string Protect(string value)
        => _protector.Protect(value);

    public string Unprotect(string value)
        => _protector.Unprotect(value);
}
