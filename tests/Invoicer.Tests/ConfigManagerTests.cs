using System.Text.RegularExpressions;
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

    [Fact]
    public void SaveAndLoad_RoundTripsBillingAccountsAndClientCountry()
    {
        WithTempConfig(null, () =>
        {
            var config = ConfigManager.CreateDefault();
            config.BillingAccounts =
            [
                new() { Key = "PLN", Label = "PKO PLN", Iban = "PL42109013200000000154701995", Bank = "PKO", Swift = "BPKOPLPW", Currency = "PLN" },
                new() { Key = "PLN2", Label = "mBank PLN", Iban = "PL61114020040000300201355387", Bank = "mBank", Swift = "BREXPLPW", Currency = "PLN" },
                new() { Key = "USD", Label = "Santander USD", Iban = "PL27109010140000071219812874", Bank = "Santander", Swift = "WBKPPLPP", Currency = "USD" },
            ];
            config.Clients[0].Country = "us";
            config.Clients[0].Vat = "";
            config.Clients[0].BillingAccount = "USD";

            ConfigManager.Save(config);
            var toml = File.ReadAllText(ConfigManager.ConfigPath);
            var loaded = ConfigManager.Load();

            Assert.Equal(3, loaded.BillingAccounts.Count);
            for (var i = 0; i < config.BillingAccounts.Count; i++)
            {
                var expected = config.BillingAccounts[i];
                var actual = loaded.BillingAccounts[i];
                Assert.Equal(
                    (expected.Key, expected.Label, expected.Iban, expected.Bank, expected.Swift, expected.Currency),
                    (actual.Key, actual.Label, actual.Iban, actual.Bank, actual.Swift, actual.Currency));
            }

            Assert.Equal("US", loaded.Clients[0].Country);
            Assert.Equal("", loaded.Clients[0].Vat);
            Assert.Equal("USD", loaded.Clients[0].BillingAccount);

            var supplierSection = SectionText(toml, "[supplier]");
            Assert.DoesNotContain("iban", supplierSection);
            Assert.DoesNotContain("bank", supplierSection);
            Assert.DoesNotContain("swift", supplierSection);
            Assert.False(File.Exists(ConfigManager.BackupPath));
        });
    }

    private const string LegacyBankToml = """
        [supplier]
        name = "Supplier"
        tin = "1234567890"
        iban = "PL42109013200000000154701995"
        bank = "Santander"
        swift = "WBKPPLPP"

        [output]
        directory = "./output"

        [[clients]]
        key = "EL"
        name = "Client"
        vat = "PL9999999999"

        [[clients]]
        key = "GREEK"
        name = "Greek client"
        vat = "EL123456789"

        [[clients]]
        key = "GREENFLOW"
        name = "Green Flow Solutions"
        vat = "N/A"

        [[clients]]
        key = "EXPLICIT"
        name = "Explicit"
        country = "US"
        vat = "PL9999999999"
        """;

    [Fact]
    public void Load_LegacySupplierBankDetails_MigratesToDefaultAccount()
    {
        WithTempConfig(LegacyBankToml, () =>
        {
            var loaded = ConfigManager.Load();

            var account = Assert.Single(loaded.BillingAccounts);
            Assert.Equal("DEFAULT", account.Key);
            Assert.Equal("Default", account.Label);
            Assert.Equal("PL42109013200000000154701995", account.Iban);
            Assert.Equal("Santander", account.Bank);
            Assert.Equal("WBKPPLPP", account.Swift);
            Assert.Equal("", account.Currency);
            Assert.All(loaded.Clients, c => Assert.Equal("DEFAULT", c.BillingAccount));

            ConfigManager.Save(loaded);
            var toml = File.ReadAllText(ConfigManager.ConfigPath);

            Assert.Contains("[[billing_accounts]]", toml);
            Assert.Equal(4, Regex.Matches(toml, "billing_account = \"DEFAULT\"").Count);
            Assert.DoesNotContain("iban", SectionText(toml, "[supplier]"));
        });
    }

    [Fact]
    public void Load_LegacyClients_InferCountryOnlyFromEuVatPrefix()
    {
        WithTempConfig(LegacyBankToml, () =>
        {
            var loaded = ConfigManager.Load();

            Assert.Equal("PL", loaded.Clients.Single(c => c.Key == "EL").Country);
            Assert.Equal("GR", loaded.Clients.Single(c => c.Key == "GREEK").Country);
            Assert.Equal("", loaded.Clients.Single(c => c.Key == "GREENFLOW").Country);
            Assert.Equal("US", loaded.Clients.Single(c => c.Key == "EXPLICIT").Country);
        });
    }

    [Fact]
    public void Load_ConfigWithBillingAccounts_IsNotMigrated()
    {
        const string toml = """
            [supplier]
            name = "Supplier"
            iban = "PL42109013200000000154701995"

            [[billing_accounts]]
            key = "USD"
            label = "USD"
            iban = "PL27109010140000071219812874"

            [[clients]]
            key = "A"
            billing_account = "USD"

            [[clients]]
            key = "B"
            """;

        WithTempConfig(toml, () =>
        {
            var loaded = ConfigManager.Load();

            Assert.Equal("USD", Assert.Single(loaded.BillingAccounts).Key);
            Assert.Equal("USD", loaded.Clients[0].BillingAccount);
            Assert.Equal("", loaded.Clients[1].BillingAccount);

            ConfigManager.Save(loaded);
            Assert.False(File.Exists(ConfigManager.BackupPath));
        });
    }

    [Fact]
    public void Save_AfterLegacyMigration_BacksUpOriginalOnce()
    {
        WithTempConfig(LegacyBankToml, () =>
        {
            var original = File.ReadAllText(ConfigManager.ConfigPath);
            var loaded = ConfigManager.Load();

            ConfigManager.Save(loaded);
            Assert.Equal(original, File.ReadAllText(ConfigManager.BackupPath));

            loaded.Clients[0].Name = "Renamed";
            ConfigManager.Save(loaded);
            Assert.Equal(original, File.ReadAllText(ConfigManager.BackupPath));
        });
    }

    [Fact]
    public void CreateDefault_AssignsSampleClientAccountAndCountry()
    {
        var config = ConfigManager.CreateDefault();

        var account = Assert.Single(config.BillingAccounts);
        Assert.Equal(account.Key, config.Clients[0].BillingAccount);
        Assert.Equal("PL", config.Clients[0].Country);
    }

    [Fact]
    public void ResolveBillingAccount_FindsAssignedAccount()
    {
        var config = new AppConfig { BillingAccounts = [new() { Key = "PLN" }, new() { Key = "USD" }] };
        var client = new ClientConfig { Key = "GREENFLOW", BillingAccount = "USD" };

        Assert.Same(config.BillingAccounts[1], config.ResolveBillingAccount(client));
        Assert.Same(config.BillingAccounts[1], config.FindBillingAccount(client));
    }

    [Theory]
    [InlineData("")]
    [InlineData("OLD")]
    public void ResolveBillingAccount_ThrowsForEmptyOrUnknownKey(string key)
    {
        var config = new AppConfig { BillingAccounts = [new() { Key = "PLN" }] };
        var client = new ClientConfig { Key = "GREENFLOW", BillingAccount = key };

        var ex = Assert.Throws<BillingAccountNotFoundException>(() => config.ResolveBillingAccount(client));

        Assert.Equal("GREENFLOW", ex.ClientKey);
        Assert.Equal(key, ex.AccountKey);
        Assert.Contains("GREENFLOW", ex.Message);
        if (key.Length > 0)
            Assert.Contains(key, ex.Message);
        Assert.Null(config.FindBillingAccount(client));
    }

    private static string SectionText(string toml, string header)
    {
        var start = toml.IndexOf(header, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Section {header} not found.");
        var next = toml.IndexOf("\n[", start + header.Length, StringComparison.Ordinal);
        return next < 0 ? toml[start..] : toml[start..next];
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
