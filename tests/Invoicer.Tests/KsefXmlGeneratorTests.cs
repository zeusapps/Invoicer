using System.Globalization;
using System.Xml.Linq;
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

        KsefXmlGenerator.Generate(invoice);
        var xmlFirst = File.ReadAllText(invoice.XmlPath);

        KsefXmlGenerator.Generate(invoice);
        var xmlSecond = File.ReadAllText(invoice.XmlPath);

        Assert.Equal(xmlFirst, xmlSecond);
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
