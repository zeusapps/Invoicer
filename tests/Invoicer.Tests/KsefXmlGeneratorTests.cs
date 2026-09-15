using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Invoicer.Config;
using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class KsefXmlGeneratorTests
{
    [Fact]
    public void Generate_WritesRequiredNamespaceAndHeaderMetadata()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice);

        var doc = XDocument.Load(invoice.XmlPath);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;

        Assert.Equal(ns.NamespaceName, doc.Root?.Name.NamespaceName);
        Assert.Equal("FA", doc.Root?.Element(ns + "Naglowek")?.Element(ns + "KodFormularza")?.Value);
        Assert.Equal("FA (3)", doc.Root?.Element(ns + "Naglowek")?.Element(ns + "KodFormularza")?.Attribute("kodSystemowy")?.Value);
        Assert.Equal("1-0E", doc.Root?.Element(ns + "Naglowek")?.Element(ns + "KodFormularza")?.Attribute("wersjaSchemy")?.Value);
        Assert.Equal("3", doc.Root?.Element(ns + "Naglowek")?.Element(ns + "WariantFormularza")?.Value);
    }

    [Fact]
    public void Generate_ProducesDocumentValidAgainstOfficialFa3Schema()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice);

        var messages = KsefSchemaValidator.Validate(invoice.XmlPath);

        Assert.True(
            messages.Count == 0,
            $"Generated XML is not valid FA(3):{Environment.NewLine}{KsefSchemaValidator.Format(messages)}");
    }

    [Fact]
    public void Generate_ProducesValidDocument_ForDefaultConfiguration()
    {
        // The config a first-run user receives must be able to produce a valid invoice:
        // its placeholders are subject to the schema's value constraints like any other data.
        var config = ConfigManager.CreateDefault();
        config.Output.Directory = Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N"));
        var client = config.Clients[0];

        var invoice = Invoice.Create(
            client,
            config.Supplier,
            config.ResolveBillingAccount(client),
            config.Output,
            invoiceNumber: 1,
            invoiceDate: new DateTime(2026, 5, 11),
            amount: client.DefaultAmount,
            generateDocx: false,
            generatePdf: false,
            generateXml: true);
        invoice.FormattedNumber = "2026/SM/0001";

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var messages = KsefSchemaValidator.Validate(invoice.XmlPath);

        Assert.True(
            messages.Count == 0,
            $"Default configuration produces invalid FA(3) XML:{Environment.NewLine}{KsefSchemaValidator.Format(messages)}");
    }

    [Fact]
    public void Generate_ThrowsValidationError_WhenRequiredFieldMissing()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.Supplier.Tin = string.Empty;

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(ex.Errors, e => e.Contains("Supplier TIN/NIP is required."));
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Fact]
    public void Generate_IsDeterministic_ForSameInput()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        var clock = new FixedTimeProvider(GenerationInstant);

        KsefXmlGenerator.Generate(invoice, clock);
        var xmlFirst = File.ReadAllText(invoice.XmlPath);

        KsefXmlGenerator.Generate(invoice, clock);
        var xmlSecond = File.ReadAllText(invoice.XmlPath);

        Assert.Equal(xmlFirst, xmlSecond);
    }

    [Fact]
    public void Generate_VariesOnlyByGenerationTime_AcrossRuns()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));
        var xmlFirst = File.ReadAllText(invoice.XmlPath);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant.AddHours(3)));
        var xmlSecond = File.ReadAllText(invoice.XmlPath);

        Assert.NotEqual(xmlFirst, xmlSecond);
        Assert.Equal(WithoutGenerationTime(xmlFirst), WithoutGenerationTime(xmlSecond));
    }

    [Fact]
    public void Generate_IdentifiesSellerByNipAndNameOnly()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice);

        var doc = XDocument.Load(invoice.XmlPath);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        var seller = doc.Root?.Element(ns + "Podmiot1");

        Assert.Equal(
            new[] { "NIP", "Nazwa" },
            ChildNames(seller?.Element(ns + "DaneIdentyfikacyjne")));

        // The seller's country code belongs in Adres, where the schema does define it.
        Assert.Equal(
            new[] { "KodKraju", "AdresL1" },
            ChildNames(seller?.Element(ns + "Adres")));
    }

    public static TheoryData<string, string, string[], string[]> BuyerIdentificationCases => new()
    {
        { "PL", "PL9999999999", ["NIP", "Nazwa"], ["9999999999"] },
        { "DE", "DE123456789", ["KodUE", "NrVatUE", "Nazwa"], ["DE", "123456789"] },
        { "GR", "EL123456789", ["KodUE", "NrVatUE", "Nazwa"], ["EL", "123456789"] },
        { "US", "12-3456789", ["KodKraju", "NrID", "Nazwa"], ["US", "12-3456789"] },
        { "US", "", ["BrakID", "Nazwa"], ["1"] },
    };

    [Theory]
    [MemberData(nameof(BuyerIdentificationCases))]
    public void Generate_IdentifiesBuyerAccordingToClientCountry(
        string country, string vat, string[] expectedElements, string[] expectedValues)
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.Client.Country = country;
        invoice.Client.Vat = vat;
        if (country != "PL")
            MakeUntaxed(invoice);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var doc = XDocument.Load(invoice.XmlPath);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        var buyer = doc.Root?.Element(ns + "Podmiot2");
        var identification = buyer?.Element(ns + "DaneIdentyfikacyjne");

        Assert.Equal(expectedElements, ChildNames(identification));
        Assert.Equal(
            expectedValues.Append(invoice.Client.Name),
            identification?.Elements().Select(e => e.Value));
        Assert.Equal(country, buyer?.Element(ns + "Adres")?.Element(ns + "KodKraju")?.Value);

        var messages = KsefSchemaValidator.Validate(invoice.XmlPath);
        Assert.True(
            messages.Count == 0,
            $"Generated XML is not valid FA(3):{Environment.NewLine}{KsefSchemaValidator.Format(messages)}");
    }

    [Fact]
    public void Generate_TakesBuyerAddressCountryFromClient_NotFromVatPrefix()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.Client.Country = "US";
        invoice.Client.Vat = "PL9999999999";
        MakeUntaxed(invoice);

        KsefXmlGenerator.Generate(invoice);

        var doc = XDocument.Load(invoice.XmlPath);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;

        Assert.Equal("US", doc.Root?.Element(ns + "Podmiot2")?.Element(ns + "Adres")?.Element(ns + "KodKraju")?.Value);
    }

    [Theory]
    [InlineData("", "PL9999999999", "Client country is required.")]
    [InlineData("ZZ", "", "Client country 'ZZ' is not a recognized country code.")]
    [InlineData("DE", "", "Client VAT is required for clients in DE.")]
    [InlineData("PL", "", "Client VAT is required for clients in PL.")]
    [InlineData("PL", "PL123", "Client VAT 'PL123' is not a valid Polish NIP.")]
    [InlineData("DE", "FR12345678901", "Client VAT prefix 'FR' does not match client country DE (expected DE).")]
    [InlineData("DE", "DE1234567890123", "Client VAT 'DE1234567890123' is not a valid EU VAT number.")]
    public void Generate_RejectsInvalidClientCountryOrVat(string country, string vat, string expectedError)
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.Client.Country = country;
        invoice.Client.Vat = vat;

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(expectedError, ex.Errors);
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Fact]
    public void Generate_AcceptsNonEuClientWithoutVat()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.Client.Country = "US";
        invoice.Client.Vat = "";
        MakeUntaxed(invoice);

        Assert.Empty(KsefXmlGenerator.Validate(invoice));
    }

    [Fact]
    public void Generate_CodesUsClientAsNpI()
    {
        var invoice = CreateInvoice("2026/GF/0001", new DateTime(2026, 9, 12), 2500m, 23);
        invoice.Client.Country = "US";
        invoice.Client.Vat = "";
        MakeUntaxed(invoice);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var (fa, seller) = LoadFaAndSeller(invoice);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        Assert.Equal("np I", fa.Element(ns + "FaWiersz")?.Element(ns + "P_12")?.Value);
        Assert.Equal("2500", fa.Element(ns + "P_13_8")?.Value);
        Assert.DoesNotContain(fa.Elements(), e => e.Name.LocalName.StartsWith("P_14") || e.Name.LocalName == "P_13_1");
        Assert.Equal("2500", fa.Element(ns + "P_15")?.Value);
        Assert.Equal("1", fa.Element(ns + "Adnotacje")?.Element(ns + "P_18")?.Value);
        Assert.Null(seller.Element(ns + "PrefiksPodatnika"));
        AssertSchemaValid(invoice);
    }

    [Fact]
    public void Generate_CodesEuClientAsNpII_WithSellerPrefix()
    {
        var invoice = CreateInvoice("2026/DE/0001", new DateTime(2026, 9, 12), 1000m, 23);
        invoice.Client.Country = "DE";
        invoice.Client.Vat = "DE123456789";
        MakeUntaxed(invoice);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var (fa, seller) = LoadFaAndSeller(invoice);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        Assert.Equal("np II", fa.Element(ns + "FaWiersz")?.Element(ns + "P_12")?.Value);
        Assert.Equal("1000", fa.Element(ns + "P_13_9")?.Value);
        Assert.DoesNotContain(fa.Elements(), e => e.Name.LocalName.StartsWith("P_14"));
        Assert.Equal("1000", fa.Element(ns + "P_15")?.Value);
        Assert.Equal("1", fa.Element(ns + "Adnotacje")?.Element(ns + "P_18")?.Value);
        Assert.Equal("PrefiksPodatnika", seller.Elements().First().Name.LocalName);
        Assert.Equal("PL", seller.Element(ns + "PrefiksPodatnika")?.Value);
        AssertSchemaValid(invoice);
    }

    [Theory]
    [InlineData(23, "P_13_1", "P_14_1")]
    [InlineData(8, "P_13_2", "P_14_2")]
    [InlineData(5, "P_13_3", "P_14_3")]
    public void Generate_CodesPolishClientWithNumericRate(int rate, string netField, string vatField)
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1000m, rate);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var (fa, seller) = LoadFaAndSeller(invoice);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        Assert.Equal(rate.ToString(), fa.Element(ns + "FaWiersz")?.Element(ns + "P_12")?.Value);
        Assert.Equal("1000", fa.Element(ns + netField)?.Value);
        Assert.Equal(invoice.VatAmount.ToString("0.##", CultureInfo.InvariantCulture), fa.Element(ns + vatField)?.Value);
        Assert.Equal(invoice.GrossAmount.ToString("0.##", CultureInfo.InvariantCulture), fa.Element(ns + "P_15")?.Value);
        Assert.Equal("2", fa.Element(ns + "Adnotacje")?.Element(ns + "P_18")?.Value);
        Assert.Null(seller.Element(ns + "PrefiksPodatnika"));
        AssertSchemaValid(invoice);
    }

    [Theory]
    [InlineData("US", 23, "Client VAT rate must be 0 for clients outside Poland (country US), not 23%.")]
    [InlineData("PL", 0, "Client VAT rate 0% is not supported for KSeF invoices to Polish clients (supported: 23, 22, 8, 7, 5).")]
    public void Generate_RejectsVatRateInconsistentWithCountry(string country, int rate, string expectedError)
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1000m, rate);
        invoice.Client.Country = country;
        invoice.Client.Vat = country == "PL" ? "PL9999999999" : "";

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains(expectedError, ex.Errors);
        Assert.False(File.Exists(invoice.XmlPath));
    }

    private static void MakeUntaxed(Invoice invoice)
    {
        invoice.Client.VatRate = 0;
        invoice.VatRate = 0;
        invoice.VatAmount = 0;
        invoice.GrossAmount = invoice.NetAmount;
    }

    private static (XElement Fa, XElement Seller) LoadFaAndSeller(Invoice invoice)
    {
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        var root = XDocument.Load(invoice.XmlPath).Root!;
        return (root.Element(ns + "Fa")!, root.Element(ns + "Podmiot1")!);
    }

    private static void AssertSchemaValid(Invoice invoice)
    {
        var messages = KsefSchemaValidator.Validate(invoice.XmlPath);
        Assert.True(
            messages.Count == 0,
            $"Generated XML is not valid FA(3):{Environment.NewLine}{KsefSchemaValidator.Format(messages)}");
    }

    [Fact]
    public void Generate_RejectsBillingAccountWithoutIban()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.BillingAccount.Iban = " ";

        var ex = Assert.Throws<KsefValidationException>(() => KsefXmlGenerator.Generate(invoice));

        Assert.Contains("Billing account 'PLN' IBAN is required.", ex.Errors);
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Fact]
    public void Generate_WritesPaymentDetailsFromBillingAccount()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.BillingAccount = new BillingAccountConfig
        {
            Key = "USD",
            Iban = "PL42 1090 1320 0000 0001 5470 1995",
            Swift = "WBKPPLPP",
            Bank = "Santander",
        };

        KsefXmlGenerator.Generate(invoice);

        var account = PaymentAccount(invoice);
        Assert.Equal(new[] { "NrRB", "SWIFT", "NazwaBanku" }, ChildNames(account));
        Assert.Equal(
            new[] { "PL42109013200000000154701995", "WBKPPLPP", "Santander" },
            account?.Elements().Select(e => e.Value));
        Assert.Empty(KsefSchemaValidator.Validate(invoice.XmlPath));
    }

    [Fact]
    public void Generate_WritesOnlyAccountNumber_WhenSwiftAndBankAreEmpty()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);
        invoice.BillingAccount = new BillingAccountConfig { Key = "PLN", Iban = "PL42109013200000000154701995" };

        KsefXmlGenerator.Generate(invoice);

        Assert.Equal(new[] { "NrRB" }, ChildNames(PaymentAccount(invoice)));
        Assert.Empty(KsefSchemaValidator.Validate(invoice.XmlPath));
    }

    private static XElement? PaymentAccount(Invoice invoice)
    {
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        return XDocument.Load(invoice.XmlPath).Root?
            .Element(ns + "Fa")?.Element(ns + "Platnosc")?.Element(ns + "RachunekBankowy");
    }

    [Fact]
    public void Generate_RecordsGenerationTime_IndependentOfInvoiceDate()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var doc = XDocument.Load(invoice.XmlPath);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;

        Assert.Equal(
            "2026-08-06T16:33:00.0000000Z",
            doc.Root?.Element(ns + "Naglowek")?.Element(ns + "DataWytworzeniaFa")?.Value);
        Assert.Equal("2026-05-11", doc.Root?.Element(ns + "Fa")?.Element(ns + "P_1")?.Value);
    }

    [Fact]
    public void Generate_ProducesValidDocument_WhenInvoiceDatePrecedesSchemaLowerBound()
    {
        // FA(3) bounds DataWytworzeniaFa to 2025-09-01Z..2050-01-01Z; deriving it from an
        // earlier invoice date would put the header outside the permitted range.
        var invoice = CreateInvoice("2025/EL/0001", new DateTime(2025, 8, 1), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice, new FixedTimeProvider(GenerationInstant));

        var messages = KsefSchemaValidator.Validate(invoice.XmlPath);

        Assert.True(
            messages.Count == 0,
            $"Generated XML is not valid FA(3):{Environment.NewLine}{KsefSchemaValidator.Format(messages)}");
    }

    [Theory]
    [InlineData("Wersja_robocza_20260511113726.xml", "2026/EL/0901", "2026-05-11", "1234.56", "283.95", "1518.51")]
    [InlineData("Wersja_robocza_20260429065615.xml", "2026/EL/0902", "2026-04-29", "987.65", "227.16", "1214.81")]
    public void Generate_MatchesReferenceFixtures_ForKeyValues(
        string fixtureName,
        string invoiceNumber,
        string invoiceDate,
        string net,
        string vat,
        string gross)
    {
        var invoice = CreateInvoice(invoiceNumber, DateTime.Parse(invoiceDate, CultureInfo.InvariantCulture), decimal.Parse(net, CultureInfo.InvariantCulture), 23);
        invoice.VatAmount = decimal.Parse(vat, CultureInfo.InvariantCulture);
        invoice.GrossAmount = decimal.Parse(gross, CultureInfo.InvariantCulture);

        KsefXmlGenerator.Generate(invoice);

        var generated = XDocument.Load(invoice.XmlPath);
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "TestData", fixtureName);
        var fixture = XDocument.Load(fixturePath);

        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;

        Assert.Equal(
            fixture.Root?.Name.NamespaceName,
            generated.Root?.Name.NamespaceName);

        Assert.Equal(
            fixture.Root?.Element(ns + "Naglowek")?.Element(ns + "KodFormularza")?.Attribute("kodSystemowy")?.Value,
            generated.Root?.Element(ns + "Naglowek")?.Element(ns + "KodFormularza")?.Attribute("kodSystemowy")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Fa")?.Element(ns + "P_2")?.Value,
            generated.Root?.Element(ns + "Fa")?.Element(ns + "P_2")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Fa")?.Element(ns + "P_1")?.Value,
            generated.Root?.Element(ns + "Fa")?.Element(ns + "P_1")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Fa")?.Element(ns + "P_13_1")?.Value,
            generated.Root?.Element(ns + "Fa")?.Element(ns + "P_13_1")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Fa")?.Element(ns + "P_14_1")?.Value,
            generated.Root?.Element(ns + "Fa")?.Element(ns + "P_14_1")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Fa")?.Element(ns + "P_15")?.Value,
            generated.Root?.Element(ns + "Fa")?.Element(ns + "P_15")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Podmiot1")?.Element(ns + "DaneIdentyfikacyjne")?.Element(ns + "NIP")?.Value,
            generated.Root?.Element(ns + "Podmiot1")?.Element(ns + "DaneIdentyfikacyjne")?.Element(ns + "NIP")?.Value);

        // The drafts identified the Polish buyer by NrID with a PL prefix; the correct TPodmiot2
        // form for a Polish buyer is NIP, which carries the same number without the prefix.
        var fixtureBuyerId = fixture.Root?.Element(ns + "Podmiot2")?.Element(ns + "DaneIdentyfikacyjne")?.Element(ns + "NrID")?.Value;
        Assert.Equal(
            fixtureBuyerId is { } id && id.StartsWith("PL", StringComparison.Ordinal) ? id[2..] : fixtureBuyerId,
            generated.Root?.Element(ns + "Podmiot2")?.Element(ns + "DaneIdentyfikacyjne")?.Element(ns + "NIP")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Podmiot2")?.Element(ns + "JST")?.Value,
            generated.Root?.Element(ns + "Podmiot2")?.Element(ns + "JST")?.Value);

        Assert.Equal(
            fixture.Root?.Element(ns + "Podmiot2")?.Element(ns + "GV")?.Value,
            generated.Root?.Element(ns + "Podmiot2")?.Element(ns + "GV")?.Value);
    }

    private static readonly DateTimeOffset GenerationInstant =
        new(2026, 8, 6, 16, 33, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider(DateTimeOffset instant) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => instant;
    }

    private static IEnumerable<string>? ChildNames(XElement? element) =>
        element?.Elements().Select(e => e.Name.LocalName);

    private static string WithoutGenerationTime(string xml) =>
        Regex.Replace(xml, "<DataWytworzeniaFa>.*?</DataWytworzeniaFa>", string.Empty);

    private static Invoice CreateInvoice(string formattedNumber, DateTime invoiceDate, decimal netAmount, int vatRate)
    {
        var outputDir = Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N"));
        var client = new ClientConfig
        {
            Key = "EL",
            Name = "Sample Client Sp. z o.o.",
            Address = "10 Example Street, 00-001 Warsaw, Poland",
            Country = "PL",
            Vat = "PL9999999999",
            BillingAccount = "PLN",
            Currency = "PLN",
            VatRate = vatRate,
            ServiceDescription = "Consulting services",
            ServiceDescriptionUa = "",
            InvoicePrefix = "EL",
            DefaultAmount = netAmount,
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
            Key = "PLN",
            Label = "PLN account",
            Iban = "PL00102010260000004270201111",
            Bank = "",
            Swift = "EXAMPLE1",
            Currency = "PLN",
        };

        var output = new OutputConfig
        {
            Directory = outputDir,
            Pattern = "{year}/Invoices",
            Filename = "{date}_{client}_PL"
        };

        var invoice = Invoice.Create(
            client,
            supplier,
            billingAccount,
            output,
            invoiceNumber: int.Parse(formattedNumber.Split('/').Last()),
            invoiceDate: invoiceDate,
            amount: netAmount,
            generateDocx: false,
            generatePdf: false,
            generateXml: true);

        invoice.FormattedNumber = formattedNumber;
        return invoice;
    }
}
