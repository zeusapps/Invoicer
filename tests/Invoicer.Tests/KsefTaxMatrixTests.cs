using System.Xml.Linq;
using Invoicer.Generation;
using Xunit;

namespace Invoicer.Tests;

public class KsefTaxMatrixTests
{
    [Theory]
    [InlineData("EE", "EE123456789", "PLN", null, "np II", "P_13_9", "1")]
    [InlineData("EE", "123456789", "PLN", null, "np II", "P_13_9", "1")]
    [InlineData("EE", " ee 123456789 ", "EUR", null, "np II", "P_13_9", "1")]
    [InlineData("EE", "123456789", "EUR", null, "np II", "P_13_9", "1")]
    [InlineData("US", "", "PLN", false, "np I", "P_13_8", "2")]
    [InlineData("US", "", "PLN", true, "np I", "P_13_8", "1")]
    [InlineData("US", "", "USD", false, "np I", "P_13_8", "2")]
    [InlineData("US", "", "USD", true, "np I", "P_13_8", "1")]
    [InlineData("US", "", "USD", null, "np I", "P_13_8", "2")]
    [InlineData("US", "12-3456789", "USD", false, "np I", "P_13_8", "2")]
    public void ForeignServicesHaveExactSemanticFieldsAndPassOfficialSchema(
        string country, string vat, string currency, bool? reverse, string rateCode, string netField, string p18)
    {
        var client = Samples.Client(country, reverse); client.Vat = vat; client.Currency = currency;
        var invoice = Samples.Invoice(client);
        invoice.Supplier.Tin = " pl 1111111111 ";
        KsefXmlGenerator.Generate(invoice, new SampleClock());
        XNamespace ns = KsefXmlGenerator.Metadata.Namespace;
        var root = XDocument.Load(invoice.XmlPath).Root!;
        var fa = root.Element(ns + "Fa")!;
        Assert.Equal(rateCode, fa.Element(ns + "FaWiersz")!.Element(ns + "P_12")!.Value);
        Assert.Equal("1000", fa.Element(ns + netField)!.Value);
        Assert.Single(fa.Elements(), e => e.Name.LocalName.StartsWith("P_13_"));
        Assert.DoesNotContain(fa.Elements(), e => e.Name.LocalName.StartsWith("P_14_"));
        Assert.Equal("1000", fa.Element(ns + "P_15")!.Value);
        Assert.Equal(p18, fa.Element(ns + "Adnotacje")!.Element(ns + "P_18")!.Value);
        var seller = root.Element(ns + "Podmiot1")!;
        Assert.Equal(country == "EE" ? "PL" : null, seller.Element(ns + "PrefiksPodatnika")?.Value);
        Assert.Equal("1111111111", seller.Element(ns + "DaneIdentyfikacyjne")!.Element(ns + "NIP")!.Value);
        var buyer = root.Element(ns + "Podmiot2")!;
        var id = buyer.Element(ns + "DaneIdentyfikacyjne")!;
        Assert.Equal(country, buyer.Element(ns + "Adres")!.Element(ns + "KodKraju")!.Value);
        if (country == "EE")
        {
            Assert.Equal("EE", id.Element(ns + "KodUE")!.Value);
            Assert.Equal("123456789", id.Element(ns + "NrVatUE")!.Value);
        }
        else if (vat.Length == 0) Assert.Equal("1", id.Element(ns + "BrakID")!.Value);
        else { Assert.Equal("US", id.Element(ns + "KodKraju")!.Value); Assert.Equal(vat, id.Element(ns + "NrID")!.Value); }
        Assert.Equal(currency == "PLN" ? null : country == "EE" ? "4.25" : "3.7998",
            fa.Element(ns + "FaWiersz")!.Element(ns + "KursWaluty")?.Value);
        Assert.Empty(KsefSchemaValidator.Validate(invoice.XmlPath));
    }
}

internal sealed class SampleClock : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
}
