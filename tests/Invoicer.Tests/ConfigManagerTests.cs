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

    [Fact]
    public void Load_ConfigWithoutUpdateSection_UsesDefaults()
    {
        // A config.toml written by a version that predates the [update] section.
        const string legacyToml = """
            [supplier]
            name = "Supplier"
            tin = "1234567890"

            [output]
            directory = "./output"

            [[clients]]
            key = "EL"
            name = "Client"
            """;

        WithTempConfig(legacyToml, () =>
        {
            var loaded = ConfigManager.Load();

            Assert.True(loaded.Update.CheckOnStartup);
            Assert.Equal("zeusapps/Invoicer", loaded.Update.Repository);
            Assert.Equal("", loaded.Update.DismissedVersion);

            // The section is materialised on the next save.
            ConfigManager.Save(loaded);
            var reloaded = ConfigManager.Load();

            Assert.True(reloaded.Update.CheckOnStartup);
            Assert.Equal("zeusapps/Invoicer", reloaded.Update.Repository);
            Assert.Single(reloaded.Clients);
            Assert.Equal("EL", reloaded.Clients[0].Key);
        });
    }

    [Fact]
    public void SaveAndLoad_RoundTripsUpdateSettings()
    {
        WithTempConfig(null, () =>
        {
            var config = ConfigManager.CreateDefault();
            config.Update.CheckOnStartup = false;
            config.Update.Repository = "someone/Fork";
            config.Update.DismissedVersion = "1.2.0";

            ConfigManager.Save(config);
            var loaded = ConfigManager.Load();

            Assert.False(loaded.Update.CheckOnStartup);
            Assert.Equal("someone/Fork", loaded.Update.Repository);
            Assert.Equal("1.2.0", loaded.Update.DismissedVersion);
        });
    }

    [Fact]
    public void CreateDefault_EnablesStartupCheck()
    {
        var config = ConfigManager.CreateDefault();

        Assert.True(config.Update.CheckOnStartup);
        Assert.Equal("zeusapps/Invoicer", config.Update.Repository);
    }

    private static void WithTempConfig(string? initialContent, Action body)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "invoicer-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalConfigPath = ConfigManager.ConfigPath;

        try
        {
            var path = Path.Combine(tempDir, "config.toml");
            if (initialContent is not null)
                File.WriteAllText(path, initialContent);

            ConfigManager.ConfigPath = path;
            body();
        }
        finally
        {
            ConfigManager.ConfigPath = originalConfigPath;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
