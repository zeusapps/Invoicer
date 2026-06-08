using System.Globalization;
using System.Text;
using System.Xml;
using Invoicer.Models;

namespace Invoicer.Generation;

public static class KsefXmlGenerator
{
    public static class Metadata
    {
        public const string Namespace = "http://crd.gov.pl/wzor/2025/06/25/13775/";
        public const string FormCodeValue = "FA";
        public const string FormCodeSystem = "FA (3)";
        public const string SchemaVersion = "1-0E";
        public const int FormVariant = 3;
        public const string SystemInfo = "Aplikacja Podatnika KSeF";
    }

    public static void Generate(Invoice invoice)
    {
        var errors = Validate(invoice);
        if (errors.Count > 0)
        {
            throw new KsefValidationException(errors);
        }

        var document = KsefInvoiceDocument.FromInvoice(invoice);

        Directory.CreateDirectory(invoice.OutputDirectory);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
        };

        using var stream = File.Create(invoice.XmlPath);
        using var writer = XmlWriter.Create(stream, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("Faktura", Metadata.Namespace);
        WriteHeader(writer, document.Header);
        WriteParty(writer, "Podmiot1", document.Seller);
        WriteParty(writer, "Podmiot2", document.Buyer);
        WriteInvoiceBody(writer, document.Body);
        writer.WriteEndElement();
        writer.WriteEndDocument();
    }

