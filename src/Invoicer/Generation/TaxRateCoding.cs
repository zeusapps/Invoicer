using System.Globalization;
using Invoicer.Models;

namespace Invoicer.Generation;

/// <summary>
/// How an invoice's tax rate is coded in FA(3): the line rate (P_12), which P_13_n/P_14_n summary
/// pair carries the amounts, and the reverse-charge annotation (P_18).
/// </summary>
/// <param name="RateCode">The TStawkaPodatku value for P_12.</param>
/// <param name="SummaryIndex">n in P_13_n.</param>
/// <param name="HasTaxAmount">Whether P_14_n is written; false for rates with no Polish VAT.</param>
/// <param name="ReverseCharge">Whether P_18 is 1 ("odwrotne obciążenie").</param>
/// <param name="SellerEuPrefix">Podmiot1/PrefiksPodatnika, set for services under art. 100 ust. 1 pkt 4.</param>
internal sealed record TaxRateCoding(
    string RateCode,
    int SummaryIndex,
    bool HasTaxAmount,
    bool ReverseCharge,
    string? SellerEuPrefix)
{
    /// <summary>
    /// Derives the coding from the client's country, following the TStawkaPodatku definitions in
    /// the MF FA(3) brochure: services to non-EU buyers are "np I" (P_13_8), art. 28b services to EU
    /// buyers are "np II" (P_13_9), and "oo" is reserved for domestic reverse charge.
    /// </summary>
    public static (TaxRateCoding? Coding, string? Error) Resolve(string? country, int vatRate)
    {
        var countryCode = Countries.Normalize(country);

        if (countryCode != Countries.Poland)
        {
            if (vatRate != 0)
            {
                return (null,
                    $"Client VAT rate must be 0 for clients outside Poland (country {countryCode}), not {vatRate}%.");
            }

            return Countries.IsEu(countryCode)
                ? (new TaxRateCoding("np II", 9, HasTaxAmount: false, ReverseCharge: true, SellerEuPrefix: Countries.Poland), null)
                : (new TaxRateCoding("np I", 8, HasTaxAmount: false, ReverseCharge: true, SellerEuPrefix: null), null);
        }

        int? summaryIndex = vatRate switch
        {
            23 or 22 => 1,
            8 or 7 => 2,
            5 => 3,
            _ => null,
        };

        return summaryIndex is { } index
            ? (new TaxRateCoding(vatRate.ToString(CultureInfo.InvariantCulture), index, HasTaxAmount: true, ReverseCharge: false, SellerEuPrefix: null), null)
            : (null, $"Client VAT rate {vatRate}% is not supported for KSeF invoices to Polish clients (supported: 23, 22, 8, 7, 5).");
    }
}
