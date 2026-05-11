using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class InvoiceOutputBehaviorTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void Create_SetsCombinedOutputFlags(bool docx, bool pdf, bool xml)
    {
        var invoice = Invoice.Create(
            CreateClient(),
            CreateSupplier(),
            CreateOutput(),
            invoiceNumber: 1,
            invoiceDate: new DateTime(2026, 5, 11),
            amount: 100m,
            generateDocx: docx,
            generatePdf: pdf,
            generateXml: xml);

        Assert.Equal(docx, invoice.GenerateDocx);
        Assert.Equal(pdf, invoice.GeneratePdf);
        Assert.Equal(xml, invoice.GenerateXml);
    }

    [Fact]
    public void Create_UsesConfiguredYearFolderAndFilenameForXmlPath()
    {
        var output = new OutputConfig
        {
            Directory = "./output-root",
            Pattern = "{year}/Invoices",
            Filename = "{date}_{client}_PL"
        };

        var invoiceDate = new DateTime(2026, 5, 11);
        var invoice = Invoice.Create(
            CreateClient(),
            CreateSupplier(),
            output,
            invoiceNumber: 12,
            invoiceDate: invoiceDate,
            amount: 100m,
            generateDocx: false,
            generatePdf: false,
            generateXml: true);

        var normalizedOutputDir = invoice.OutputDirectory.Replace('\\', '/');
        var normalizedXmlPath = invoice.XmlPath.Replace('\\', '/');

        Assert.Contains("2026/Invoices", normalizedOutputDir);
        Assert.Equal("20260511_EL_PL", invoice.BaseFilename);
        Assert.EndsWith("2026/Invoices/20260511_EL_PL.xml", normalizedXmlPath);
    }

    private static ClientConfig CreateClient()
    {
        return new ClientConfig
        {
            Key = "EL",
            Name = "Client",
            Address = "Address",
            Vat = "PL1234567890",
            Currency = "PLN",
            VatRate = 23,
            ServiceDescription = "Service",
            ServiceDescriptionUa = "",
            InvoicePrefix = "EL",
            DefaultAmount = 100m,
            MonthOffsetRule = "early_previous",
            Enabled = true,
        };
    }

    private static SupplierConfig CreateSupplier()
    {
        return new SupplierConfig
        {
            Name = "Supplier",
            Tin = "1234567890",
            Address = "Address",
            Iban = "PL00102010260000004270201111",
            Swift = "EXAMPLE1",
        };
    }

    private static OutputConfig CreateOutput()
    {
        return new OutputConfig
        {
            Directory = "./output",
            Pattern = "{year}/Invoices",
            Filename = "{date}_{client}_PL"
        };
    }
}
