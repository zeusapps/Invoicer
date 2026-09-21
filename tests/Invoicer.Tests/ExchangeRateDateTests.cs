using Invoicer.Exchange;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class ExchangeRateDateTests
{
    // The three cases named in the exchange-rate spec, covering both branches of art. 31a.
    [Theory]
    // Invoiced after the service month ended: the obligation arose on completion.
    [InlineData("2026-10-05", "2026-09-30", "2026-09-30")]
    // Invoiced during the service month: the invoice predates completion, so it governs.
    [InlineData("2026-10-25", "2026-10-31", "2026-10-25")]
    // Invoiced on the last day of its own service month: the two dates coincide.
    [InlineData("2026-10-31", "2026-10-31", "2026-10-31")]
    public void RelevantDate_IsTheEarlierOfInvoiceDateAndServiceMonthEnd(
        string invoiceDate, string serviceMonth, string expected)
    {
        var result = ExchangeRateRules.RelevantDate(DateTime.Parse(invoiceDate), DateTime.Parse(serviceMonth));

        Assert.Equal(DateTime.Parse(expected), result);
    }

    // Any day within the service month identifies it; only the month's last day matters.
    [Theory]
    [InlineData("2026-09-01")]
    [InlineData("2026-09-15")]
    [InlineData("2026-09-30")]
    public void RelevantDate_UsesTheServiceMonthEndRegardlessOfTheDayWithinIt(string serviceMonth)
    {
        var result = ExchangeRateRules.RelevantDate(new DateTime(2026, 10, 5), DateTime.Parse(serviceMonth));

        Assert.Equal(new DateTime(2026, 9, 30), result);
    }

    [Fact]
    public void RelevantDate_HandlesFebruaryInALeapYear()
    {
        var result = ExchangeRateRules.RelevantDate(new DateTime(2028, 3, 5), new DateTime(2028, 2, 10));

        Assert.Equal(new DateTime(2028, 2, 29), result);
    }

    [Fact]
    public void RelevantDate_IgnoresTheTimeOfDayOnTheInvoiceDate()
    {
        var result = ExchangeRateRules.RelevantDate(
            new DateTime(2026, 10, 25, 23, 59, 59), new DateTime(2026, 10, 1));

        Assert.Equal(new DateTime(2026, 10, 25), result);
    }

    // The derivation must hold through the real month-offset rules, not just in isolation.
    [Theory]
    // early_previous, invoiced on the 5th: service month is September, which has ended.
    [InlineData("early_previous", "2026-10-05", "2026-09-30")]
    // early_previous, invoiced on the 25th: service month is October, still running.
    [InlineData("early_previous", "2026-10-25", "2026-10-25")]
    // early_current, invoiced on the 5th: service month is October, still running.
    [InlineData("early_current", "2026-10-05", "2026-10-05")]
    // early_current, invoiced on the 25th: service month is November, still to come.
    [InlineData("early_current", "2026-10-25", "2026-10-25")]
    public void RelevantDate_FollowsTheClientsMonthOffsetRule(string rule, string invoiceDate, string expected)
    {
        var date = DateTime.Parse(invoiceDate);
        var serviceMonth = Invoice.CalculateServiceMonth(date, rule);

        Assert.Equal(DateTime.Parse(expected), ExchangeRateRules.RelevantDate(date, serviceMonth));
    }

    [Fact]
    public void RelevantDate_ForAnInvoice_UsesItsOwnDateAndServiceMonth()
    {
        var invoice = new Invoice
        {
            Client = new ClientConfig(),
            Supplier = new SupplierConfig(),
            BillingAccount = new BillingAccountConfig(),
            InvoiceDate = new DateTime(2026, 10, 5),
            ServiceMonth = new DateTime(2026, 9, 5),
        };

        Assert.Equal(new DateTime(2026, 9, 30), ExchangeRateRules.RelevantDate(invoice));
    }

    [Theory]
    [InlineData("USD", true)]
    [InlineData("usd", true)]
    [InlineData("EUR", true)]
    [InlineData("PLN", false)]
    [InlineData("pln", false)]
    [InlineData(" PLN ", false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData(null, false)]
    public void RequiresRate_IsTrueForEveryCurrencyExceptPln(string? currency, bool expected)
    {
        Assert.Equal(expected, ExchangeRateRules.RequiresRate(currency));
    }
}
