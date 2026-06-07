namespace ObsStreamingOpener.Domain;

public readonly record struct UtcRange(DateTimeOffset? Since, DateTimeOffset? Until)
{
    public bool Contains(DateTimeOffset value)
        => (!Since.HasValue || value >= Since.Value)
            && (!Until.HasValue || value < Until.Value);
}
