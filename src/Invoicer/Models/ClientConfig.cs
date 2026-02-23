namespace Invoicer.Models;

public class ClientConfig
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public string NameUa { get; set; } = "";
    public string Address { get; set; } = "";
    public string AddressUa { get; set; } = "";
    public string Vat { get; set; } = "";
    public string Currency { get; set; } = "PLN";
    public int VatRate { get; set; }
    public string ServiceDescription { get; set; } = "";
    public string ServiceDescriptionUa { get; set; } = "";
    public string InvoicePrefix { get; set; } = "";
    public decimal DefaultAmount { get; set; }
    public string MonthOffsetRule { get; set; } = "early_previous";
    public int LastInvoiceNumber { get; set; }
    public bool Enabled { get; set; } = true;
}
