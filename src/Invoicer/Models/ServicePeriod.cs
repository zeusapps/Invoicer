namespace Invoicer.Models;

/// <summary>Inclusive calendar dates, resolved once for an invoice generation request.</summary>
public sealed record ServicePeriod
{
    public DateTime Start { get; }
    public DateTime End { get; }

    private ServicePeriod(DateTime start, DateTime end) => (Start, End) = (start.Date, end.Date);

    public static ServicePeriod Resolve(DateTime serviceMonth, DateTime? start = null, DateTime? end = null)
    {
        if (start.HasValue != end.HasValue)
            throw new InvoiceValidationException(["Service period requires both From and To dates."]);
        if (!start.HasValue)
        {
            if (serviceMonth == default)
                throw new InvoiceValidationException(["Service month is required to derive the service period."]);
            start = new DateTime(serviceMonth.Year, serviceMonth.Month, 1);
            end = new DateTime(serviceMonth.Year, serviceMonth.Month,
                DateTime.DaysInMonth(serviceMonth.Year, serviceMonth.Month));
        }
        if (start.Value.Date == default || end!.Value.Date == default)
            throw new InvoiceValidationException(["Service period dates must be valid, non-empty dates."]);
        if (start.Value.Date > end.Value.Date)
            throw new InvoiceValidationException(["Service period From date must not be after To date."]);
        return new(start.Value, end.Value);
    }

    public bool IsFullMonth => Start.Day == 1 && Start.Year == End.Year && Start.Month == End.Month
        && End.Day == DateTime.DaysInMonth(End.Year, End.Month);
}
