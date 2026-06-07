namespace ObsStreamingOpener.Domain;

public readonly record struct Money(decimal Amount, string Currency)
{
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount - other.Amount };
    }

    public Money Round(int decimals = 2, MidpointRounding mode = MidpointRounding.AwayFromZero)
        => this with { Amount = decimal.Round(Amount, decimals, mode) };

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Cannot combine money values in '{Currency}' and '{other.Currency}'.");
        }
    }
}
