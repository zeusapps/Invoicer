using System.Text.RegularExpressions;
using Invoicer.Models;

namespace Invoicer.Generation;

/// <summary>
/// The identification form of a KSeF party, one of the TPodmiot2/DaneIdentyfikacyjne choices.
/// Modelled as a closed set so a buyer can never carry a mix of forms (such as a guessed
/// country code next to a placeholder identifier).
/// </summary>
internal abstract partial record BuyerIdentification
{
    /// <summary>Polish taxpayer: NIP.</summary>
    public sealed record Nip(string Value) : BuyerIdentification;

    /// <summary>EU VAT-registered buyer: KodUE + NrVatUE.</summary>
    public sealed record EuVat(string CountryPrefix, string Number) : BuyerIdentification;

    /// <summary>Buyer with a tax identifier issued outside the EU: KodKraju + NrID.</summary>
    public sealed record ForeignId(string CountryCode, string Identifier) : BuyerIdentification;

    /// <summary>Buyer without a tax identifier: BrakID.</summary>
    public sealed record NoId : BuyerIdentification;

    /// <summary>
    /// Selects the identification form from the client's country, never from the letters the
    /// VAT number happens to start with. Returns an error message when the data cannot form a
    /// valid identification.
    /// </summary>
    public static (BuyerIdentification? Identification, string? Error) Resolve(string? country, string? vat)
    {
        var countryCode = Countries.Normalize(country);
        if (countryCode.Length == 0)
            return (null, "Client country is required.");
        if (!Countries.IsKnown(countryCode))
            return (null, $"Client country '{countryCode}' is not a recognized country code.");

        var rawVat = (vat ?? "").Trim();

        if (!Countries.IsEu(countryCode))
        {
            return rawVat.Length == 0
                ? (new NoId(), null)
                : (new ForeignId(countryCode, rawVat), null);
        }

        if (rawVat.Length == 0)
            return (null, $"Client VAT is required for clients in {countryCode}.");

        var normalized = WhitespacePattern().Replace(rawVat, "").ToUpperInvariant();

        if (countryCode == Countries.Poland)
        {
            var nip = normalized.StartsWith(Countries.Poland, StringComparison.Ordinal)
                ? normalized[Countries.Poland.Length..]
                : normalized;

            return NipPattern().IsMatch(nip)
                ? (new Nip(nip), null)
                : (null, $"Client VAT '{rawVat}' is not a valid Polish NIP.");
        }

        var expectedPrefix = Countries.EuVatPrefix(countryCode)!;
        var number = normalized;
        if (normalized.Length >= 2 && char.IsAsciiLetter(normalized[0]) && char.IsAsciiLetter(normalized[1]))
        {
            var prefix = normalized[..2];
            if (prefix != expectedPrefix)
            {
                return (null,
                    $"Client VAT prefix '{prefix}' does not match client country {countryCode} (expected {expectedPrefix}).");
            }

            number = normalized[2..];
        }

        return EuVatNumberPattern().IsMatch(number)
            ? (new EuVat(expectedPrefix, number), null)
            : (null, $"Client VAT '{rawVat}' is not a valid EU VAT number.");
    }

    // FA(3) TNrNIP.
    [GeneratedRegex(@"^[1-9]((\d[1-9])|([1-9]\d))\d{7}$")]
    private static partial Regex NipPattern();

    // FA(3) TNrVatUE.
    [GeneratedRegex(@"^(\d|[A-Z]|\+|\*){1,12}$")]
    private static partial Regex EuVatNumberPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
