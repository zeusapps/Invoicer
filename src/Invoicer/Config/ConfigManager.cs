using Tomlyn;
using Tomlyn.Model;
using Invoicer.Models;

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

    public static void Save(AppConfig config)
    {
        var dir = Path.GetDirectoryName(ConfigPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var toml = ToTomlString(config);
        File.WriteAllText(ConfigPath, toml);
    }

    private static AppConfig FromTomlTable(TomlTable table)
    {
        var config = new AppConfig();

        if (table.TryGetValue("supplier", out var supplierObj) && supplierObj is TomlTable supplierTable)
        {
            config.Supplier = new SupplierConfig
            {
                Name = GetString(supplierTable, "name"),
                NameUa = GetString(supplierTable, "name_ua"),
                Tin = GetString(supplierTable, "tin"),
                Regon = GetString(supplierTable, "regon"),
                Vat = GetString(supplierTable, "vat"),
                Address = GetString(supplierTable, "address"),
                AddressUa = GetString(supplierTable, "address_ua"),
                Iban = GetString(supplierTable, "iban"),
                Bank = GetString(supplierTable, "bank"),
                Swift = GetString(supplierTable, "swift"),
            };
        }

        if (table.TryGetValue("output", out var outputObj) && outputObj is TomlTable outputTable)
        {
            config.Output = new OutputConfig
            {
                Directory = GetString(outputTable, "directory"),
                Pattern = GetString(outputTable, "pattern"),
                Filename = GetString(outputTable, "filename"),
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
                    Vat = GetString(clientTable, "vat"),
                    Currency = GetString(clientTable, "currency", "PLN"),
                    VatRate = GetInt(clientTable, "vat_rate"),
                    ServiceDescription = GetString(clientTable, "service_description"),
                    ServiceDescriptionUa = GetString(clientTable, "service_description_ua"),
                    InvoicePrefix = GetString(clientTable, "invoice_prefix"),
                    DefaultAmount = GetDecimal(clientTable, "default_amount"),
                    MonthOffsetRule = GetString(clientTable, "month_offset_rule", "early_previous"),
                    LastInvoiceNumber = GetInt(clientTable, "last_invoice_number"),
                });
            }
        }

        return config;
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
        WriteString(sb, "iban", config.Supplier.Iban);
        WriteString(sb, "bank", config.Supplier.Bank);
        WriteString(sb, "swift", config.Supplier.Swift);

        sb.AppendLine();
        sb.AppendLine("[output]");
        WriteString(sb, "directory", config.Output.Directory);
        WriteString(sb, "pattern", config.Output.Pattern);
        WriteString(sb, "filename", config.Output.Filename);

        foreach (var client in config.Clients)
        {
            sb.AppendLine();
            sb.AppendLine("[[clients]]");
            WriteString(sb, "key", client.Key);
            WriteString(sb, "name", client.Name);
            WriteString(sb, "name_ua", client.NameUa);
            WriteString(sb, "address", client.Address);
            WriteString(sb, "address_ua", client.AddressUa);
            WriteString(sb, "vat", client.Vat);
            WriteString(sb, "currency", client.Currency);
            sb.AppendLine($"vat_rate = {client.VatRate}");
            WriteString(sb, "service_description", client.ServiceDescription);
            WriteString(sb, "service_description_ua", client.ServiceDescriptionUa);
            WriteString(sb, "invoice_prefix", client.InvoicePrefix);
            sb.AppendLine($"default_amount = {client.DefaultAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            WriteString(sb, "month_offset_rule", client.MonthOffsetRule);
            sb.AppendLine($"last_invoice_number = {client.LastInvoiceNumber}");
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
                Tin = "0000000000",
                Regon = "000000000",
                Vat = "PL0000000000",
                Address = "ul. Example 1/1, 00-000 Warsaw, Poland",
                AddressUa = "вул. Приклад 1/1, 00-000 Варшава, Польща",
                Iban = "PL00000000000000000000000000",
                Bank = "Example Bank SA",
                Swift = "EXMPPLPW",
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
                    Vat = "PL0000000000",
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
