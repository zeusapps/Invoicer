namespace Invoicer.Models;

public class Invoice
{
    public required ClientConfig Client { get; set; }
    public required SupplierConfig Supplier { get; set; }

    public string InvoiceNumber { get; set; } = "";
    public string FormattedNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime ServiceMonth { get; set; }

    public decimal NetAmount { get; set; }
    public int VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    public string ServiceDescription { get; set; } = "";
    public string ServiceDescriptionUa { get; set; } = "";

    public bool GenerateDocx { get; set; } = true;
    public bool GeneratePdf { get; set; } = true;

    public string OutputDirectory { get; set; } = "";
    public string BaseFilename { get; set; } = "";
    public string DocxPath => Path.Combine(OutputDirectory, BaseFilename + ".docx");
    public string PdfPath => Path.Combine(OutputDirectory, BaseFilename + ".pdf");

    public static Invoice Create(
        ClientConfig client,
        SupplierConfig supplier,
        OutputConfig output,
        int invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        bool generateDocx = true,
        bool generatePdf = true)
    {
        var serviceMonth = CalculateServiceMonth(invoiceDate, client.MonthOffsetRule);
        var netAmount = amount;
        var vatRate = client.VatRate;
        var vatAmount = Math.Round(netAmount * vatRate / 100m, 2);
        var grossAmount = netAmount + vatAmount;

        var formattedNumber = FormatInvoiceNumber(client.InvoicePrefix, invoiceNumber, invoiceDate);
        var outputDir = ResolveOutputDirectory(output, invoiceDate);
        var filename = ResolveFilename(output, client, invoiceDate);

        return new Invoice
        {
            Client = client,
            Supplier = supplier,
            InvoiceNumber = invoiceNumber.ToString(),
            FormattedNumber = formattedNumber,
            InvoiceDate = invoiceDate,
            ServiceMonth = serviceMonth,
            NetAmount = netAmount,
            VatRate = vatRate,
            VatAmount = vatAmount,
            GrossAmount = grossAmount,
            Currency = client.Currency,
            ServiceDescription = client.ServiceDescription,
            ServiceDescriptionUa = client.ServiceDescriptionUa,
            GenerateDocx = generateDocx,
            GeneratePdf = generatePdf,
            OutputDirectory = outputDir,
            BaseFilename = filename,
        };
    }

    public static DateTime CalculateServiceMonth(DateTime invoiceDate, string rule)
    {
        return rule switch
        {
            "early_previous" => invoiceDate.Day <= 20
                ? invoiceDate.AddMonths(-1)
                : invoiceDate,
            "early_current" => invoiceDate.Day <= 20
                ? invoiceDate
                : invoiceDate.AddMonths(1),
            _ => invoiceDate,
        };
    }

    private static string FormatInvoiceNumber(string prefix, int number, DateTime date)
    {
        return $"{date:yyyy}/{prefix}/{number:D4}";
    }

    private static string ResolveOutputDirectory(OutputConfig output, DateTime date)
    {
        var subPath = output.Pattern
            .Replace("{year}", date.Year.ToString());
        return Path.Combine(output.Directory, subPath);
    }

    private static string ResolveFilename(OutputConfig output, ClientConfig client, DateTime date)
    {
        return output.Filename
            .Replace("{date}", date.ToString("yyyy-MM-dd"))
            .Replace("{client}", client.Key);
    }
}
