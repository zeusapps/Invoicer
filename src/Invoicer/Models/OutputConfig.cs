namespace Invoicer.Models;

public class OutputConfig
{
    public string Directory { get; set; } = "./output";
    public string Pattern { get; set; } = "{year}/Invoices";
    public string Filename { get; set; } = "{date}_{client}_PL";
}
