namespace Invoicer.Models;

/// <summary>
/// Editing rules for billing accounts, kept out of the TUI views so they can be tested.
/// </summary>
public static class BillingAccountRules
{
    /// <summary>An error message when any key is empty or duplicated, otherwise null.</summary>
    public static string? ValidateKeys(IEnumerable<BillingAccountConfig> accounts)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var account in accounts)
        {
            var key = account.Key.Trim();
            if (key.Length == 0)
                return "Every billing account needs a key.";
            if (!seen.Add(key))
                return $"Billing account key '{key}' is used more than once.";
        }

        return null;
    }

    /// <summary>
    /// Points clients at the new keys of renamed accounts. All renames are applied in one pass,
    /// so swapping two keys does not collapse both clients onto one account.
    /// </summary>
    public static void ApplyKeyRenames(IEnumerable<ClientConfig> clients, IEnumerable<(string OldKey, string NewKey)> renames)
    {
        var map = renames
            .Where(r => r.OldKey != r.NewKey)
            .ToDictionary(r => r.OldKey, r => r.NewKey, StringComparer.Ordinal);
        if (map.Count == 0)
            return;

        foreach (var client in clients)
        {
            if (map.TryGetValue(client.BillingAccount, out var newKey))
                client.BillingAccount = newKey;
        }
    }

    /// <summary>Keys of the clients assigned to the account with <paramref name="accountKey"/>.</summary>
    public static IReadOnlyList<string> ClientsReferencing(IEnumerable<ClientConfig> clients, string accountKey)
    {
        return clients
            .Where(c => c.BillingAccount.Length > 0 && c.BillingAccount == accountKey)
            .Select(c => c.Key)
            .ToList();
    }

    /// <summary>
    /// A warning when the account declares a currency that differs from the client's, otherwise null.
    /// Informational only: a multi-currency account can legitimately receive another currency.
    /// </summary>
    public static string? CurrencyMismatchWarning(ClientConfig client, BillingAccountConfig? account)
    {
        if (account is null || string.IsNullOrWhiteSpace(account.Currency))
            return null;

        return string.Equals(account.Currency.Trim(), client.Currency.Trim(), StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Client currency is {client.Currency}, account currency is {account.Currency}.";
    }
}
