using DocumentFormat.OpenXml.Packaging;
using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class InvoiceDocumentTextTests
{
    private const string UsdIban = "PL27109010140000071219812874";

    [Fact]
    public void CustomerBlock_IncludesVatLine_WhenClientHasVat()
    {
        var invoice = CreateInvoice(vat: "DE123456789");

        Assert.Contains("VAT: DE123456789", InvoiceText.CustomerBlock(invoice).Split('\n'));
    }

    [Fact]
    public void CustomerBlock_OmitsVatLine_WhenClientHasNoVat()
    {
        var invoice = CreateInvoice(vat: "");

        var lines = InvoiceText.CustomerBlock(invoice).Split('\n');

        Assert.DoesNotContain(lines, l => l.StartsWith("VAT", StringComparison.Ordinal));
        Assert.Equal(
            new[] { "Green Flow Solutions / Грін Флоу", "123 River St Newton, MA 02465", "Ньютон, Массачусетс" },
            lines);
    }

    [Fact]
    public void BankAccountBlock_UsesBillingAccount()
    {
        var invoice = CreateInvoice(vat: "");

        Assert.Equal(
            $"IBAN: {UsdIban}\nBank: Santander\nSWIFT: WBKPPLPP",
            InvoiceText.BankAccountBlock(invoice));
    }

    [Theory]
    [InlineData("DE123456789", true)]
    [InlineData("", false)]
    public void Docx_ShowsVatLineOnlyWhenPresent_AndAccountIban(string vat, bool expectVatLine)
    {
        var invoice = CreateInvoice(vat);

        DocxGenerator.Generate(invoice);

        using var doc = WordprocessingDocument.Open(invoice.DocxPath, false);
        var paragraphs = doc.MainDocumentPart!.Document!.Body!
            .Descendants<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
            .Select(p => p.InnerText)
            .ToList();

        Assert.Equal(expectVatLine, paragraphs.Contains($"VAT: {vat}"));
        Assert.DoesNotContain(paragraphs, p => p == "VAT: ");
        Assert.Contains($"IBAN: {UsdIban}", paragraphs);
    }

    [Theory]
    [InlineData("DE123456789")]
    [InlineData("")]
    public void Pdf_GeneratesForClientWithAndWithoutVat(string vat)
    {
        // The PDF layout takes its customer and bank text from InvoiceText, covered above.
        var invoice = CreateInvoice(vat);

        PdfGenerator.Generate(invoice);

        Assert.True(new FileInfo(invoice.PdfPath).Length > 0);
    }

    private static Invoice CreateInvoice(string vat)
    {
        var client = new ClientConfig
        {
            Key = "GREENFLOW",
            Name = "Green Flow Solutions",
            NameUa = "Грін Флоу",
            Address = "123 River St Newton, MA 02465",
            AddressUa = "Ньютон, Массачусетс",
            Country = "US",
            Vat = vat,
            Currency = "USD",
            ServiceDescription = "Consulting services rendered",
            InvoicePrefix = "GF",
            BillingAccount = "USD",
        };

        var account = new BillingAccountConfig
        {
            Key = "USD",
            Label = "Santander USD",
            Iban = UsdIban,
            Bank = "Santander",
            Swift = "WBKPPLPP",
            Currency = "USD",
        };

        var output = new OutputConfig
        {
            Directory = Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N")),
            Pattern = "{year}/Invoices",
            Filename = "{date}_{client}",
        };

        return Invoice.Create(
            client,
            new SupplierConfig { Name = "Supplier", Tin = "1111111111", Address = "Address" },
            account,
            output,
            invoiceNumber: 1,
            invoiceDate: new DateTime(2026, 9, 12),
            amount: 2500m);
    }
}
