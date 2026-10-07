using Invoicer.Config;
using Invoicer.Models;
using Invoicer.Generation;
using Xunit;

namespace Invoicer.Tests;

public partial class ConfigManagerTests
{
    [Theory]
    [InlineData("US", "", null, false)]
    [InlineData("US", "", true, true)]
    [InlineData("US", "", false, false)]
    [InlineData("EE", "EE123456789", null, null)]
    [InlineData("EE", "EE123456789", true, true)]
    [InlineData("EE", "EE123456789", false, false)]
    [InlineData("PL", "PL9999999999", null, null)]
    [InlineData("PL", "PL9999999999", false, false)]
    [InlineData("PL", "PL9999999999", true, true)]
    [InlineData("GB", "", null, null)]
    [InlineData("GB", "", true, true)]
    [InlineData("GB", "", false, false)]
    [InlineData("", "EE123456789", null, null)]
    [InlineData("", "PL9999999999", null, null)]
    [InlineData("", "12-3456789", null, null)]
    [InlineData("ZZ", "", null, null)]
    public void ReverseChargeMigrationPreservesOtherFields(string country, string vat, bool? choice, bool? expected)
    {
        WithTaxConfigPath(() =>
        {
            var text = $$"""
                [supplier]
                tin = "1111111111"
                iban = "PL00102010260000004270201111"
                bank = "Legacy Bank"
                [output]
                directory = "./output"
                pattern = "{year}/Invoices"
                filename = "{date}_{client}"
                [[clients]]
                key = "TEST"
                country = "{{country}}"
                vat = "{{vat}}"
                currency = "EUR"
                vat_rate = 0
                last_invoice_number = 42
                invoice_prefix = "TEST"
                default_amount = 1234.56
                enabled = false
                service_description = "Services"
                """;
            if (choice.HasValue) text += $"\nreverse_charge = {choice.Value.ToString().ToLowerInvariant()}\n";
            File.WriteAllText(ConfigManager.ConfigPath, text);
            var loaded = ConfigManager.Load();
            var client = Assert.Single(loaded.Clients);
            Assert.Equal(expected, client.ReverseCharge);
            Assert.Equal(country.Length > 0 ? country : Countries.FromEuVatPrefix(vat.Length >= 2 ? vat[..2] : "") ?? "", client.Country);
            Assert.Equal(42, client.LastInvoiceNumber);
            Assert.Equal(1234.56m, client.DefaultAmount);
            Assert.Equal("EUR", client.Currency);
            Assert.Equal("TEST", client.InvoicePrefix);
            Assert.Equal("Services", client.ServiceDescription);
            Assert.Equal(vat, client.Vat);
            Assert.False(client.Enabled);
            Assert.Equal("DEFAULT", client.BillingAccount);
            Assert.Equal("Legacy Bank", Assert.Single(loaded.BillingAccounts).Bank);
            ConfigManager.Save(loaded);
            Assert.Equal(text, File.ReadAllText(ConfigManager.BackupPath));
            var first = File.ReadAllText(ConfigManager.ConfigPath);
            Assert.Equal(expected.HasValue, first.Contains("reverse_charge ="));
            if (expected.HasValue) Assert.Contains($"reverse_charge = {expected.Value.ToString().ToLowerInvariant()}", first);
            loaded = ConfigManager.Load();
            Assert.Equal(expected, loaded.Clients[0].ReverseCharge);
            ConfigManager.Save(loaded);
            Assert.Equal(first, File.ReadAllText(ConfigManager.ConfigPath));
        });
    }

    [Theory]
    [InlineData("\"false\"")]
    [InlineData("1")]
    [InlineData("[true]")]
    public void ReverseChargeRequiresAnActualTomlBoolean(string value)
    {
        WithTaxConfigPath(() =>
        {
            File.WriteAllText(ConfigManager.ConfigPath, $"[[clients]]\nkey = \"BAD\"\ncountry = \"US\"\nreverse_charge = {value}\n");
            var error = Assert.Throws<FormatException>(() => ConfigManager.Load());
            Assert.Contains("BAD", error.Message); Assert.Contains("TOML boolean", error.Message);
        });
    }

    [Fact]
    public void InMemoryUsDefaultPersistsWhileGbRemainsUnresolved()
    {
        WithTaxConfigPath(() =>
        {
            var config = ConfigManager.CreateDefault();
            config.Clients = [Samples.Client("US"), Samples.Client("GB")];
            Assert.False(InvoiceTaxTreatment.ForClient(config.Clients[0]).ReverseCharge);
            Assert.Throws<InvoiceValidationException>(() => InvoiceTaxTreatment.ForClient(config.Clients[1]));
            ConfigManager.Save(config);
            var loaded = ConfigManager.Load();
            Assert.False(loaded.Clients[0].ReverseCharge); Assert.Null(loaded.Clients[1].ReverseCharge);
        });
    }

