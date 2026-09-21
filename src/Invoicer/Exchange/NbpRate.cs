namespace Invoicer.Exchange;

/// <summary>
/// One average (table A) rate as published by the NBP: how many PLN one unit of the currency
/// is worth, the date the table took effect, and that table's identifier.
/// </summary>
/// <param name="Rate">PLN per one unit, at the precision NBP published it.</param>
/// <param name="EffectiveDate">The table's effective date.</param>
/// <param name="TableNumber">The table identifier, for example <c>182/A/NBP/2026</c>.</param>
public sealed record NbpRate(decimal Rate, DateTime EffectiveDate, string TableNumber);

/// <summary>
/// The outcome of a rate lookup. Every failure is a message rather than an exception, so a
/// missing rate degrades to "type it in" instead of interrupting invoice preparation.
/// </summary>
public sealed class NbpRateResult
{
    private NbpRateResult(NbpRate? rate, string? error)
    {
        Rate = rate;
        Error = error;
    }

    public NbpRate? Rate { get; }

    public string? Error { get; }

    public bool Succeeded => Rate is not null;

    public static NbpRateResult Success(NbpRate rate) => new(rate, null);

    public static NbpRateResult Failed(string error) => new(null, error);
}
