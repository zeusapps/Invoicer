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

    [Fact]
    public void Generate_KeepsBuyerIdentificationCountryCode()
    {
        var invoice = CreateInvoice("2026/EL/0901", new DateTime(2026, 5, 11), 1234.56m, 23);

        KsefXmlGenerator.Generate(invoice);

        var doc = XDocument.Load(invoice.XmlPath);
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;

        Assert.Equal(
            new[] { "KodKraju", "NrID", "Nazwa" },
            ChildNames(doc.Root?.Element(ns + "Podmiot2")?.Element(ns + "DaneIdentyfikacyjne")));
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

        Assert.Equal(
            fixture.Root?.Element(ns + "Podmiot2")?.Element(ns + "DaneIdentyfikacyjne")?.Element(ns + "NrID")?.Value,
            generated.Root?.Element(ns + "Podmiot2")?.Element(ns + "DaneIdentyfikacyjne")?.Element(ns + "NrID")?.Value);

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
            Vat = "PL9999999999",
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
            Iban = "PL00102010260000004270201111",
            Bank = "",
            Swift = "EXAMPLE1",
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
