using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Contracts;

public interface IFeeEstimator
{
    IReadOnlyList<FeeLine> EstimateFees(ProviderTipRecord record);
}
