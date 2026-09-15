namespace Invoicer.Models;

/// <summary>
/// Country reference data used for client countries and KSeF buyer identification.
/// The lists mirror the FA(3) schema types TKodKraju and TKodyKrajowUE; a test compares
/// them against the vendored XSDs so they cannot silently drift.
/// </summary>
public static class Countries
{
    public const string Poland = "PL";

    // TKodKraju: every country code the FA(3) schema accepts.
    internal static readonly IReadOnlySet<string> KnownCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "AF", "AX", "AL", "DZ", "AD", "AO", "AI", "AQ", "AG", "AN", "SA", "AR", "AM", "AW", "AU", "AT",
        "AZ", "BS", "BH", "BD", "BB", "BE", "BZ", "BJ", "BM", "BT", "BY", "BO", "BQ", "BA", "BW", "BR",
        "BN", "IO", "BG", "BF", "BI", "XC", "CL", "CN", "HR", "CW", "CY", "TD", "ME", "DK", "DM", "DO",
        "DJ", "EG", "EC", "ER", "EE", "ET", "FK", "FJ", "PH", "FI", "FR", "TF", "GA", "GM", "GH", "GI",
        "GR", "GD", "GL", "GE", "GU", "GG", "GY", "GF", "GP", "GT", "GN", "GQ", "GW", "HT", "ES", "HN",
        "HK", "IN", "ID", "IQ", "IR", "IE", "IS", "IL", "JM", "JP", "YE", "JE", "JO", "KY", "KH", "CM",
        "CA", "QA", "KZ", "KE", "KG", "KI", "CO", "KM", "CG", "CD", "KP", "XK", "CR", "CU", "KW", "LA",
        "LS", "LB", "LR", "LY", "LI", "LT", "LV", "LU", "MK", "MG", "YT", "MO", "MW", "MV", "MY", "ML",
        "MT", "MP", "MA", "MQ", "MR", "MU", "MX", "XL", "FM", "UM", "MD", "MC", "MN", "MS", "MZ", "MM",
        "NA", "NR", "NP", "NL", "DE", "NE", "NG", "NI", "NU", "NF", "NO", "NC", "NZ", "PS", "OM", "PK",
        "PW", "PA", "PG", "PY", "PE", "PN", "PF", "PL", "GS", "PT", "PR", "CF", "CZ", "KR", "ZA", "RE",
        "RU", "RO", "RW", "EH", "BL", "KN", "LC", "MF", "VC", "SV", "WS", "AS", "SM", "SN", "RS", "SC",
        "SL", "SG", "SK", "SI", "SO", "LK", "PM", "US", "SZ", "SD", "SS", "SR", "SJ", "SH", "SY", "CH",
        "SE", "TJ", "TH", "TW", "TZ", "TG", "TK", "TO", "TT", "TN", "TR", "TM", "TV", "UG", "UA", "UY",
        "UZ", "VU", "WF", "VA", "HU", "VE", "GB", "VN", "IT", "TL", "CI", "BV", "CX", "IM", "SX", "CK",
        "VI", "VG", "HM", "CC", "MH", "FO", "SB", "ST", "TC", "ZM", "CV", "ZW", "AE", "XI",
    };

    // EU member states by ISO code. Their VAT prefixes (TKodyKrajowUE) match the ISO code
    // except Greece, which uses EL. Northern Ireland (XI) is deliberately not included.
    internal static readonly IReadOnlySet<string> EuCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        "AT", "BE", "BG", "CY", "CZ", "DK", "EE", "FI", "FR", "DE", "GR", "HR", "HU", "IE",
        "IT", "LV", "LT", "LU", "MT", "NL", "PL", "PT", "RO", "SK", "SI", "ES", "SE",
    };

    private const string GreeceIso = "GR";
    private const string GreeceVatPrefix = "EL";

    public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

    public static bool IsKnown(string? code) => KnownCodes.Contains(Normalize(code));

    public static bool IsEu(string? code) => EuCodes.Contains(Normalize(code));

    /// <summary>The EU VAT prefix for an EU country, or null for a non-EU country.</summary>
    public static string? EuVatPrefix(string? code)
    {
        var normalized = Normalize(code);
        if (!EuCodes.Contains(normalized))
            return null;
        return normalized == GreeceIso ? GreeceVatPrefix : normalized;
    }

    /// <summary>The ISO country code for an EU VAT prefix, or null when the prefix is not an EU member state's.</summary>
    public static string? FromEuVatPrefix(string? prefix)
    {
        var normalized = Normalize(prefix);
        if (normalized == GreeceVatPrefix)
            return GreeceIso;
        if (normalized == GreeceIso)
            return null;
        return EuCodes.Contains(normalized) ? normalized : null;
    }
}
