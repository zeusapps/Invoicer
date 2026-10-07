using Invoicer.Models;

namespace Invoicer.Generation;

/// <summary>Immutable validated tax inputs for one synchronous generation request.</summary>
internal sealed record PreparedInvoiceTax(
    InvoiceTaxTreatment Treatment, InvoiceAmounts Amounts,
    string SellerNip, string SellerVat, BuyerIdentification Buyer);

internal static class InvoicePreparation
{
    internal static string NormalizeId(string value) =>
        string.Concat(value.Where(c => !char.IsWhiteSpace(c))).ToUpperInvariant();

    public static PreparedInvoiceTax Prepare(Invoice invoice)
    {
        var errors = new List<string>();
        var (treatment, taxError) = InvoiceTaxTreatment.Resolve(
            invoice.Client.Country, invoice.Client.VatRate, invoice.Client.ReverseCharge);
        if (taxError is not null) errors.Add($"Client '{invoice.Client.Key}': {taxError}");
        if (invoice.VatRate != invoice.Client.VatRate)
            errors.Add("Invoice VAT rate differs from current client vat_rate; recreate the invoice.");
        var amounts = treatment?.Calculate(invoice.NetAmount);
        if (amounts is not null && (invoice.VatAmount != amounts.Vat || invoice.GrossAmount != amounts.Gross))
            errors.Add("Invoice VAT/gross amounts are inconsistent with net and the current tax treatment; recreate the invoice.");

        var (buyer, buyerError) = BuyerIdentification.Resolve(invoice.Client.Country, invoice.Client.Vat);
        if (buyerError is not null) errors.Add($"Client '{invoice.Client.Key}': {buyerError}");
        var sellerNip = NormalizeId(invoice.Supplier.Tin);
        if (sellerNip.StartsWith("PL", StringComparison.Ordinal)) sellerNip = sellerNip[2..];
        var (_, sellerError) = BuyerIdentification.Resolve("PL", sellerNip);
        if (sellerError is not null) errors.Add("Supplier TIN/NIP is required and must be a valid Polish NIP.");
        var sellerVat = NormalizeId(invoice.Supplier.Vat);
        if (treatment?.Kind == InvoiceTaxKind.EuServices && sellerVat != "PL" + sellerNip)
            errors.Add("Supplier EU VAT identifier is required and must be PL followed by the same supplier NIP.");
        if (errors.Count > 0) throw new InvoiceValidationException(errors);
        return new(treatment!, amounts!, sellerNip, sellerVat, buyer!);
    }
}
