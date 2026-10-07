using Invoicer.Generation;
using Invoicer.Models;
using Xunit;

namespace Invoicer.Tests;

public class InvoiceTaxTreatmentTests
{
    [Theory]
    [InlineData("PL", 23, null, InvoiceTaxKind.Domestic, false)]
    [InlineData("PL", 22, false, InvoiceTaxKind.Domestic, false)]
    [InlineData("PL", 8, null, InvoiceTaxKind.Domestic, false)]
    [InlineData("PL", 7, null, InvoiceTaxKind.Domestic, false)]
    [InlineData("PL", 5, null, InvoiceTaxKind.Domestic, false)]
    [InlineData(" ee ", 0, null, InvoiceTaxKind.EuServices, true)]
    [InlineData("EE", 0, true, InvoiceTaxKind.EuServices, true)]
    [InlineData("US", 0, null, InvoiceTaxKind.NonEuServices, false)]
    [InlineData("US", 0, false, InvoiceTaxKind.NonEuServices, false)]
    [InlineData("US", 0, true, InvoiceTaxKind.NonEuServices, true)]
    [InlineData("GB", 0, false, InvoiceTaxKind.NonEuServices, false)]
    [InlineData("GB", 0, true, InvoiceTaxKind.NonEuServices, true)]
    public void ResolvesSupportedTreatments(string country, int rate, bool? choice, InvoiceTaxKind kind, bool reverse)
    {
        var (treatment, error) = InvoiceTaxTreatment.Resolve(country, rate, choice);
        Assert.Null(error);
        Assert.Equal(new InvoiceTaxTreatment(kind, rate, reverse), treatment);
        var amounts = treatment!.Calculate(1000m);
        Assert.Equal(kind == InvoiceTaxKind.Domestic ? rate * 10m : 0m, amounts.Vat);
        Assert.Equal(1000m + amounts.Vat, amounts.Gross);
    }

    [Theory]
    [InlineData("", 0, null, "country is required")]
    [InlineData("ZZ", 0, false, "recognized")]
    [InlineData("PL", 0, null, "not supported")]
    [InlineData("PL", 12, null, "not supported")]
    [InlineData("PL", 23, true, "reverse_charge")]
    [InlineData("EE", 23, null, "must be 0")]
    [InlineData("US", 23, false, "must be 0")]
    [InlineData("EE", 0, false, "require reverse_charge")]
    [InlineData("GB", 0, null, "explicit reverse_charge")]
    public void RejectsInvalidTreatments(string country, int rate, bool? choice, string message)
    {
        var (treatment, error) = InvoiceTaxTreatment.Resolve(country, rate, choice);
        Assert.Null(treatment);
        Assert.Contains(message, error);
    }

    [Fact]
    public void CalculationRetainsMidpointToEven()
    {
        var client = Samples.Client("PL"); client.VatRate = 5;
        var invoice = Samples.Invoice(client, amount: .1m);
        Assert.Equal(0m, invoice.VatAmount); // .005 rounds to .00
        Assert.Equal(.1m, invoice.GrossAmount);
    }

    [Theory]
    [InlineData("copied")]
    [InlineData("current")]
    [InlineData("vat")]
    [InlineData("gross")]
    public void RejectsStaleMutableInvoices(string mutation)
    {
        var invoice = Samples.Invoice(Samples.Client("US"));
        switch (mutation)
        {
            case "copied": invoice.VatRate = 23; break;
            case "current": invoice.Client.VatRate = 23; break;
            case "vat": invoice.VatAmount = 230; break;
            case "gross": invoice.GrossAmount++; break;
        }
        Assert.Throws<InvoiceValidationException>(() => InvoicePreparation.Prepare(invoice));
    }

    [Theory]
    [InlineData("PL", "", "1111111111", "PL1111111111")]
    [InlineData("EE", "", "1111111111", "PL1111111111")]
    [InlineData("EE", "DE123456789", "1111111111", "PL1111111111")]
    [InlineData("EE", "EE123456789", "1111111111", "")]
    [InlineData("EE", "EE123456789", "1111111111", "PL9999999999")]
    [InlineData("US", "", "", "")]
    [InlineData("US", "", "123", "")]
    public void AllFormatsRequireConsistentIdentifiers(string country, string buyer, string nip, string sellerVat)
    {
        var invoice = Samples.Invoice(Samples.Client(country));
        invoice.Client.Vat = buyer; invoice.Supplier.Tin = nip; invoice.Supplier.Vat = sellerVat;
        Assert.Throws<InvoiceValidationException>(() => InvoicePreparation.Prepare(invoice));
    }

    [Theory]
    [InlineData(" ee 123 456789 ")]
    [InlineData("123456789")]
    public void NormalizesEuIdentificationForBothDocumentAndXml(string buyer)
    {
        var invoice = Samples.Invoice(Samples.Client("EE"));
        invoice.Client.Vat = buyer; invoice.Supplier.Tin = " pl 111 1111111 "; invoice.Supplier.Vat = " pl 1111111111 ";
        var prepared = InvoicePreparation.Prepare(invoice);
        Assert.Equal("1111111111", prepared.SellerNip);
        Assert.Equal("PL1111111111", prepared.SellerVat);
        Assert.Equal(new BuyerIdentification.EuVat("EE", "123456789"), prepared.Buyer);
        Assert.Contains("VAT: EE123456789", InvoiceText.CustomerBlock(invoice));
    }
}

internal static class Samples
{
    internal static ClientConfig Client(string country, bool? reverse = null) => new()
    {
        Key = country, Country = country, Name = $"Example {country} Buyer", NameUa = "Тестовий замовник",
        Address = "2 Example Street", AddressUa = "Тестова адреса",
        Vat = country == "PL" ? "PL9999999999" : country == "EE" ? "EE123456789" : "",
        VatRate = country == "PL" ? 23 : 0, ReverseCharge = reverse,
        Currency = country == "PL" ? "PLN" : country == "EE" ? "EUR" : "USD",
        InvoicePrefix = country, BillingAccount = "SAMPLE", DefaultAmount = 1000,
        ServiceDescription = "B2B IT services", ServiceDescriptionUa = "ІТ-послуги для бізнесу",
    };
    internal static Invoice Invoice(ClientConfig client, decimal amount = 1000m, string? directory = null) => Models.Invoice.Create(
        client, new SupplierConfig { Name = "Example Polish Supplier", NameUa = "Тестовий польський постачальник",
            Tin = "1111111111", Vat = "PL1111111111", Address = "1 Example Street, Warsaw, Poland", AddressUa = "Тестова адреса, Варшава, Польща" },
        new BillingAccountConfig { Key = "SAMPLE", Iban = "PL00102010260000004270201111", Bank = "Example Bank" },
        new OutputConfig { Directory = directory ?? Path.Combine(Path.GetTempPath(), "invoicer-tests", Guid.NewGuid().ToString("N")), Pattern = "{year}/Invoices", Filename = "{date}_{client}" },
        1, new DateTime(2026, 10, 7), amount, generateXml: true, exchangeRate: client.Currency == "EUR" ? 4.25m : 3.7998m);
}