    private static void WithTaxConfigPath(Action action)
    {
        var original = ConfigManager.ConfigPath;
        var directory = Path.Combine(Path.GetTempPath(), "invoicer-config-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { ConfigManager.ConfigPath = Path.Combine(directory, "config.toml"); action(); }
        finally { ConfigManager.ConfigPath = original; Directory.Delete(directory, true); }
    }

    [Theory]
    [InlineData("rate")]
    [InlineData("tax")]
    [InlineData("identifier")]
    [InlineData("account")]
    public void FailedWorkflowPreservesNumberingAndPreferencesInMemoryAndOnDisk(string problem)
    {
        WithTaxConfigPath(() =>
        {
            var source = Samples.Invoice(Samples.Client("EE"));
            var client = source.Client; client.LastInvoiceNumber = 42;
            var config = new AppConfig
            {
                Supplier = source.Supplier, Clients = [client], BillingAccounts = [source.BillingAccount],
                Output = new OutputConfig { Directory = source.OutputDirectory, GenerateDocxByDefault = false,
                    GeneratePdfByDefault = false, GenerateXmlByDefault = false },
            };
            ConfigManager.Save(config);
            var original = File.ReadAllText(ConfigManager.ConfigPath);
            switch (problem)
            {
                case "tax": client.VatRate = 23; break;
                case "identifier": client.Vat = ""; break;
                case "account": client.BillingAccount = "UNKNOWN"; break;
            }
            Assert.ThrowsAny<Exception>(() => InvoiceGeneration.CreateAndGenerate(
                config, client, 43, source.InvoiceDate, 1000, true, true, true));
            Assert.Equal(42, client.LastInvoiceNumber);
            Assert.False(config.Output.GenerateDocxByDefault); Assert.False(config.Output.GeneratePdfByDefault);
            Assert.False(config.Output.GenerateXmlByDefault);
            Assert.Equal(original, File.ReadAllText(ConfigManager.ConfigPath));
            Assert.False(Directory.Exists(config.Output.Directory));
            // A later unrelated save must not accidentally persist the failed format selection.
            ConfigManager.Save(config);
            var loaded = ConfigManager.Load();
            Assert.Equal(42, loaded.Clients[0].LastInvoiceNumber);
            Assert.False(loaded.Output.GenerateDocxByDefault); Assert.False(loaded.Output.GeneratePdfByDefault);
            Assert.False(loaded.Output.GenerateXmlByDefault);
        });
    }

    [Fact]
    public void SuccessfulWorkflowPersistsChosenNumberAndFormats()
    {
        WithTaxConfigPath(() =>
        {
            var source = Samples.Invoice(Samples.Client("US"));
            var config = new AppConfig
            {
                Supplier = source.Supplier, Clients = [source.Client], BillingAccounts = [source.BillingAccount],
                Output = new OutputConfig { Directory = source.OutputDirectory },
            };
            var (invoice, paths) = InvoiceGeneration.CreateAndGenerate(
                config, source.Client, 7, source.InvoiceDate, 1000, false, true, true, 3.7998m);
            Assert.Equal("2026/US/0007", invoice.FormattedNumber);
            Assert.Equal(new[] { invoice.PdfPath, invoice.XmlPath }, paths);
            Assert.All(paths, path => Assert.True(File.Exists(path)));
            var loaded = ConfigManager.Load();
            Assert.Equal(7, loaded.Clients[0].LastInvoiceNumber);
            Assert.False(loaded.Output.GenerateDocxByDefault); Assert.True(loaded.Output.GeneratePdfByDefault);
            Assert.True(loaded.Output.GenerateXmlByDefault);
        });
    }

    [Fact]
    public void GenerateDocumentSamples()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Invoicer.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var readme = File.ReadAllText(Path.Combine(root.FullName, "README.md"));
        var match = System.Text.RegularExpressions.Regex.Match(readme,
            @"(?s)<!-- tax-treatment-sample-start -->\s*```toml\r?\n(.*?)\r?\n```\s*<!-- tax-treatment-sample-end -->");
        Assert.True(match.Success, "README must include the complete sample TOML.");
        WithTaxConfigPath(() =>
        {
            File.WriteAllText(ConfigManager.ConfigPath, match.Groups[1].Value);
            var config = ConfigManager.Load();
            var sampleDirectory = Environment.GetEnvironmentVariable("INVOICER_SAMPLE_DIRECTORY");
            config.Output.Directory = string.IsNullOrWhiteSpace(sampleDirectory)
                ? Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N"))
                : Path.GetFullPath(sampleDirectory);
            Assert.Equal(new[] { "Domestic Polish VAT", "EU B2B reverse charge", "Outside Polish VAT - no reverse charge" },
                config.Clients.Select(c => InvoiceTaxTreatment.ForClient(c).Label));
            var usOverride = System.Text.Json.JsonSerializer.Deserialize<ClientConfig>(
                System.Text.Json.JsonSerializer.Serialize(config.Clients[2]))!;
            usOverride.Key = "USA_RC"; usOverride.InvoicePrefix = "USRC"; usOverride.ReverseCharge = true;
            config.Clients.Add(usOverride);
            var original = File.ReadAllText(ConfigManager.ConfigPath);
            foreach (var client in config.Clients)
            {
                var rate = client.Currency == "EUR" ? 4.25m : client.Currency == "USD" ? 3.7998m : (decimal?)null;
                var invoice = Invoice.Create(client, config.Supplier, config.ResolveBillingAccount(client), config.Output,
                    1, new DateTime(2026, 10, 7), 1000m, true, true, true, rate);
                var paths = InvoiceGeneration.Generate(invoice, new SampleClock());
                Assert.Equal(3, paths.Count); Assert.All(paths, p => Assert.True(new FileInfo(p).Length > 0));
                Assert.Empty(KsefSchemaValidator.Validate(invoice.XmlPath));
                using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(invoice.DocxPath, false);
                Assert.Empty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(doc));
                var text = doc.MainDocumentPart!.Document!.InnerText;
                Assert.Equal(InvoiceTaxTreatment.ForClient(client).ReverseCharge, text.Contains(InvoiceText.ReverseChargeAnnotation));
                Assert.Contains(InvoiceText.TaxLabel(InvoiceTaxTreatment.ForClient(client)), text);
                Assert.Contains(InvoiceText.FormatAmount(invoice.VatAmount, invoice.Currency), text);
                Assert.Equal(0, client.LastInvoiceNumber);
            }
            Assert.Equal(original, File.ReadAllText(ConfigManager.ConfigPath));
        });
    }
}
