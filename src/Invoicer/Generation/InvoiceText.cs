using Invoicer.Models;

namespace Invoicer.Generation;

/// <summary>
/// Multi-line text blocks shared by the DOCX and PDF layouts, so both documents always
/// show the same customer and payment details.
/// </summary>
internal static class InvoiceText
{
    public const string NpLabel = "NP - not subject to Polish VAT";
    public const string NpExplanationUa = "Не підлягає оподаткуванню ПДВ у Польщі";
    public const string ReverseChargeAnnotation = "Reverse charge / odwrotne obciążenie";
    public const string ReverseChargeExplanationUa = "Зворотне нарахування ПДВ";

    public static string TaxLabel(InvoiceTaxTreatment treatment) =>
        treatment.Kind == InvoiceTaxKind.Domestic ? $"{treatment.Rate}%" : NpLabel;
    public static string FormatAmount(decimal amount, string currency) =>
        $"{amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} {currency}";

    public static string SupplierBlock(Invoice invoice, PreparedInvoiceTax prepared) =>
        $"{invoice.Supplier.Name} / {invoice.Supplier.NameUa}\n" +
        $"NIP/TIN: {prepared.SellerNip}, REGON: {invoice.Supplier.Regon}\n" +
        $"VAT EU: {prepared.SellerVat}\n{invoice.Supplier.Address}\n{invoice.Supplier.AddressUa}";

    public static string CustomerBlock(Invoice invoice)
        => CustomerBlock(invoice, InvoicePreparation.Prepare(invoice));

    public static string CustomerBlock(Invoice invoice, PreparedInvoiceTax prepared)
    {
        var lines = new List<string> { $"{invoice.Client.Name} / {invoice.Client.NameUa}" };

        // VAT only applies to some clients (e.g. not to US ones); omit the line rather than print a placeholder.
        var identifier = prepared.Buyer switch
        {
            BuyerIdentification.Nip nip => "PL" + nip.Value,
            BuyerIdentification.EuVat eu => eu.CountryPrefix + eu.Number,
            BuyerIdentification.ForeignId foreign => foreign.Identifier,
            _ => null,
        };
        if (identifier is not null) lines.Add($"VAT: {identifier}");

        lines.Add(invoice.Client.Address);
        lines.Add(invoice.Client.AddressUa);
        return string.Join("\n", lines);
    }

    public static string BankAccountBlock(Invoice invoice)
    {
        return $"IBAN: {invoice.BillingAccount.Iban}\n" +
               $"Bank: {invoice.BillingAccount.Bank}\n" +
               $"SWIFT: {invoice.BillingAccount.Swift}";
    }
}
