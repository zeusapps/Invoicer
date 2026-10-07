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
    public static (TaxRateCoding? Coding, string? Error) Resolve(string? country, int vatRate, bool? reverseCharge = null)
    {
        var (treatment, error) = InvoiceTaxTreatment.Resolve(country, vatRate, reverseCharge);
        return treatment is null ? (null, error) : (FromTreatment(treatment), null);
    }

    public static TaxRateCoding FromTreatment(InvoiceTaxTreatment treatment) => treatment.Kind switch
    {
        InvoiceTaxKind.EuServices => new("np II", 9, false, true, Countries.Poland),
        InvoiceTaxKind.NonEuServices => new("np I", 8, false, treatment.ReverseCharge, null),
        _ => new(treatment.Rate.ToString(CultureInfo.InvariantCulture),
            treatment.Rate switch { 23 or 22 => 1, 8 or 7 => 2, _ => 3 }, true, false, null),
    };
}
