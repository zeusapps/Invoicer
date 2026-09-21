using System.Globalization;
using System.Xml.Linq;
using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class KsefExchangeRateTests
{
    private static readonly XNamespace Ns = KsefXmlGenerator.Metadata.Namespace;

    private static XElement? FaWiersz(string xmlPath) =>
        XDocument.Load(xmlPath).Root?.Element(Ns + "Fa")?.Element(Ns + "FaWiersz");

    private static void AssertSchemaValid(string xmlPath)
    {
        var messages = KsefSchemaValidator.Validate(xmlPath);

        Assert.True(
            messages.Count == 0,
            $"Generated XML is not valid FA(3):{Environment.NewLine}{KsefSchemaValidator.Format(messages)}");
    }

    // Task 3.1 / spec scenario: "USD invoice carries the rate".
    [Fact]
    public void ForeignCurrencyInvoice_CarriesTheRateAsTheLastElementOfFaWiersz()
    {
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: 3.7998m);

        KsefXmlGenerator.Generate(invoice);

        var line = FaWiersz(invoice.XmlPath);
        Assert.Equal("3.7998", line?.Element(Ns + "KursWaluty")?.Value);
        Assert.Equal("KursWaluty", line?.Elements().Last().Name.LocalName);
        // The schema puts KursWaluty after Procedura and before StanPrzed; P_12 is the last
        // element this generator writes before it.
        Assert.Equal("P_12", line?.Elements().SkipLast(1).Last().Name.LocalName);
        AssertSchemaValid(invoice.XmlPath);
    }

    // Task 3.1 / spec scenario: "Rate with six decimal places".
    [Fact]
    public void RateWithSixDecimalPlaces_IsWrittenInFull()
    {
        var invoice = CreateInvoice(currency: "HUF", country: "HU", vat: "HU12345678", rate: 0.012007m);

        KsefXmlGenerator.Generate(invoice);

        Assert.Equal("0.012007", FaWiersz(invoice.XmlPath)?.Element(Ns + "KursWaluty")?.Value);
        AssertSchemaValid(invoice.XmlPath);
    }

    [Fact]
    public void RateIsNeverRoundedToTheFormatUsedForAmounts()
    {
        // "0.##", used for the monetary fields, would render this as 3.8 and misstate the base.
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: 3.7998m);

        KsefXmlGenerator.Generate(invoice);

        Assert.Equal("3.7998", FaWiersz(invoice.XmlPath)?.Element(Ns + "KursWaluty")?.Value);
    }

    [Fact]
    public void RateWithTrailingZeros_IsWrittenWithoutThem()
    {
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: 3.79980000m);

        KsefXmlGenerator.Generate(invoice);

        Assert.Equal("3.7998", FaWiersz(invoice.XmlPath)?.Element(Ns + "KursWaluty")?.Value);
        AssertSchemaValid(invoice.XmlPath);
    }

    // Task 3.2 / spec scenario: "PLN invoice omits the rate".
    [Fact]
    public void PlnInvoice_OmitsTheRateEntirely()
    {
        var invoice = CreateInvoice(currency: "PLN", country: "PL", vat: "PL9999999999", vatRate: 23);

        KsefXmlGenerator.Generate(invoice);

        var document = XDocument.Load(invoice.XmlPath);
        Assert.Empty(document.Descendants(Ns + "KursWaluty"));
        AssertSchemaValid(invoice.XmlPath);
    }

    [Fact]
    public void PlnInvoice_IgnoresAStrayRateLeftOnTheInvoice()
    {
        var invoice = CreateInvoice(currency: "PLN", country: "PL", vat: "PL9999999999", vatRate: 23);
        invoice.ExchangeRate = 3.7998m;

        KsefXmlGenerator.Generate(invoice);

        Assert.Empty(XDocument.Load(invoice.XmlPath).Descendants(Ns + "KursWaluty"));
    }

    // Task 3.3 / spec scenario: "Foreign-currency invoice without a rate".
    [Fact]
    public void ForeignCurrencyInvoiceWithoutARate_IsRejected()
    {
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: null);

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(ex.Errors, e => e == "An exchange rate is required for a USD invoice.");
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-3.7998)]
    public void NonPositiveRate_IsRejected(decimal rate)
    {
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: rate);

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(ex.Errors, e => e.Contains("must be greater than zero"));
        Assert.False(File.Exists(invoice.XmlPath));
    }

    // Task 3.4 / spec scenario: "Rate too precise for the schema".
    [Fact]
    public void RateBeyondSixDecimalPlaces_IsRejectedRatherThanRounded()
    {
        var invoice = CreateInvoice(currency: "IDR", country: "ID", rate: 0.00021425m);

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(ex.Errors, e => e.Contains("more than 6 decimal places"));
        Assert.Contains(ex.Errors, e => e.Contains("0.00021425"));
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Fact]
    public void RateWithTrailingZerosBeyondSixPlaces_IsAccepted()
    {
        // A scale of 8 that loses nothing at six places is representable, so it must not be
        // rejected: the limit is on the value, not on how it happened to be written.
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: 3.79980000m);

        KsefXmlGenerator.Generate(invoice);

        Assert.True(File.Exists(invoice.XmlPath));
    }

    // Task 3.5 / spec scenario: "Polish client billed in USD".
    [Fact]
    public void PolishClientInAForeignCurrency_IsRejected()
    {
        var invoice = CreateInvoice(
            currency: "USD", country: "PL", vat: "PL9999999999", vatRate: 23, rate: 3.7998m);

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(ex.Errors, e => e.Contains("Polish client cannot be invoiced in a foreign currency"));
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Fact]
    public void PolishClientInAForeignCurrency_StillGeneratesDocxAndPdf()
    {
        var invoice = CreateInvoice(
            currency: "USD", country: "PL", vat: "PL9999999999", vatRate: 23, rate: 3.7998m);

        DocxGenerator.Generate(invoice);
        PdfGenerator.Generate(invoice);

        Assert.True(File.Exists(invoice.DocxPath));
        Assert.True(File.Exists(invoice.PdfPath));
    }

    // Task 3.6 / spec scenario: "Generation performs no rate lookup".
    [Fact]
    public void Generate_IsSynchronous()
    {
        var method = typeof(KsefXmlGenerator).GetMethod(nameof(KsefXmlGenerator.Generate));

        Assert.NotNull(method);
        Assert.Equal(typeof(void), method!.ReturnType);
    }

    [Fact]
    public void Generate_WritesTheRateOnTheInvoiceWithoutConsultingAnySource()
    {
        // A value no NBP table ever published. If generation looked a rate up, or corrected the
        // one it was given, this would not survive to the document.
        var invoice = CreateInvoice(currency: "USD", country: "US", rate: 1.234567m);

        KsefXmlGenerator.Generate(invoice);

        Assert.Equal("1.234567", FaWiersz(invoice.XmlPath)?.Element(Ns + "KursWaluty")?.Value);
    }

    // Task 3.6 / spec scenario: "Determinism with a rate present".
    [Fact]
    public void Generate_IsDeterministic_WithARatePresent()
    {
        var clock = new FixedClock(new DateTimeOffset(2026, 8, 6, 16, 33, 0, TimeSpan.Zero));

        var first = CreateInvoice(currency: "USD", country: "US", rate: 3.7998m);
        KsefXmlGenerator.Generate(first, clock);

        var second = CreateInvoice(currency: "USD", country: "US", rate: 3.7998m);
        second.FormattedNumber = first.FormattedNumber;
        KsefXmlGenerator.Generate(second, clock);

        Assert.Equal(File.ReadAllBytes(first.XmlPath), File.ReadAllBytes(second.XmlPath));
    }

    // Task 3.7: the two rate fields this system must never write.
    [Theory]
    [InlineData("USD", "US")]
    [InlineData("PLN", "PL")]
    public void OtherRateFields_AreNeverWritten(string currency, string country)
    {
        var invoice = currency == "PLN"
            ? CreateInvoice(currency, country, vat: "PL9999999999", vatRate: 23)
            : CreateInvoice(currency, country, rate: 3.7998m);

        KsefXmlGenerator.Generate(invoice);

        var document = XDocument.Load(invoice.XmlPath);
        Assert.Empty(document.Descendants(Ns + "KursWalutyZ"));
        Assert.Empty(document.Descendants(Ns + "KursUmowny"));
        Assert.Empty(document.Descendants(Ns + "WalutaUmowna"));
    }

    private sealed class FixedClock(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    private static Invoice CreateInvoice(
        string currency,
        string country,
        string vat = "",
        int vatRate = 0,
        decimal? rate = null)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N"));

        var client = new ClientConfig
        {
            Key = "EL",
            Name = "Sample Client Inc.",
            Address = "123 River St, Newton, MA 02465",
            Country = country,
            Vat = vat,
            BillingAccount = "MAIN",
            Currency = currency,
            VatRate = vatRate,
            ServiceDescription = "Software development services",
            InvoicePrefix = "EL",
            DefaultAmount = 5000m,
            MonthOffsetRule = "early_previous",
            Enabled = true,
        };

        var supplier = new SupplierConfig
        {
            Name = "Sample Supplier Sp. z o.o.",
            Tin = "1111111111",
            Regon = "",
            Vat = "",
            Address = "1 Demo Avenue, 00-002 Warsaw, Poland",
        };

        var billingAccount = new BillingAccountConfig
        {
            Key = "MAIN",
            Label = "Main account",
            Iban = "PL00102010260000004270201111",
            Bank = "Santander",
            Swift = "EXAMPLE1",
            Currency = currency,
        };

        var output = new OutputConfig
        {
            Directory = outputDir,
            Pattern = "{year}/Invoices",
            Filename = "{date}_{client}_PL",
        };

        var invoice = Invoice.Create(
            client,
            supplier,
            billingAccount,
            output,
            invoiceNumber: 7,
            invoiceDate: new DateTime(2026, 10, 5),
            amount: 5000m,
            generateDocx: false,
            generatePdf: false,
            generateXml: true,
            exchangeRate: rate,
            exchangeRateDate: rate is null ? null : new DateTime(2026, 9, 29),
            exchangeRateTable: rate is null ? null : "189/A/NBP/2026");

        invoice.FormattedNumber = string.Create(
            CultureInfo.InvariantCulture, $"2026/EL/{7:D4}");
        return invoice;
    }
}
