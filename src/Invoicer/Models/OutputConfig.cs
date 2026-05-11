namespace Invoicer.Models;

public class OutputConfig
{
    public string Directory { get; set; } = "./output";
    public string Pattern { get; set; } = "{year}/Invoices";
    public string Filename { get; set; } = "{date}_{client}_PL";
    public bool GenerateDocxByDefault { get; set; } = true;
    public bool GeneratePdfByDefault { get; set; } = true;
    public bool GenerateXmlByDefault { get; set; } = false;
}
