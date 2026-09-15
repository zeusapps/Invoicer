using Invoicer.Generation;
using Xunit;

namespace Invoicer.Tests;

public class TaxRateCodingTests
{
    [Theory]
    [InlineData("US", "np I", 8, null)]
    [InlineData("GB", "np I", 8, null)]
    [InlineData("DE", "np II", 9, "PL")]
    [InlineData("gr", "np II", 9, "PL")]
    public void ForeignClient_IsNotTaxedInPoland_AndReverseCharged(string country, string code, int index, string? prefix)
    {
        var (coding, error) = TaxRateCoding.Resolve(country, 0);

        Assert.Null(error);
        Assert.Equal(new TaxRateCoding(code, index, HasTaxAmount: false, ReverseCharge: true, SellerEuPrefix: prefix), coding);
    }

    [Theory]
    [InlineData(23, 1)]
    [InlineData(22, 1)]
    [InlineData(8, 2)]
    [InlineData(7, 2)]
    [InlineData(5, 3)]
    public void PolishClient_UsesNumericRateAndMatchingSummaryPair(int rate, int index)
    {
        var (coding, error) = TaxRateCoding.Resolve("PL", rate);

        Assert.Null(error);
        Assert.Equal(new TaxRateCoding(rate.ToString(), index, HasTaxAmount: true, ReverseCharge: false, SellerEuPrefix: null), coding);
    }

    [Fact]
    public void ForeignClientWithRate_IsRejected()
    {
        var (coding, error) = TaxRateCoding.Resolve("US", 23);

        Assert.Null(coding);
        Assert.Equal("Client VAT rate must be 0 for clients outside Poland (country US), not 23%.", error);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(12)]
    public void PolishClientWithUnsupportedRate_IsRejected(int rate)
    {
        var (coding, error) = TaxRateCoding.Resolve("PL", rate);

        Assert.Null(coding);
        Assert.Equal($"Client VAT rate {rate}% is not supported for KSeF invoices to Polish clients (supported: 23, 22, 8, 7, 5).", error);
    }
}
