namespace ObsStreamingOpener.Application.Contracts;

public interface ICredentialProtector
{
    string Protect(string value);

    string Unprotect(string value);
}
