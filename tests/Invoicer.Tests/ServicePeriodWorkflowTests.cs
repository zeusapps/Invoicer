using Invoicer.Config;
using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public partial class ConfigManagerTests
{
    [Theory]
    [InlineData("reversed")]
    [InlineData("partial")]
    [InlineData("uninitialized")]
    [InlineData("schema")]
    public void InvalidServicePeriodPreservesNumberingPreferencesAndConfig(string problem)
    {
        WithTaxConfigPath(() =>
        {
            var source = Samples.Invoice(Samples.Client("PL"));
            source.Client.LastInvoiceNumber = 42;
            var config = new AppConfig
            {
                Supplier = source.Supplier, Clients = [source.Client], BillingAccounts = [source.BillingAccount],
                Output = new OutputConfig { Directory = source.OutputDirectory, Pattern = "", GenerateDocxByDefault = false,
                    GeneratePdfByDefault = false, GenerateXmlByDefault = false },
            };
            ConfigManager.Save(config);
            var original = File.ReadAllText(ConfigManager.ConfigPath);
            Directory.CreateDirectory(source.OutputDirectory);
            var probe = Invoice.Create(source.Client, source.Supplier, source.BillingAccount, config.Output, 43, source.InvoiceDate, 1000);
            var paths = new[] { probe.DocxPath, probe.PdfPath, probe.XmlPath };
            foreach (var path in paths) File.WriteAllText(path, "original invoice");
            var start = problem == "uninitialized" ? default(DateTime) : new DateTime(2026, 9, 30);
            DateTime? end = problem switch
            {
                "partial" => null, "schema" => new DateTime(2050, 1, 2), _ => new DateTime(2026, 9, 15),
            };
            Assert.ThrowsAny<Exception>(() => InvoiceGeneration.CreateAndGenerate(config, source.Client, 43,
                source.InvoiceDate, 1000, true, true, true, servicePeriodStart: start, servicePeriodEnd: end));
            Assert.Equal(42, source.Client.LastInvoiceNumber);
            Assert.False(config.Output.GenerateDocxByDefault); Assert.False(config.Output.GeneratePdfByDefault);
            Assert.False(config.Output.GenerateXmlByDefault);
            Assert.Equal(original, File.ReadAllText(ConfigManager.ConfigPath));
            foreach (var path in paths) Assert.Equal("original invoice", File.ReadAllText(path));
        });
    }

    [Fact]
    public void CustomPeriodReachesGenerationWithoutChangingInvoiceNamingOrClientRule()
    {
        WithTaxConfigPath(() =>
        {
            var source = Samples.Invoice(Samples.Client("PL"));
            var config = new AppConfig
            {
                Supplier = source.Supplier, Clients = [source.Client], BillingAccounts = [source.BillingAccount],
                Output = new OutputConfig { Directory = source.OutputDirectory },
            };
            var date = new DateTime(2027, 1, 5);
            var baseline = Invoice.Create(source.Client, source.Supplier, source.BillingAccount, config.Output, 7, date, 1000);
            var (invoice, paths) = InvoiceGeneration.CreateAndGenerate(config, source.Client, 7, date, 1000,
                true, false, true, servicePeriodStart: new DateTime(2026, 12, 15), servicePeriodEnd: new DateTime(2026, 12, 31));
            Assert.Equal(2, paths.Count);
            Assert.Equal(new DateTime(2026, 12, 15), invoice.ResolveServicePeriod().Start);
            Assert.Equal(new DateTime(2026, 12, 31), invoice.ResolveServicePeriod().End);
            Assert.Equal(baseline.InvoiceDate, invoice.InvoiceDate);
            Assert.Equal(baseline.FormattedNumber, invoice.FormattedNumber);
            Assert.Equal(baseline.OutputDirectory, invoice.OutputDirectory);
            Assert.Equal(baseline.BaseFilename, invoice.BaseFilename);
            Assert.Equal("early_previous", source.Client.MonthOffsetRule);
            Assert.DoesNotContain("service_period", File.ReadAllText(ConfigManager.ConfigPath));
        });
    }
}
