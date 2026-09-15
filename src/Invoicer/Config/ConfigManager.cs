using Invoicer.Models;
using Tomlyn;
using Tomlyn.Model;

namespace Invoicer.Config;

public static class ConfigManager
{
    private static string? _configPath;

    public static string ConfigPath
    {
        get => _configPath ??= Path.Combine(AppContext.BaseDirectory, "config.toml");
        set => _configPath = value;
    }

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaultConfig = CreateDefault();
            Save(defaultConfig);
            return defaultConfig;
        }

        var toml = File.ReadAllText(ConfigPath);
        var table = Toml.ToModel(toml);
        return FromTomlTable(table);
    }

    public const string LegacyBillingAccountKey = "DEFAULT";

    public static string BackupPath => ConfigPath + ".bak";

    public static void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // The first save after migrating a legacy file drops the supplier bank keys that older
        // versions read, so keep the original around for a downgrade.
        if (config.PendingLegacyBackup)
        {
            if (File.Exists(ConfigPath))
                File.Copy(ConfigPath, BackupPath, overwrite: true);
            config.PendingLegacyBackup = false;
        }

        var toml = ToTomlString(config);
        File.WriteAllText(ConfigPath, toml);
    }

    private static AppConfig FromTomlTable(TomlTable table)
    {
        var config = new AppConfig();
        TomlTable? supplierTable = null;

        if (table.TryGetValue("supplier", out var supplierObj) && supplierObj is TomlTable supplierTableValue)
        {
            supplierTable = supplierTableValue;
            config.Supplier = new SupplierConfig
            {
                Name = GetString(supplierTable, "name"),
                NameUa = GetString(supplierTable, "name_ua"),
                Tin = GetString(supplierTable, "tin"),
                Regon = GetString(supplierTable, "regon"),
                Vat = GetString(supplierTable, "vat"),
                Address = GetString(supplierTable, "address"),
                AddressUa = GetString(supplierTable, "address_ua"),
            };
        }

        if (table.TryGetValue("billing_accounts", out var accountsObj) && accountsObj is TomlTableArray accountsArray)
        {
            foreach (var accountTable in accountsArray)
            {
                config.BillingAccounts.Add(new BillingAccountConfig
                {
                    Key = GetString(accountTable, "key"),
                    Label = GetString(accountTable, "label"),
                    Iban = GetString(accountTable, "iban"),
                    Bank = GetString(accountTable, "bank"),
                    Swift = GetString(accountTable, "swift"),
                    Currency = GetString(accountTable, "currency"),
                });
            }
        }

        if (table.TryGetValue("output", out var outputObj) && outputObj is TomlTable outputTable)
        {
            config.Output = new OutputConfig
            {
                Directory = GetString(outputTable, "directory"),
                Pattern = GetString(outputTable, "pattern"),
                Filename = GetString(outputTable, "filename"),
                GenerateDocxByDefault = GetBool(outputTable, "generate_docx_by_default", true),
                GeneratePdfByDefault = GetBool(outputTable, "generate_pdf_by_default", true),
                GenerateXmlByDefault = GetBool(outputTable, "generate_xml_by_default", false),
            };
        }

        if (table.TryGetValue("update", out var updateObj) && updateObj is TomlTable updateTable)
        {
            var repository = GetString(updateTable, "repository", "zeusapps/Invoicer");
            config.Update = new UpdateConfig
            {
                CheckOnStartup = GetBool(updateTable, "check_on_startup", true),
                Repository = string.IsNullOrWhiteSpace(repository) ? "zeusapps/Invoicer" : repository,
                DismissedVersion = GetString(updateTable, "dismissed_version"),
            };
        }

        if (table.TryGetValue("clients", out var clientsObj) && clientsObj is TomlTableArray clientsArray)
        {
            foreach (var clientTable in clientsArray)
            {
                config.Clients.Add(new ClientConfig
                {
                    Key = GetString(clientTable, "key"),
                    Name = GetString(clientTable, "name"),
                    NameUa = GetString(clientTable, "name_ua"),
                    Address = GetString(clientTable, "address"),
                    AddressUa = GetString(clientTable, "address_ua"),
                    Country = Countries.Normalize(GetString(clientTable, "country")),
                    Vat = GetString(clientTable, "vat"),
                    BillingAccount = GetString(clientTable, "billing_account"),
                    Currency = GetString(clientTable, "currency", "PLN"),
                    VatRate = GetInt(clientTable, "vat_rate"),
                    ServiceDescription = GetString(clientTable, "service_description"),
                    ServiceDescriptionUa = GetString(clientTable, "service_description_ua"),
                    InvoicePrefix = GetString(clientTable, "invoice_prefix"),
                    DefaultAmount = GetDecimal(clientTable, "default_amount"),
                    MonthOffsetRule = GetString(clientTable, "month_offset_rule", "early_previous"),
                    LastInvoiceNumber = GetInt(clientTable, "last_invoice_number"),
                    Enabled = GetBool(clientTable, "enabled", true),
                });
            }
        }

        MigrateLegacyBillingAccount(config, supplierTable);
        InferMissingClientCountries(config);

        return config;
    }

    /// <summary>
    /// Configs written before billing accounts existed kept a single account on [supplier].
    /// It becomes the DEFAULT account for every client that has none.
    /// </summary>
    private static void MigrateLegacyBillingAccount(AppConfig config, TomlTable? supplierTable)
    {
        if (config.BillingAccounts.Count > 0 || supplierTable is null)
            return;

        var iban = GetString(supplierTable, "iban");
        var bank = GetString(supplierTable, "bank");
        var swift = GetString(supplierTable, "swift");
        if (string.IsNullOrWhiteSpace(iban) && string.IsNullOrWhiteSpace(bank) && string.IsNullOrWhiteSpace(swift))
            return;

        config.BillingAccounts.Add(new BillingAccountConfig
        {
            Key = LegacyBillingAccountKey,
            Label = "Default",
            Iban = iban,
            Bank = bank,
            Swift = swift,
        });

        foreach (var client in config.Clients.Where(c => string.IsNullOrWhiteSpace(c.BillingAccount)))
            client.BillingAccount = LegacyBillingAccountKey;

        config.PendingLegacyBackup = true;
    }

    /// <summary>
    /// Clients written before the country field existed get one only when their VAT carries an
    /// EU prefix. Anything else stays empty: guessing a default country is the bug this replaces.
    /// </summary>
    private static void InferMissingClientCountries(AppConfig config)
    {
        foreach (var client in config.Clients.Where(c => string.IsNullOrWhiteSpace(c.Country)))
        {
            var vat = client.Vat.Trim();
            if (vat.Length < 2)
                continue;

            client.Country = Countries.FromEuVatPrefix(vat[..2]) ?? "";
        }
    }

    private static string ToTomlString(AppConfig config)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("[supplier]");
        WriteString(sb, "name", config.Supplier.Name);
        WriteString(sb, "name_ua", config.Supplier.NameUa);
        WriteString(sb, "tin", config.Supplier.Tin);
        WriteString(sb, "regon", config.Supplier.Regon);
        WriteString(sb, "vat", config.Supplier.Vat);
        WriteString(sb, "address", config.Supplier.Address);
        WriteString(sb, "address_ua", config.Supplier.AddressUa);

        foreach (var account in config.BillingAccounts)
        {
            sb.AppendLine();
            sb.AppendLine("[[billing_accounts]]");
            WriteString(sb, "key", account.Key);
            WriteString(sb, "label", account.Label);
            WriteString(sb, "iban", account.Iban);
            WriteString(sb, "bank", account.Bank);
            WriteString(sb, "swift", account.Swift);
            WriteString(sb, "currency", account.Currency);
        }

        sb.AppendLine();
        sb.AppendLine("[output]");
        WriteString(sb, "directory", config.Output.Directory);
        WriteString(sb, "pattern", config.Output.Pattern);
        WriteString(sb, "filename", config.Output.Filename);
        sb.AppendLine($"generate_docx_by_default = {(config.Output.GenerateDocxByDefault ? "true" : "false")}");
        sb.AppendLine($"generate_pdf_by_default = {(config.Output.GeneratePdfByDefault ? "true" : "false")}");
        sb.AppendLine($"generate_xml_by_default = {(config.Output.GenerateXmlByDefault ? "true" : "false")}");

        sb.AppendLine();
        sb.AppendLine("[update]");
        sb.AppendLine($"check_on_startup = {(config.Update.CheckOnStartup ? "true" : "false")}");
        WriteString(sb, "repository", config.Update.Repository);
        WriteString(sb, "dismissed_version", config.Update.DismissedVersion);

        foreach (var client in config.Clients)
        {
            sb.AppendLine();
            sb.AppendLine("[[clients]]");
            WriteString(sb, "key", client.Key);
            WriteString(sb, "name", client.Name);
            WriteString(sb, "name_ua", client.NameUa);
            WriteString(sb, "address", client.Address);
            WriteString(sb, "address_ua", client.AddressUa);
            WriteString(sb, "country", Countries.Normalize(client.Country));
            WriteString(sb, "vat", client.Vat);
            WriteString(sb, "billing_account", client.BillingAccount);
            WriteString(sb, "currency", client.Currency);
            sb.AppendLine($"vat_rate = {client.VatRate}");
            WriteString(sb, "service_description", client.ServiceDescription);
            WriteString(sb, "service_description_ua", client.ServiceDescriptionUa);
            WriteString(sb, "invoice_prefix", client.InvoicePrefix);
            sb.AppendLine($"default_amount = {client.DefaultAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            WriteString(sb, "month_offset_rule", client.MonthOffsetRule);
            sb.AppendLine($"last_invoice_number = {client.LastInvoiceNumber}");
            sb.AppendLine($"enabled = {(client.Enabled ? "true" : "false")}");
        }

        return sb.ToString();
    }

    private static void WriteString(System.Text.StringBuilder sb, string key, string value)
    {
        sb.AppendLine($"{key} = \"{EscapeToml(value)}\"");
    }

    private static string EscapeToml(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    private static string GetString(TomlTable table, string key, string defaultValue = "")
    {
        return table.TryGetValue(key, out var val) ? val?.ToString() ?? defaultValue : defaultValue;
    }

    private static int GetInt(TomlTable table, string key, int defaultValue = 0)
    {
        if (table.TryGetValue(key, out var val))
        {
            if (val is long l) return (int)l;
            if (val is int i) return i;
            if (int.TryParse(val?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }

    private static bool GetBool(TomlTable table, string key, bool defaultValue = false)
    {
        if (table.TryGetValue(key, out var val))
        {
            if (val is bool b) return b;
            if (bool.TryParse(val?.ToString(), out var parsed)) return parsed;
        }
        return defaultValue;
    }

    private static decimal GetDecimal(TomlTable table, string key, decimal defaultValue = 0)
    {
        if (table.TryGetValue(key, out var val))
        {
            if (val is double d) return (decimal)d;
            if (val is long l) return l;
            if (decimal.TryParse(val?.ToString(), System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                return parsed;
        }
        return defaultValue;
    }

    public static AppConfig CreateDefault()
    {
        return new AppConfig
        {
            Supplier = new SupplierConfig
            {
                Name = "John Doe",
                NameUa = "Джон Доу",
                // Placeholders must satisfy the FA(3) type TNrNIP ([1-9]((\d[1-9])|([1-9]\d))\d{7}),
                // otherwise the default config cannot produce a valid KSeF invoice.
                Tin = "1111111111",
                Regon = "000000000",
                Vat = "PL1111111111",
                Address = "ul. Example 1/1, 00-000 Warsaw, Poland",
                AddressUa = "вул. Приклад 1/1, 00-000 Варшава, Польща",
            },
            BillingAccounts = new List<BillingAccountConfig>
            {
                new()
                {
                    Key = "PLN",
                    Label = "Example Bank PLN",
                    Iban = "PL00000000000000000000000000",
                    Bank = "Example Bank SA",
                    Swift = "EXMPPLPW",
                    Currency = "PLN",
                },
            },
            Output = new OutputConfig(),
            Clients = new List<ClientConfig>
            {
                new()
                {
                    Key = "SAMPLE",
                    Name = "Sample Client Sp. z o.o.",
                    NameUa = "ТОВ «Зразок»",
                    Address = "1 Main St., 00-000 Warsaw, Poland",
                    AddressUa = "вул. Головна 1, 00-000 Варшава, Польща",
                    Country = Countries.Poland,
                    Vat = "PL9999999999",
                    BillingAccount = "PLN",
                    Currency = "PLN",
                    VatRate = 23,
                    ServiceDescription = "Services according to agreement",
                    ServiceDescriptionUa = "Надання послуг згідно з договором",
                    InvoicePrefix = "SM",
                    DefaultAmount = 1000.00m,
                    MonthOffsetRule = "early_previous",
                },
            },
        };
    }
}
