using Invoicer.Generation;
using Xunit;

namespace Invoicer.Tests;

public class BuyerIdentificationTests
{
    [Fact]
    public void PolishClient_UsesNipWithoutPrefix()
    {
        var (identification, error) = BuyerIdentification.Resolve("PL", "PL9999999999");

        Assert.Null(error);
        Assert.Equal(new BuyerIdentification.Nip("9999999999"), identification);
    }

    [Fact]
    public void EuClient_UsesKodUeAndNumberWithoutPrefixOrWhitespace()
    {
        var (identification, error) = BuyerIdentification.Resolve("DE", "DE 123 456 789");

        Assert.Null(error);
        Assert.Equal(new BuyerIdentification.EuVat("DE", "123456789"), identification);
    }

    [Fact]
    public void GreekClient_UsesElPrefix()
    {
        var (identification, error) = BuyerIdentification.Resolve("GR", "EL123456789");

        Assert.Null(error);
        Assert.Equal(new BuyerIdentification.EuVat("EL", "123456789"), identification);
    }

    [Fact]
    public void EuClient_WithoutPrefix_KeepsNumber()
    {
        var (identification, error) = BuyerIdentification.Resolve("de", "123456789");

        Assert.Null(error);
        Assert.Equal(new BuyerIdentification.EuVat("DE", "123456789"), identification);
    }

    [Fact]
    public void NonEuClient_WithTaxId_UsesKodKrajuAndNrIdAsEntered()
    {
        var (identification, error) = BuyerIdentification.Resolve("US", " 12-3456789 ");

        Assert.Null(error);
        Assert.Equal(new BuyerIdentification.ForeignId("US", "12-3456789"), identification);
    }

    [Fact]
    public void NonEuClient_WithoutTaxId_UsesNoId()
    {
        var (identification, error) = BuyerIdentification.Resolve("US", "");

        Assert.Null(error);
        Assert.IsType<BuyerIdentification.NoId>(identification);
    }

    [Theory]
    [InlineData("", "PL9999999999")]
    [InlineData("ZZ", "")]
    [InlineData("DE", "")]
    [InlineData("PL", "DE9999999999")]
    [InlineData("DE", "FR12345678901")]
    public void InvalidData_ReturnsErrorWithoutIdentification(string country, string vat)
    {
        var (identification, error) = BuyerIdentification.Resolve(country, vat);

        Assert.Null(identification);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
