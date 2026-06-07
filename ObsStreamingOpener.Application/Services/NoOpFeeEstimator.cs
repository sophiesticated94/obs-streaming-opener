using ObsStreamingOpener.Application.Contracts;
using ObsStreamingOpener.Application.Dto;
using ObsStreamingOpener.Domain;

namespace ObsStreamingOpener.Application.Services;

public sealed class NoOpFeeEstimator : IFeeEstimator
{
    public IReadOnlyList<FeeLine> EstimateFees(ProviderTipRecord record)
        => record.FeeLines;
}
