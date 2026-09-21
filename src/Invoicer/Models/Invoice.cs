namespace Invoicer.Models;

public class Invoice
{
    public required ClientConfig Client { get; set; }
    public required SupplierConfig Supplier { get; set; }
    public required BillingAccountConfig BillingAccount { get; set; }

    public string InvoiceNumber { get; set; } = "";
    public string FormattedNumber { get; set; } = "";
    public DateTime InvoiceDate { get; set; }
    public DateTime ServiceMonth { get; set; }

    public decimal NetAmount { get; set; }
    public int VatRate { get; set; }
    public decimal VatAmount { get; set; }
    public decimal GrossAmount { get; set; }
    public string Currency { get; set; } = "PLN";

    /// <summary>
    /// PLN per one unit of <see cref="Currency"/>, at the precision NBP published it.
    /// Null for a PLN invoice, and for a foreign-currency invoice whose rate could not be resolved.
    /// </summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>Effective date of the NBP table <see cref="ExchangeRate"/> came from, or null when it was entered by hand.</summary>
    public DateTime? ExchangeRateDate { get; set; }

    /// <summary>Identifier of that NBP table (for example <c>182/A/NBP/2026</c>), or null when the rate was entered by hand.</summary>
    public string? ExchangeRateTable { get; set; }

    public string ServiceDescription { get; set; } = "";
    public string ServiceDescriptionUa { get; set; } = "";

    public bool GenerateDocx { get; set; } = true;
    public bool GeneratePdf { get; set; } = true;
    public bool GenerateXml { get; set; }

    public string OutputDirectory { get; set; } = "";
    public string BaseFilename { get; set; } = "";
    public string DocxPath => Path.Combine(OutputDirectory, BaseFilename + ".docx");
    public string PdfPath => Path.Combine(OutputDirectory, BaseFilename + ".pdf");
    public string XmlPath => Path.Combine(OutputDirectory, BaseFilename + ".xml");

    public static Invoice Create(
        ClientConfig client,
        SupplierConfig supplier,
        BillingAccountConfig billingAccount,
        OutputConfig output,
        int invoiceNumber,
        DateTime invoiceDate,
        decimal amount,
        bool generateDocx = true,
        bool generatePdf = true,
        bool generateXml = false,
        decimal? exchangeRate = null,
        DateTime? exchangeRateDate = null,
        string? exchangeRateTable = null)
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
            BillingAccount = billingAccount,
            InvoiceNumber = invoiceNumber.ToString(),
            FormattedNumber = formattedNumber,
            InvoiceDate = invoiceDate,
            ServiceMonth = serviceMonth,
            NetAmount = netAmount,
            VatRate = vatRate,
            VatAmount = vatAmount,
            GrossAmount = grossAmount,
            Currency = client.Currency,
            ExchangeRate = exchangeRate,
            ExchangeRateDate = exchangeRateDate,
            ExchangeRateTable = exchangeRateTable,
            ServiceDescription = client.ServiceDescription,
            ServiceDescriptionUa = client.ServiceDescriptionUa,
            GenerateDocx = generateDocx,
            GeneratePdf = generatePdf,
            GenerateXml = generateXml,
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
            .Replace("{date}", date.ToString("yyyyMMdd"))
            .Replace("{client}", client.Key);
    }
}
