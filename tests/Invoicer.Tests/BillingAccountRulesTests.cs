using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class BillingAccountRulesTests
{
    [Fact]
    public void ValidateKeys_AcceptsUniqueKeys()
    {
        Assert.Null(BillingAccountRules.ValidateKeys([new() { Key = "PLN" }, new() { Key = "USD" }]));
    }

    [Fact]
    public void ValidateKeys_RejectsDuplicateKey()
    {
        var error = BillingAccountRules.ValidateKeys([new() { Key = "PLN" }, new() { Key = "PLN" }]);

        Assert.NotNull(error);
        Assert.Contains("'PLN'", error);
    }

    [Fact]
    public void ValidateKeys_RejectsEmptyKey()
    {
        Assert.NotNull(BillingAccountRules.ValidateKeys([new() { Key = "PLN" }, new() { Key = " " }]));
    }

    [Fact]
    public void ApplyKeyRenames_PointsClientsAtNewKey()
    {
        var clients = new List<ClientConfig>
        {
            new() { Key = "GREENFLOW", BillingAccount = "USD" },
            new() { Key = "EL", BillingAccount = "PLN" },
        };

        BillingAccountRules.ApplyKeyRenames(clients, [("USD", "USD_WISE")]);

        Assert.Equal("USD_WISE", clients[0].BillingAccount);
        Assert.Equal("PLN", clients[1].BillingAccount);
    }

    [Fact]
    public void ApplyKeyRenames_SwapsKeysWithoutCollapsing()
    {
        var clients = new List<ClientConfig>
        {
            new() { Key = "A", BillingAccount = "PLN" },
            new() { Key = "B", BillingAccount = "USD" },
        };

        BillingAccountRules.ApplyKeyRenames(clients, [("PLN", "USD"), ("USD", "PLN")]);

        Assert.Equal("USD", clients[0].BillingAccount);
        Assert.Equal("PLN", clients[1].BillingAccount);
    }

    [Fact]
    public void ClientsReferencing_NamesAssignedClients()
    {
        var clients = new List<ClientConfig>
        {
            new() { Key = "GREENFLOW", BillingAccount = "USD" },
            new() { Key = "EL", BillingAccount = "PLN" },
            new() { Key = "NONE", BillingAccount = "" },
        };

        Assert.Equal(new[] { "GREENFLOW" }, BillingAccountRules.ClientsReferencing(clients, "USD"));
        Assert.Empty(BillingAccountRules.ClientsReferencing(clients, "EUR"));
        Assert.Empty(BillingAccountRules.ClientsReferencing(clients, ""));
    }

    [Theory]
    [InlineData("USD", "PLN", true)]
    [InlineData("USD", "usd", false)]
    [InlineData("USD", "", false)]
    public void CurrencyMismatchWarning_OnlyForDeclaredDifferentCurrency(string clientCurrency, string accountCurrency, bool expectWarning)
    {
        var warning = BillingAccountRules.CurrencyMismatchWarning(
            new ClientConfig { Currency = clientCurrency },
            new BillingAccountConfig { Currency = accountCurrency });

        Assert.Equal(expectWarning, warning is not null);
    }

    [Fact]
    public void CurrencyMismatchWarning_NoneWithoutAccount()
    {
        Assert.Null(BillingAccountRules.CurrencyMismatchWarning(new ClientConfig { Currency = "USD" }, null));
    }
}
