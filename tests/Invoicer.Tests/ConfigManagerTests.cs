using Invoicer.Config;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class ConfigManagerTests
{
    [Fact]
    public void SaveAndLoad_RoundTripsXmlDefaultOutputFlag()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "invoicer-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var testConfigPath = Path.Combine(tempDir, "config.toml");

        var originalConfigPath = ConfigManager.ConfigPath;

        try
        {
            ConfigManager.ConfigPath = testConfigPath;

            var config = new AppConfig
            {
                Supplier = new SupplierConfig
                {
                    Name = "Supplier",
                    NameUa = "",
                    Tin = "1234567890",
                    Regon = "",
                    Vat = "",
                    Address = "Address",
                    AddressUa = "",
                    Iban = "PL00102010260000004270201111",
                    Bank = "",
                    Swift = "EXAMPLE1",
                },
                Output = new OutputConfig
                {
                    Directory = "./output",
                    Pattern = "{year}/Invoices",
                    Filename = "{date}_{client}_PL",
                    GenerateDocxByDefault = true,
                    GeneratePdfByDefault = false,
                    GenerateXmlByDefault = true,
                },
                Clients = new List<ClientConfig>
                {
                    new()
                    {
                        Key = "EL",
                        Name = "Client",
                        NameUa = "",
                        Address = "Address",
                        AddressUa = "",
                        Vat = "PL1234567890",
                        Currency = "PLN",
                        VatRate = 23,
                        ServiceDescription = "Service",
                        ServiceDescriptionUa = "",
                        InvoicePrefix = "EL",
                        DefaultAmount = 100m,
                        MonthOffsetRule = "early_previous",
                        LastInvoiceNumber = 0,
                        Enabled = true,
                    },
                }
            };

            ConfigManager.Save(config);
            var loaded = ConfigManager.Load();

            Assert.True(loaded.Output.GenerateXmlByDefault);
            Assert.True(loaded.Output.GenerateDocxByDefault);
            Assert.False(loaded.Output.GeneratePdfByDefault);
        }
        finally
        {
            ConfigManager.ConfigPath = originalConfigPath;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
