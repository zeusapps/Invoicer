namespace Invoicer.Models;

public class BillingAccountConfig
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Iban { get; set; } = "";
    public string Bank { get; set; } = "";
    public string Swift { get; set; } = "";
    public string Currency { get; set; } = "";
}

public sealed class BillingAccountNotFoundException : Exception
{
    public string ClientKey { get; }
    public string AccountKey { get; }

    public BillingAccountNotFoundException(string clientKey, string accountKey)
        : base(string.IsNullOrWhiteSpace(accountKey)
            ? $"Client '{clientKey}' has no billing account assigned."
            : $"Client '{clientKey}' references billing account '{accountKey}', which does not exist.")
    {
        ClientKey = clientKey;
        AccountKey = accountKey;
    }
}
