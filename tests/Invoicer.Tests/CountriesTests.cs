using System.Xml.Linq;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class CountriesTests
{
    [Theory]
    [InlineData("PL", true, true, "PL")]
    [InlineData("de", true, true, "DE")]
    [InlineData("GR", true, true, "EL")]
    [InlineData("US", true, false, null)]
    [InlineData("GB", true, false, null)]
    [InlineData("XI", true, false, null)]
    [InlineData("ZZ", false, false, null)]
    [InlineData("", false, false, null)]
    public void ClassifiesCountryCodes(string code, bool known, bool eu, string? vatPrefix)
    {
        Assert.Equal(known, Countries.IsKnown(code));
        Assert.Equal(eu, Countries.IsEu(code));
        Assert.Equal(vatPrefix, Countries.EuVatPrefix(code));
    }

    [Theory]
    [InlineData("PL", "PL")]
    [InlineData("DE", "DE")]
    [InlineData("EL", "GR")]
    [InlineData("GR", null)]
    [InlineData("XI", null)]
    [InlineData("US", null)]
    [InlineData("N/", null)]
    public void MapsEuVatPrefixToCountry(string prefix, string? expected)
    {
        Assert.Equal(expected, Countries.FromEuVatPrefix(prefix));
    }

    [Fact]
    public void KnownCodes_MatchSchemaTKodKraju()
    {
        var schemaCodes = ReadEnumeration("KodyKrajow_v10-0E.xsd", "TKodKraju");

        Assert.Equal(schemaCodes.Order(), Countries.KnownCodes.Order());
    }

    [Fact]
    public void EuCodes_MatchSchemaTKodyKrajowUE()
    {
        // The schema lists VAT prefixes: Greece is EL rather than GR, and XI (Northern Ireland)
        // is a prefix without a member state of its own.
        var schemaPrefixes = ReadEnumeration("schemat.xsd", "TKodyKrajowUE")
            .Where(p => p != "XI")
            .Select(p => p == "EL" ? "GR" : p);

        Assert.Equal(schemaPrefixes.Order(), Countries.EuCodes.Order());
    }

    private static List<string> ReadEnumeration(string schemaFile, string typeName)
    {
        XNamespace xsd = "http://www.w3.org/2001/XMLSchema";
        var doc = XDocument.Load(Path.Combine(LocalSchemaResolver.SchemaDirectory, schemaFile));

        var type = doc.Descendants(xsd + "simpleType")
            .Single(e => (string?)e.Attribute("name") == typeName);

        return type.Descendants(xsd + "enumeration")
            .Select(e => (string)e.Attribute("value")!)
            .ToList();
    }
}
