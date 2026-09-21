using System.Globalization;
using System.Text;
using System.Xml;
using Invoicer.Exchange;
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

    public static void Generate(Invoice invoice, TimeProvider? timeProvider = null)
    {
        var errors = Validate(invoice);
        if (errors.Count > 0)
        {
            throw new KsefValidationException(errors);
        }

        var document = KsefInvoiceDocument.FromInvoice(invoice, timeProvider ?? TimeProvider.System);

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
        if (string.IsNullOrWhiteSpace(invoice.Client.Address))
            errors.Add("Client address is required.");

        var (_, identificationError) = BuyerIdentification.Resolve(invoice.Client.Country, invoice.Client.Vat);
        if (identificationError is not null)
        {
            errors.Add(identificationError);
        }
        else
        {
            // Only meaningful once the country is known to be valid.
            var (_, rateError) = TaxRateCoding.Resolve(invoice.Client.Country, invoice.VatRate);
            if (rateError is not null)
                errors.Add(rateError);
        }

        if (string.IsNullOrWhiteSpace(invoice.BillingAccount.Iban))
            errors.Add($"Billing account '{invoice.BillingAccount.Key}' IBAN is required.");

        errors.AddRange(ValidateCurrency(invoice));

        return errors;
    }

    /// <summary>
    /// The maximum fraction digits TIlosci can hold, which is what bounds KursWaluty. NBP
    /// publishes some low-value currencies more precisely than this; such a rate is rejected
    /// rather than rounded to fit, because rounding it is the very defect this field guards.
    /// </summary>
    internal const int MaxRateDecimals = 6;

    private static IEnumerable<string> ValidateCurrency(Invoice invoice)
    {
        if (!ExchangeRateRules.RequiresRate(invoice.Currency))
            yield break;

        var currency = ExchangeRateRules.Normalize(invoice.Currency);

        // FA(3) expects the tax fields of a foreign-currency invoice in PLN while the sales
        // values stay in the invoice currency. That conversion is not implemented, so emitting
        // the amounts unconverted would produce a schema-valid but factually wrong invoice.
        if (Countries.Normalize(invoice.Client.Country) == Countries.Poland)
        {
            yield return $"A Polish client cannot be invoiced in a foreign currency ({currency}) for KSeF. "
                         + "Convert the invoice to PLN, or set the client country to the buyer's country.";
            yield break;
        }

        if (invoice.ExchangeRate is not { } rate)
        {
            yield return $"An exchange rate is required for a {currency} invoice.";
            yield break;
        }

        if (rate <= 0)
        {
            yield return $"The exchange rate must be greater than zero, not {rate.ToString(CultureInfo.InvariantCulture)}.";
            yield break;
        }

        // Whether the value survives six decimal places, not how many digits it was written
        // with: 3.79980000 carries a scale of 8 but loses nothing, while 0.00021425 does.
        if (decimal.Round(rate, MaxRateDecimals) != rate)
        {
            yield return $"The exchange rate {rate.ToString(CultureInfo.InvariantCulture)} has more than "
                         + $"{MaxRateDecimals} decimal places, which KSeF cannot represent. "
                         + "Rounding it would misstate the PLN taxable base.";
        }
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

        if (!string.IsNullOrWhiteSpace(party.EuVatPrefix))
            writer.WriteElementString("PrefiksPodatnika", party.EuVatPrefix);

        // TPodmiot1 is a closed sequence of NIP + Nazwa, so the seller is always the Nip form;
        // only the buyer uses the other TPodmiot2 choices. The seller's country lives in Adres.
        writer.WriteStartElement("DaneIdentyfikacyjne");
        switch (party.Identification)
        {
            case BuyerIdentification.Nip nip:
                writer.WriteElementString("NIP", nip.Value);
                break;
            case BuyerIdentification.EuVat euVat:
                writer.WriteElementString("KodUE", euVat.CountryPrefix);
                writer.WriteElementString("NrVatUE", euVat.Number);
                break;
            case BuyerIdentification.ForeignId foreignId:
                writer.WriteElementString("KodKraju", foreignId.CountryCode);
                writer.WriteElementString("NrID", foreignId.Identifier);
                break;
            case BuyerIdentification.NoId:
                writer.WriteElementString("BrakID", "1");
                break;
            default:
                throw new InvalidOperationException($"Unsupported identification {party.Identification.GetType().Name}.");
        }

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

        // One summary pair per invoice, chosen by the rate coding: P_13_1..3 carry a Polish tax
        // amount in P_14_n; P_13_8/P_13_9 (np I / np II) have no tax amount at all.
        var summary = body.TaxRate.SummaryIndex.ToString(CultureInfo.InvariantCulture);
        writer.WriteElementString($"P_13_{summary}", body.NetAmount.ToString("0.##", CultureInfo.InvariantCulture));
        if (body.TaxRate.HasTaxAmount)
            writer.WriteElementString($"P_14_{summary}", body.VatAmount.ToString("0.##", CultureInfo.InvariantCulture));
        writer.WriteElementString("P_15", body.GrossAmount.ToString("0.##", CultureInfo.InvariantCulture));

        writer.WriteStartElement("Adnotacje");
        writer.WriteElementString("P_16", "2");
        writer.WriteElementString("P_17", "2");
        writer.WriteElementString("P_18", body.TaxRate.ReverseCharge ? "1" : "2");
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
        writer.WriteElementString("P_12", body.TaxRate.RateCode);
        // Last element of FaWiersz, after Procedura and before StanPrzed, per the FA(3) sequence.
        // Written at the precision NBP published: "0.##" used for amounts would silently truncate
        // a six-decimal rate, and a silently rounded rate misstates the PLN taxable base.
        if (body.ExchangeRate is { } exchangeRate)
            writer.WriteElementString("KursWaluty", exchangeRate.ToString("0.######", CultureInfo.InvariantCulture));
        writer.WriteEndElement();

        writer.WriteStartElement("Platnosc");
        writer.WriteElementString("FormaPlatnosci", "6");
        writer.WriteStartElement("RachunekBankowy");
        writer.WriteElementString("NrRB", body.Payment.Iban);
        if (!string.IsNullOrWhiteSpace(body.Payment.Swift))
            writer.WriteElementString("SWIFT", body.Payment.Swift);
        if (!string.IsNullOrWhiteSpace(body.Payment.BankName))
            writer.WriteElementString("NazwaBanku", body.Payment.BankName);
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteEndElement();
    }

    internal sealed record KsefInvoiceDocument(KsefHeader Header, KsefParty Seller, KsefParty Buyer, KsefInvoiceBody Body)
    {
        public static KsefInvoiceDocument FromInvoice(Invoice invoice, TimeProvider timeProvider)
        {
            var (buyerIdentification, identificationError) =
                BuyerIdentification.Resolve(invoice.Client.Country, invoice.Client.Vat);
            if (buyerIdentification is null)
                throw new InvalidOperationException(identificationError);

            var (taxRate, rateError) = TaxRateCoding.Resolve(invoice.Client.Country, invoice.VatRate);
            if (taxRate is null)
                throw new InvalidOperationException(rateError);

            return new KsefInvoiceDocument(
                new KsefHeader(
                    Metadata.FormCodeValue,
                    Metadata.FormCodeSystem,
                    Metadata.SchemaVersion,
                    Metadata.FormVariant,
                    // Time the document was produced, not the invoice date: FA(3) bounds this
                    // field to 2025-09-01Z..2050-01-01Z, which an invoice date can fall outside.
                    timeProvider.GetUtcNow().UtcDateTime,
                    Metadata.SystemInfo),
                new KsefParty(
                    EuVatPrefix: taxRate.SellerEuPrefix,
                    Identification: new BuyerIdentification.Nip(invoice.Supplier.Tin),
                    Name: invoice.Supplier.Name,
                    AddressCountryCode: Countries.Poland,
                    AddressLine1: NormalizeWhitespace(invoice.Supplier.Address)),
                new KsefParty(
                    EuVatPrefix: null,
                    Identification: buyerIdentification,
                    Name: invoice.Client.Name,
                    AddressCountryCode: Countries.Normalize(invoice.Client.Country),
                    AddressLine1: NormalizeWhitespace(invoice.Client.Address)),
                new KsefInvoiceBody(
                    CurrencyCode: invoice.Currency,
                    // Read from the invoice, never looked up here: generation stays synchronous
                    // and network-free so its output remains deterministic.
                    ExchangeRate: ExchangeRateRules.RequiresRate(invoice.Currency) ? invoice.ExchangeRate : null,
                    InvoiceDate: invoice.InvoiceDate,
                    InvoiceNumber: invoice.FormattedNumber,
                    NetAmount: invoice.NetAmount,
                    VatAmount: invoice.VatAmount,
                    GrossAmount: invoice.GrossAmount,
                    TaxRate: taxRate,
                    Line: new KsefInvoiceLine(
                        Description: invoice.ServiceDescription,
                        UnitCode: "szt",
                        Quantity: 1m,
                        UnitNetAmount: invoice.NetAmount,
                        NetAmount: invoice.NetAmount),
                    Payment: new KsefPayment(
                        Iban: RemoveWhitespace(invoice.BillingAccount.Iban),
                        Swift: invoice.BillingAccount.Swift.Trim(),
                        BankName: invoice.BillingAccount.Bank.Trim()))
            );
        }

        private static string RemoveWhitespace(string value)
        {
            return string.Concat(value.Where(c => !char.IsWhiteSpace(c)));
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
        string? EuVatPrefix,
        BuyerIdentification Identification,
        string Name,
        string AddressCountryCode,
        string AddressLine1);

    internal sealed record KsefInvoiceBody(
        string CurrencyCode,
        decimal? ExchangeRate,
        DateTime InvoiceDate,
        string InvoiceNumber,
        decimal NetAmount,
        decimal VatAmount,
        decimal GrossAmount,
        TaxRateCoding TaxRate,
        KsefInvoiceLine Line,
        KsefPayment Payment);

    internal sealed record KsefInvoiceLine(
        string Description,
        string UnitCode,
        decimal Quantity,
        decimal UnitNetAmount,
        decimal NetAmount);

    internal sealed record KsefPayment(string Iban, string? Swift, string? BankName);
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
