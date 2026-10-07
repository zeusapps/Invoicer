using Invoicer.Config;
using Invoicer.Models;

namespace Invoicer.Generation;

public static class InvoiceGeneration
{
    public static IReadOnlyList<string> Generate(Invoice invoice, TimeProvider? clock = null)
    {
        if (!invoice.GenerateDocx && !invoice.GeneratePdf && !invoice.GenerateXml)
            throw new InvoiceValidationException(["Select at least one output format."]);
        var prepared = InvoicePreparation.Prepare(invoice);
        if (string.IsNullOrWhiteSpace(invoice.BillingAccount.Iban))
            throw new InvoiceValidationException([$"Billing account '{invoice.BillingAccount.Key}' IBAN is required."]);
        if (invoice.GenerateXml)
        {
            var errors = KsefXmlGenerator.Validate(invoice);
            if (errors.Count > 0) throw new KsefValidationException(errors);
        }
        var paths = new List<string>();
        if (invoice.GenerateDocx) { DocxGenerator.GeneratePrepared(invoice, prepared); paths.Add(invoice.DocxPath); }
        if (invoice.GeneratePdf) { PdfGenerator.GeneratePrepared(invoice, prepared); paths.Add(invoice.PdfPath); }
        if (invoice.GenerateXml) { KsefXmlGenerator.GeneratePrepared(invoice, prepared, clock); paths.Add(invoice.XmlPath); }
        return paths;
    }

    /// <summary>Resolve payment and complete generation before changing saved user defaults or numbering.</summary>
    public static (Invoice Invoice, IReadOnlyList<string> Paths) CreateAndGenerate(
        AppConfig config, ClientConfig client, int number, DateTime date, decimal amount,
        bool docx, bool pdf, bool xml, decimal? rate = null, DateTime? rateDate = null, string? rateTable = null,
        TimeProvider? clock = null)
    {
        var invoice = Invoice.Create(client, config.Supplier, config.ResolveBillingAccount(client), config.Output,
            number, date, amount, docx, pdf, xml, rate, rateDate, rateTable);
        var paths = Generate(invoice, clock);
        client.LastInvoiceNumber = number;
        config.Output.GenerateDocxByDefault = docx;
        config.Output.GeneratePdfByDefault = pdf;
        config.Output.GenerateXmlByDefault = xml;
        ConfigManager.Save(config);
        return (invoice, paths);
    }
}
