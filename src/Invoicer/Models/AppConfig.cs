namespace Invoicer.Models;

public class AppConfig
{
    public SupplierConfig Supplier { get; set; } = new();
    public List<BillingAccountConfig> BillingAccounts { get; set; } = new();
    public OutputConfig Output { get; set; } = new();
    public UpdateConfig Update { get; set; } = new();
    public List<ClientConfig> Clients { get; set; } = new();

    /// <summary>
    /// Set when this config was migrated from the legacy single-account shape; the next save
    /// backs up the original file before overwriting it.
    /// </summary>
    internal bool PendingLegacyBackup { get; set; }

    /// <summary>The client's billing account, or null when the key is empty or unknown.</summary>
    public BillingAccountConfig? FindBillingAccount(ClientConfig client)
    {
        if (string.IsNullOrWhiteSpace(client.BillingAccount))
            return null;
        return BillingAccounts.FirstOrDefault(a => a.Key == client.BillingAccount);
    }

    /// <summary>
    /// The client's billing account. Never falls back to another account: an invoice paid
    /// into the wrong account is worse than one that is not generated.
    /// </summary>
    public BillingAccountConfig ResolveBillingAccount(ClientConfig client)
    {
        return FindBillingAccount(client)
            ?? throw new BillingAccountNotFoundException(client.Key, client.BillingAccount);
    }
}
