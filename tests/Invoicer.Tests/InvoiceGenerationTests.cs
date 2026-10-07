using DocumentFormat.OpenXml.Packaging;
using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class InvoiceGenerationTests
{
    [Theory]
    [InlineData("missing rate")]
    [InlineData("precise rate")]
    [InlineData("buyer")]
    [InlineData("seller")]
    [InlineData("stale rate")]
    [InlineData("gross")]
    [InlineData("PL currency")]
    public void PreflightPreservesEveryExistingFile(string problem)
    {
        var invoice = Samples.Invoice(Samples.Client(problem == "PL currency" ? "PL" : "EE"));
        switch (problem)
        {
            case "missing rate": invoice.ExchangeRate = null; break;
            case "precise rate": invoice.ExchangeRate = .00021425m; break;
            case "buyer": invoice.Client.Vat = ""; break;
            case "seller": invoice.Supplier.Vat = ""; break;
            case "stale rate": invoice.Client.VatRate = 23; break;
            case "gross": invoice.GrossAmount++; break;
            case "PL currency": invoice.Currency = "USD"; break;
        }
        Assert.ThrowsAny<Exception>(() => InvoiceGeneration.Generate(invoice));
        Assert.False(Directory.Exists(invoice.OutputDirectory));
        Directory.CreateDirectory(invoice.OutputDirectory);
        var paths = new[] { invoice.DocxPath, invoice.PdfPath, invoice.XmlPath };
        foreach (var path in paths) File.WriteAllText(path, "sentinel " + Path.GetExtension(path));
        Assert.ThrowsAny<Exception>(() => InvoiceGeneration.Generate(invoice));
        foreach (var path in paths) Assert.Equal("sentinel " + Path.GetExtension(path), File.ReadAllText(path));
    }

    [Theory]
    [InlineData("PL")]
    [InlineData("EE")]
    [InlineData("US")]
    public void DocumentsWithoutXmlNeedNoExchangeRate(string country)
    {
        var invoice = Samples.Invoice(Samples.Client(country));
        invoice.Currency = "USD"; invoice.ExchangeRate = null; invoice.GenerateXml = false;
        Assert.Equal(2, InvoiceGeneration.Generate(invoice).Count);
        Assert.True(File.Exists(invoice.DocxPath)); Assert.True(File.Exists(invoice.PdfPath));
        Assert.False(File.Exists(invoice.XmlPath));
    }

    [Theory]
    [InlineData("PDF", "tax")]
    [InlineData("DOCX", "tax")]
    [InlineData("XML", "tax")]
    [InlineData("PDF", "buyer")]
    [InlineData("DOCX", "buyer")]
    [InlineData("XML", "buyer")]
    [InlineData("PDF", "gross")]
    [InlineData("DOCX", "gross")]
    [InlineData("XML", "gross")]
    public void DirectGeneratorsValidateBeforeWriting(string format, string problem)
    {
        var invoice = Samples.Invoice(Samples.Client("EE"));
        switch (problem)
        {
            case "tax": invoice.Client.VatRate = 23; break;
            case "buyer": invoice.Client.Vat = "DE123456789"; break;
            case "gross": invoice.GrossAmount++; break;
        }
        var path = format switch { "PDF" => invoice.PdfPath, "DOCX" => invoice.DocxPath, _ => invoice.XmlPath };
        Action generate = format switch
        {
            "PDF" => () => PdfGenerator.Generate(invoice),
            "DOCX" => () => DocxGenerator.Generate(invoice),
            _ => () => KsefXmlGenerator.Generate(invoice),
        };
        Assert.ThrowsAny<Exception>(generate);
        Assert.False(Directory.Exists(invoice.OutputDirectory));
        Directory.CreateDirectory(invoice.OutputDirectory); File.WriteAllText(path, "original");
        Assert.ThrowsAny<Exception>(generate); Assert.Equal("original", File.ReadAllText(path));
    }

    [Theory]
    [InlineData("PL", null, false)]
    [InlineData("EE", null, true)]
    [InlineData("US", null, false)]
    [InlineData("US", true, true)]
    public void SharedTextAndDocxAgreeOnTreatmentAndZeroAmounts(string country, bool? choice, bool reverse)
    {
        var invoice = Samples.Invoice(Samples.Client(country, choice));
        var prepared = InvoicePreparation.Prepare(invoice);
        Assert.Equal(reverse, prepared.Treatment.ReverseCharge);
        Assert.Equal(country == "PL" ? "23%" : "NP - not subject to Polish VAT", InvoiceText.TaxLabel(prepared.Treatment));
        DocxGenerator.Generate(invoice);
        using var doc = WordprocessingDocument.Open(invoice.DocxPath, false);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var text = body.InnerText;
        var validation = new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc).ToList();
        Assert.True(validation.Count == 0, string.Join("\n", validation.Select(e => e.Description)));
        Assert.Contains(InvoiceText.TaxLabel(prepared.Treatment), text);
        Assert.Contains(InvoiceText.FormatAmount(prepared.Amounts.Vat, invoice.Currency), text);
        Assert.Equal(reverse, text.Contains(InvoiceText.ReverseChargeAnnotation));
        Assert.Equal(reverse, text.Contains(InvoiceText.ReverseChargeExplanationUa));
        Assert.DoesNotContain("N/A", text);
        Assert.DoesNotContain("0%", text);
        Assert.Contains("IBAN:", text); Assert.Contains(invoice.ServiceDescriptionUa, text);
        if (country == "US") Assert.DoesNotContain("VAT: ", text);
        if (country == "EE") Assert.Contains("VAT: EE123456789", text);
        if (country != "PL") Assert.Contains(InvoiceText.NpExplanationUa, text);
        var size = body.Descendants<DocumentFormat.OpenXml.Wordprocessing.PageSize>().Single();
        Assert.Equal(11906u, size.Width!.Value); Assert.Equal(16838u, size.Height!.Value);
    }
}
