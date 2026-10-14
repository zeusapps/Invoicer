using System.Globalization;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Invoicer.Exchange;
using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class ServicePeriodTests
{
    [Theory]
    [InlineData("early_previous", "2026-10-20", "2026-09-01", "2026-09-30")]
    [InlineData("early_previous", "2026-10-21", "2026-10-01", "2026-10-31")]
    [InlineData("early_current", "2026-10-20", "2026-10-01", "2026-10-31")]
    [InlineData("early_current", "2026-10-21", "2026-11-01", "2026-11-30")]
    [InlineData("unknown", "2026-10-05", "2026-10-01", "2026-10-31")]
    [InlineData("early_previous", "2027-01-05", "2026-12-01", "2026-12-31")]
    [InlineData("early_current", "2026-12-25", "2027-01-01", "2027-01-31")]
    [InlineData("early_previous", "2028-03-05", "2028-02-01", "2028-02-29")]
    [InlineData("early_previous", "2027-03-05", "2027-02-01", "2027-02-28")]
    public void AutomaticUsesCalendarBoundaries(string rule, string date, string start, string end)
    {
        var period = ServicePeriod.Resolve(Invoice.CalculateServiceMonth(Parse(date).AddHours(12), rule));
        Assert.Equal(Parse(start), period.Start);
        Assert.Equal(Parse(end), period.End);
        Assert.True(period.IsFullMonth);
    }

    [Theory]
    [InlineData("2026-09-15", "2026-09-15")]
    [InlineData("2026-09-15", "2026-10-14")]
    [InlineData("2026-12-15", "2027-01-14")]
    public void ExplicitPeriodTakesPrecedenceAndNormalizesTime(string start, string end)
    {
        var period = ServicePeriod.Resolve(default, Parse(start).AddHours(14), Parse(end).AddHours(1));
        Assert.Equal(Parse(start), period.Start);
        Assert.Equal(Parse(end), period.End);
        Assert.False(period.IsFullMonth);
    }

    [Fact]
    public void RejectsIncompleteUninitializedReversedAndOverflowingDefaults()
    {
        Assert.Throws<InvoiceValidationException>(() => ServicePeriod.Resolve(default));
        Assert.Throws<InvoiceValidationException>(() => ServicePeriod.Resolve(Parse("2026-09-01"), Parse("2026-09-15")));
        Assert.Throws<InvoiceValidationException>(() => ServicePeriod.Resolve(default, default(DateTime), Parse("2026-09-30")));
        Assert.Throws<InvoiceValidationException>(() => ServicePeriod.Resolve(default, Parse("2026-09-30"), Parse("2026-09-15")));
        Assert.Throws<InvoiceValidationException>(() => Invoice.CalculateServiceMonth(DateTime.MaxValue, "early_current"));
        Assert.Throws<InvoiceValidationException>(() => Invoice.CalculateServiceMonth(new DateTime(1, 1, 2), "early_previous"));
    }

    [Theory]
    [InlineData("2026-10-05", "2026-09-15", "2026-09-15")]
    [InlineData("2026-09-25", "2026-10-14", "2026-09-25")]
    [InlineData("2027-01-05", "2026-12-15", "2026-12-15")]
    public void RateUsesActualPeriodEnd(string date, string end, string expected)
    {
        var invoice = Samples.Invoice(Samples.Client("PL"));
        invoice.InvoiceDate = Parse(date).AddHours(18);
        invoice.ServicePeriodStart = Parse(end);
        invoice.ServicePeriodEnd = Parse(end).AddHours(4);
        Assert.Equal(Parse(expected), ExchangeRateRules.RelevantDate(invoice));
    }

    [Theory]
    [InlineData("2026-09-01", "2026-09-30", "September 2026")]
    [InlineData("2026-09-15", "2026-09-30", "15.09.2026 - 30.09.2026")]
    [InlineData("2026-09-15", "2026-10-14", "15.09.2026 - 14.10.2026")]
    [InlineData("2026-09-15", "2026-09-15", "15.09.2026 - 15.09.2026")]
    public void DocumentsUseSelectedPeriod(string start, string end, string expected)
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            var invoice = Samples.Invoice(Samples.Client("PL"));
            invoice.ServicePeriodStart = Parse(start); invoice.ServicePeriodEnd = Parse(end);
            Assert.Equal(expected, InvoiceText.ServicePeriodText(invoice.ResolveServicePeriod()));
            DocxGenerator.Generate(invoice);
            using var doc = WordprocessingDocument.Open(invoice.DocxPath, false);
            var text = doc.MainDocumentPart!.Document!.Body!.InnerText;
            Assert.Contains(expected, text);
            Assert.Contains("Service period / Період надання послуг:", text);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Theory]
    [InlineData("2026-10-05", null, null, "2026-09-01", "2026-09-30")]
    [InlineData("2027-01-05", null, null, "2026-12-01", "2026-12-31")]
    [InlineData("2026-10-05", "2026-09-15", "2026-10-14", "2026-09-15", "2026-10-14")]
    public void CombinedOutputsCarryTheSamePeriod(string date, string? explicitStart, string? explicitEnd, string start, string end)
    {
        var client = Samples.Client("PL");
        var samples = Environment.GetEnvironmentVariable("INVOICER_PERIOD_SAMPLES");
        var seed = Samples.Invoice(client);
        var output = new OutputConfig
        {
            Directory = samples is null ? Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N"))
                : Path.Combine(samples, explicitStart is null ? date : "custom"),
        };
        var invoice = Invoice.Create(client, seed.Supplier, seed.BillingAccount, output, 1, Parse(date), 1000,
            generateXml: true, servicePeriodStart: explicitStart is null ? null : Parse(explicitStart),
            servicePeriodEnd: explicitEnd is null ? null : Parse(explicitEnd));
        var clock = new Clock();
        Assert.Equal(3, InvoiceGeneration.Generate(invoice, clock).Count);
        var bytes = File.ReadAllBytes(invoice.XmlPath);
        KsefXmlGenerator.Generate(invoice, clock);
        Assert.Equal(bytes, File.ReadAllBytes(invoice.XmlPath));
        Assert.Empty(KsefSchemaValidator.Validate(invoice.XmlPath));
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        var fa = XDocument.Load(invoice.XmlPath).Root!.Element(ns + "Fa")!;
        Assert.Equal(date, fa.Element(ns + "P_1")!.Value);
        var period = fa.Element(ns + "OkresFa")!;
        Assert.Equal(new[] { "P_6_Od", "P_6_Do" }, period.Elements().Select(e => e.Name.LocalName));
        Assert.Equal(start, period.Element(ns + "P_6_Od")!.Value);
        Assert.Equal(end, period.Element(ns + "P_6_Do")!.Value);
        Assert.Equal("OkresFa", fa.Elements().Skip(3).First().Name.LocalName);
        Assert.Empty(fa.Descendants(ns + "P_6")); Assert.Empty(fa.Descendants(ns + "P_6A"));
        using var doc = WordprocessingDocument.Open(invoice.DocxPath, false);
        Assert.Contains(InvoiceText.ServicePeriodText(invoice.ResolveServicePeriod()), doc.MainDocumentPart!.Document!.Body!.InnerText);
    }

    [Theory]
    [InlineData("2006-01-01")]
    [InlineData("2050-01-01")]
    public void XmlAcceptsInclusiveSchemaBounds(string date)
    {
        var invoice = Samples.Invoice(Samples.Client("PL"));
        invoice.ServicePeriodStart = invoice.ServicePeriodEnd = Parse(date);
        KsefXmlGenerator.Generate(invoice, new Clock());
        Assert.Empty(KsefSchemaValidator.Validate(invoice.XmlPath));
    }

    [Theory]
    [InlineData("2005-12-31")]
    [InlineData("2050-01-02")]
    public void SchemaBoundsApplyOnlyToXml(string date)
    {
        var invoice = Samples.Invoice(Samples.Client("PL"));
        invoice.ServicePeriodStart = invoice.ServicePeriodEnd = Parse(date);
        Assert.Throws<KsefValidationException>(() => InvoiceGeneration.Generate(invoice));
        Assert.False(Directory.Exists(invoice.OutputDirectory));
        Directory.CreateDirectory(invoice.OutputDirectory);
        foreach (var path in new[] { invoice.XmlPath, invoice.PdfPath, invoice.DocxPath }) File.WriteAllText(path, "sentinel");
        Assert.Throws<KsefValidationException>(() => InvoiceGeneration.Generate(invoice));
        foreach (var path in new[] { invoice.XmlPath, invoice.PdfPath, invoice.DocxPath }) Assert.Equal("sentinel", File.ReadAllText(path));
        invoice.GenerateXml = false;
        Assert.Equal(2, InvoiceGeneration.Generate(invoice).Count);
    }

    [Theory]
    [InlineData("PDF")]
    [InlineData("DOCX")]
    [InlineData("XML")]
    [InlineData("combined")]
    [InlineData("documents")]
    public void InvalidPeriodPreservesExistingFilesForEveryEntryPoint(string format)
    {
        var invoice = Samples.Invoice(Samples.Client("PL"));
        invoice.ServicePeriodStart = Parse("2026-09-30"); invoice.ServicePeriodEnd = Parse("2026-09-15");
        invoice.GenerateXml = format != "documents";
        Directory.CreateDirectory(invoice.OutputDirectory);
        var paths = new[] { invoice.XmlPath, invoice.PdfPath, invoice.DocxPath };
        foreach (var path in paths) File.WriteAllText(path, "sentinel");
        Action generate = format switch
        {
            "PDF" => () => PdfGenerator.Generate(invoice), "DOCX" => () => DocxGenerator.Generate(invoice),
            "XML" => () => KsefXmlGenerator.Generate(invoice), _ => () => InvoiceGeneration.Generate(invoice),
        };
        Assert.ThrowsAny<Exception>(generate);
        foreach (var path in paths) Assert.Equal("sentinel", File.ReadAllText(path));
    }

    internal static DateTime Parse(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture);
    private sealed class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);
    }
}
