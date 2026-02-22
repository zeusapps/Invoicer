namespace Invoicer.Models;

public class AppConfig
{
    public SupplierConfig Supplier { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public List<ClientConfig> Clients { get; set; } = new();
}
