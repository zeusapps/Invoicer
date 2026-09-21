using Invoicer.Models;

namespace Invoicer.Exchange;

/// <summary>
/// The currency rules that decide whether an invoice needs an exchange rate and which date
/// determines it. Pure: no clock, no network, so both are testable on their own.
/// </summary>
public static class ExchangeRateRules
{
    public const string BaseCurrency = "PLN";

    /// <summary>
    /// Whether an invoice in <paramref name="currency"/> needs an exchange rate, which is any
    /// currency other than PLN. An empty currency is treated as PLN, matching the model default.
    /// </summary>
    public static bool RequiresRate(string? currency)
    {
        var normalized = Normalize(currency);
        return normalized.Length > 0 && normalized != BaseCurrency;
    }

    public static string Normalize(string? currency) => (currency ?? "").Trim().ToUpperInvariant();

    /// <summary>
    /// The date that determines which rate applies, under art. 31a of the VAT act: the day the
    /// tax obligation arose, except where the invoice was issued before that day, when the
    /// invoice date governs instead.
    ///
    /// Those two branches collapse into one expression. Taking the service month's last day as
    /// the day the obligation arose, ust. 2 applies exactly when the invoice predates it, so the
    /// earlier of the two dates is correct either way and no branch on the month offset rule is
    /// needed.
    ///
    /// This is a default, not a verdict: an obligation can instead arise on payment receipt,
    /// which the invoice data cannot show. The value is presented for the user to override.
    /// </summary>
    public static DateTime RelevantDate(DateTime invoiceDate, DateTime serviceMonth)
    {
        var serviceMonthEnd = new DateTime(
            serviceMonth.Year,
            serviceMonth.Month,
            DateTime.DaysInMonth(serviceMonth.Year, serviceMonth.Month));

        return invoiceDate.Date <= serviceMonthEnd ? invoiceDate.Date : serviceMonthEnd;
    }

    /// <summary>The relevant date for an invoice, derived from its own date and service month.</summary>
    public static DateTime RelevantDate(Invoice invoice) =>
        RelevantDate(invoice.InvoiceDate, invoice.ServiceMonth);
}
