using Invoicer.Models;

namespace Invoicer.Generation;

/// <summary>
/// Multi-line text blocks shared by the DOCX and PDF layouts, so both documents always
/// show the same customer and payment details.
/// </summary>
internal static class InvoiceText
{
    public static string CustomerBlock(Invoice invoice)
    {
        var lines = new List<string> { $"{invoice.Client.Name} / {invoice.Client.NameUa}" };

        // VAT only applies to some clients (e.g. not to US ones); omit the line rather than print a placeholder.
        if (!string.IsNullOrWhiteSpace(invoice.Client.Vat))
            lines.Add($"VAT: {invoice.Client.Vat}");

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
