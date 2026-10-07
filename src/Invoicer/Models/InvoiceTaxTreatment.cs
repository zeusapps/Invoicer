namespace Invoicer.Models;

public enum InvoiceTaxKind { Domestic, EuServices, NonEuServices }

/// <summary>Ordinary B2B services only. NP classification and buyer tax liability are separate.</summary>
public sealed record InvoiceTaxTreatment(InvoiceTaxKind Kind, int Rate, bool ReverseCharge)
{
    public string Label => Kind switch
    {
        InvoiceTaxKind.Domestic => "Domestic Polish VAT",
        InvoiceTaxKind.EuServices => "EU B2B reverse charge",
        _ => ReverseCharge ? "Outside Polish VAT - reverse charge" : "Outside Polish VAT - no reverse charge",
    };

    public static bool? EffectiveChoice(string? country, bool? choice) =>
        choice ?? (Countries.Normalize(country) == "US" ? false : null);

    public static (InvoiceTaxTreatment? Treatment, string? Error) Resolve(string? country, int rate, bool? choice = null)
    {
        var code = Countries.Normalize(country);
        if (code.Length == 0) return (null, "Client country is required.");
        if (!Countries.IsKnown(code)) return (null, $"Client country '{code}' is not a recognized country code.");
        if (code == Countries.Poland)
        {
            if (rate is not (23 or 22 or 8 or 7 or 5))
                return (null, $"Client VAT rate {rate}% is not supported for Polish clients (supported: 23, 22, 8, 7, 5).");
            if (choice == true) return (null, "Polish domestic VAT requires reverse_charge = false (or omit the setting).");
            return (new(InvoiceTaxKind.Domestic, rate, false), null);
        }
        if (rate != 0)
            return (null, $"Client VAT rate must be 0 for clients outside Poland (country {code}), not {rate}%.");
        if (Countries.IsEu(code))
        {
            if (choice == false) return (null, "EU B2B services require reverse_charge = true (or omit the setting).");
            return (new(InvoiceTaxKind.EuServices, 0, true), null);
        }
        return EffectiveChoice(code, choice) is { } reverseCharge
            ? (new(InvoiceTaxKind.NonEuServices, 0, reverseCharge), null)
            : (null, $"Client in {code} requires an explicit reverse_charge choice (Yes or No).");
    }

    public static InvoiceTaxTreatment ForClient(ClientConfig client)
    {
        var (treatment, error) = Resolve(client.Country, client.VatRate, client.ReverseCharge);
        return treatment ?? throw new InvoiceValidationException([$"Client '{client.Key}': {error}"]);
    }

    public InvoiceAmounts Calculate(decimal net)
    {
        var vat = Kind == InvoiceTaxKind.Domestic ? Math.Round(net * Rate / 100m, 2) : 0m;
        return new(net, vat, net + vat);
    }
}

public sealed record InvoiceAmounts(decimal Net, decimal Vat, decimal Gross);

public sealed class InvoiceValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join(Environment.NewLine, errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