    internal static List<string> Validate(Invoice invoice)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(invoice.FormattedNumber))
            errors.Add("Invoice number is required (FormattedNumber).");
        if (invoice.InvoiceDate == default)
            errors.Add("Invoice date is required.");
        if (string.IsNullOrWhiteSpace(invoice.Currency))
            errors.Add("Invoice currency is required.");
        if (invoice.NetAmount <= 0)
            errors.Add("Net amount must be greater than zero.");
        if (invoice.GrossAmount <= 0)
            errors.Add("Gross amount must be greater than zero.");
        if (string.IsNullOrWhiteSpace(invoice.ServiceDescription))
            errors.Add("Service description is required.");

        if (string.IsNullOrWhiteSpace(invoice.Supplier.Tin))
            errors.Add("Supplier TIN/NIP is required.");
        if (string.IsNullOrWhiteSpace(invoice.Supplier.Name))
            errors.Add("Supplier name is required.");
        if (string.IsNullOrWhiteSpace(invoice.Supplier.Address))
            errors.Add("Supplier address is required.");

        if (string.IsNullOrWhiteSpace(invoice.Client.Name))
            errors.Add("Client name is required.");
        if (string.IsNullOrWhiteSpace(invoice.Client.Vat))
            errors.Add("Client VAT/NrID is required.");
        if (string.IsNullOrWhiteSpace(invoice.Client.Address))
            errors.Add("Client address is required.");

        return errors;
    }

    private static void WriteHeader(XmlWriter writer, KsefHeader header)
    {
        writer.WriteStartElement("Naglowek");

        writer.WriteStartElement("KodFormularza");
        writer.WriteAttributeString("kodSystemowy", header.FormCodeSystem);
        writer.WriteAttributeString("wersjaSchemy", header.SchemaVersion);
        writer.WriteString(header.FormCodeValue);
        writer.WriteEndElement();

        writer.WriteElementString("WariantFormularza", header.FormVariant.ToString(CultureInfo.InvariantCulture));
        writer.WriteElementString("DataWytworzeniaFa", header.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture));
        writer.WriteElementString("SystemInfo", header.SystemInfo);

        writer.WriteEndElement();
    }

    private static void WriteParty(XmlWriter writer, string elementName, KsefParty party)
    {
        writer.WriteStartElement(elementName);

        writer.WriteStartElement("DaneIdentyfikacyjne");
        if (!string.IsNullOrWhiteSpace(party.CountryCode))
            writer.WriteElementString("KodKraju", party.CountryCode);

        if (!string.IsNullOrWhiteSpace(party.Nip))
            writer.WriteElementString("NIP", party.Nip);

        if (!string.IsNullOrWhiteSpace(party.Identifier))
            writer.WriteElementString("NrID", party.Identifier);

        writer.WriteElementString("Nazwa", party.Name);
        writer.WriteEndElement();

        writer.WriteStartElement("Adres");
        writer.WriteElementString("KodKraju", party.AddressCountryCode);
        writer.WriteElementString("AdresL1", party.AddressLine1);
        writer.WriteEndElement();

        // Buyer block in KSeF examples includes mandatory indicators JST/GV.
        if (elementName == "Podmiot2")
        {
            writer.WriteElementString("JST", "2");
            writer.WriteElementString("GV", "2");
        }

        writer.WriteEndElement();
    }

    private static void WriteInvoiceBody(XmlWriter writer, KsefInvoiceBody body)
    {
        writer.WriteStartElement("Fa");

        writer.WriteElementString("KodWaluty", body.CurrencyCode);
        writer.WriteElementString("P_1", body.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_2", body.InvoiceNumber);

        writer.WriteElementString("P_13_1", body.NetAmount.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_14_1", body.VatAmount.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_15", body.GrossAmount.ToString("0.##", CultureInfo.InvariantCulture));

        writer.WriteStartElement("Adnotacje");
        writer.WriteElementString("P_16", "2");
        writer.WriteElementString("P_17", "2");
        writer.WriteElementString("P_18", "2");
        writer.WriteElementString("P_18A", "2");
        writer.WriteStartElement("Zwolnienie");
        writer.WriteElementString("P_19N", "1");
        writer.WriteEndElement();
        writer.WriteStartElement("NoweSrodkiTransportu");
        writer.WriteElementString("P_22N", "1");
        writer.WriteEndElement();
        writer.WriteElementString("P_23", "2");
        writer.WriteStartElement("PMarzy");
        writer.WriteElementString("P_PMarzyN", "1");
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteElementString("RodzajFaktury", "VAT");

        writer.WriteStartElement("FaWiersz");
        writer.WriteElementString("NrWierszaFa", "1");
        writer.WriteElementString("P_7", body.Line.Description);
        writer.WriteElementString("P_8A", body.Line.UnitCode);
        writer.WriteElementString("P_8B", body.Line.Quantity.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_9A", body.Line.UnitNetAmount.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_11", body.Line.NetAmount.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_12", body.Line.VatRatePercent.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();

        writer.WriteStartElement("Platnosc");
        writer.WriteElementString("FormaPlatnosci", "6");
        writer.WriteStartElement("RachunekBankowy");
        writer.WriteElementString("NrRB", body.Payment.Iban);
        if (!string.IsNullOrWhiteSpace(body.Payment.Swift))
            writer.WriteElementString("SWIFT", body.Payment.Swift);
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement();
    }

    internal sealed record KsefInvoiceDocument(KsefHeader Header, KsefParty Seller, KsefParty Buyer, KsefInvoiceBody Body)
    {
        public static KsefInvoiceDocument FromInvoice(Invoice invoice)
        {
            var buyerCountryCode = ExtractCountryCode(invoice.Client.Vat);
            var buyerIdentifier = invoice.Client.Vat;

            return new KsefInvoiceDocument(
                new KsefHeader(
                    Metadata.FormCodeValue,
                    Metadata.FormCodeSystem,
                    Metadata.SchemaVersion,
                    Metadata.FormVariant,
                    DateTime.SpecifyKind(invoice.InvoiceDate.Date, DateTimeKind.Utc),
                    Metadata.SystemInfo),
                new KsefParty(
                    CountryCode: "PL",
                    Nip: invoice.Supplier.Tin,
                    Identifier: null,
                    Name: invoice.Supplier.Name,
                    AddressCountryCode: "PL",
                    AddressLine1: NormalizeWhitespace(invoice.Supplier.Address)),
                new KsefParty(
                    CountryCode: buyerCountryCode,
                    Nip: null,
                    Identifier: buyerIdentifier,
                    Name: invoice.Client.Name,
                    AddressCountryCode: buyerCountryCode,
                    AddressLine1: NormalizeWhitespace(invoice.Client.Address)),
                new KsefInvoiceBody(
                    CurrencyCode: invoice.Currency,
                    InvoiceDate: invoice.InvoiceDate,
                    InvoiceNumber: invoice.FormattedNumber,
                    NetAmount: invoice.NetAmount,
                    VatAmount: invoice.VatAmount,
                    GrossAmount: invoice.GrossAmount,
                    Line: new KsefInvoiceLine(
                        Description: invoice.ServiceDescription,
                        UnitCode: "szt",
                        Quantity: 1m,
                        UnitNetAmount: invoice.NetAmount,
                        NetAmount: invoice.NetAmount,
                        VatRatePercent: invoice.VatRate),
                    Payment: new KsefPayment(
                        Iban: invoice.Supplier.Iban,
                        Swift: invoice.Supplier.Swift))
            );
        }

        private static string ExtractCountryCode(string identifier)
        {
            if (identifier.Length >= 2 && char.IsLetter(identifier[0]) && char.IsLetter(identifier[1]))
                return identifier.Substring(0, 2).ToUpperInvariant();
            return "PL";
        }

        private static string NormalizeWhitespace(string value)
        {
            return value.Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }

    internal sealed record KsefHeader(
        string FormCodeValue,
        string FormCodeSystem,
        string SchemaVersion,
        int FormVariant,
        DateTime CreatedAtUtc,
        string SystemInfo);

    internal sealed record KsefParty(
        string? CountryCode,
        string? Nip,
        string? Identifier,
        string Name,
        string AddressCountryCode,
        string AddressLine1);

    internal sealed record KsefInvoiceBody(
        string CurrencyCode,
        DateTime InvoiceDate,
        string InvoiceNumber,
        decimal NetAmount,
        decimal VatAmount,
        decimal GrossAmount,
        KsefInvoiceLine Line,
        KsefPayment Payment);

    internal sealed record KsefInvoiceLine(
        string Description,
        string UnitCode,
        decimal Quantity,
        decimal UnitNetAmount,
        decimal NetAmount,
        int VatRatePercent);

    internal sealed record KsefPayment(string Iban, string? Swift);
}

public sealed class KsefValidationException : Exception
{
    public IReadOnlyList<string> Errors { get; }

    public KsefValidationException(IReadOnlyList<string> errors)
        : base("KSeF validation failed:")
    {
        Errors = errors;
    }

    public override string ToString()
    {
        return $"{Message}{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", Errors)}";
    }
}
