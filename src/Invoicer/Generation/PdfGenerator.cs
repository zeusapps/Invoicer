using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Invoicer.Models;

namespace Invoicer.Generation;

public static class PdfGenerator
{
    private static bool _fontsRegistered;

    private static void EnsureFontsRegistered()
    {
        if (_fontsRegistered) return;
        _fontsRegistered = true;

        var assembly = typeof(PdfGenerator).Assembly;
        foreach (var name in new[] { "Lato-Regular.ttf", "Lato-Bold.ttf", "Lato-Italic.ttf" })
        {
            using var stream = assembly.GetManifestResourceStream($"Invoicer.Resources.Fonts.{name}");
            if (stream != null)
                FontManager.RegisterFont(stream);
        }
    }

    public static void Generate(Invoice invoice)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        EnsureFontsRegistered();
        Directory.CreateDirectory(invoice.OutputDirectory);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.MarginHorizontal(1.5f, Unit.Centimetre);
                page.MarginVertical(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Content().Column(col =>
                {
                    // Title
                    col.Item().PaddingBottom(10).Column(titleCol =>
                    {
                        titleCol.Item().AlignCenter().Text($"Faktura / Invoice {invoice.FormattedNumber}")
                            .Bold().FontSize(14);
                        titleCol.Item().AlignCenter().Text($"Рахунок-фактура {invoice.FormattedNumber}")
                            .Italic().FontSize(11).FontColor(Colors.Grey.Darken1);
                    });

                    // Info table
                    col.Item().PaddingBottom(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(35);
                            columns.RelativeColumn(65);
                        });

                        AddInfoRow(table, "Invoice date / Дата рахунку-фактури:",
                            invoice.InvoiceDate.ToString("dd.MM.yyyy"));

                        AddInfoRow(table, "Service period / Період надання послуг:",
                            $"{new DateTime(invoice.ServiceMonth.Year, invoice.ServiceMonth.Month, 1):MMMM yyyy}");

                        AddInfoRow(table, "Service / Послуга:",
                            $"{invoice.ServiceDescription}\n{invoice.ServiceDescriptionUa}");

                        AddInfoRow(table, "Supplier / Постачальник:",
                            $"{invoice.Supplier.Name} / {invoice.Supplier.NameUa}\n" +
                            $"NIP/TIN: {invoice.Supplier.Tin}, REGON: {invoice.Supplier.Regon}\n" +
                            $"VAT EU: {invoice.Supplier.Vat}\n" +
                            $"{invoice.Supplier.Address}\n{invoice.Supplier.AddressUa}");

                        AddInfoRow(table, "Customer / Замовник:",
                            $"{invoice.Client.Name} / {invoice.Client.NameUa}\n" +
                            $"VAT: {invoice.Client.Vat}\n" +
                            $"{invoice.Client.Address}\n{invoice.Client.AddressUa}");

                        AddInfoRow(table, "Bank account / Банківський рахунок:",
                            $"IBAN: {invoice.Supplier.Iban}\n" +
                            $"Bank: {invoice.Supplier.Bank}\n" +
                            $"SWIFT: {invoice.Supplier.Swift}");

                        AddInfoRow(table, "Payment method / Спосіб оплати:",
                            "Bank transfer / Банківський переказ");

                        AddInfoRow(table, "Payment due / Термін оплати:",
                            $"{invoice.InvoiceDate.AddDays(14):dd.MM.yyyy} (14 days / днів)");
                    });

                    // Services table
                    col.Item().PaddingBottom(10).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);   // No.
                            columns.RelativeColumn(4);    // Description
                            columns.RelativeColumn(1.5f); // Net
                            columns.RelativeColumn(1);    // VAT %
                            columns.RelativeColumn(1.2f); // VAT
                            columns.RelativeColumn(1.5f); // Gross
                        });

                        // Header
                        AddServicesHeaderCell(table, "No.\nНомер");
                        AddServicesHeaderCell(table, "Description\nОпис");
                        AddServicesHeaderCell(table, "Net\nНетто");
                        AddServicesHeaderCell(table, "VAT %\nПДВ %");
                        AddServicesHeaderCell(table, "VAT\nПДВ");
                        AddServicesHeaderCell(table, "Gross\nБрутто");

                        // Data row
                        AddServicesCell(table, "1");
                        AddServicesCell(table, $"{invoice.ServiceDescription}\n{invoice.ServiceDescriptionUa}");
                        AddServicesCell(table, FormatAmount(invoice.NetAmount, invoice.Currency));
                        AddServicesCell(table, invoice.VatRate > 0 ? $"{invoice.VatRate}%" : "N/A");
                        AddServicesCell(table, invoice.VatRate > 0 ? FormatAmount(invoice.VatAmount, invoice.Currency) : "N/A");
                        AddServicesCell(table, FormatAmount(invoice.GrossAmount, invoice.Currency));

                        // Total row
                        AddServicesTotalCell(table, "");
                        AddServicesTotalCell(table, "Total / Всього");
                        AddServicesTotalCell(table, FormatAmount(invoice.NetAmount, invoice.Currency));
                        AddServicesTotalCell(table, "");
                        AddServicesTotalCell(table, invoice.VatRate > 0 ? FormatAmount(invoice.VatAmount, invoice.Currency) : "N/A");
                        AddServicesTotalCell(table, FormatAmount(invoice.GrossAmount, invoice.Currency));
                    });

                    // Footer
                    col.Item().Column(footerCol =>
                    {
                        footerCol.Item().Text(text =>
                        {
                            text.Span($"Total amount due: {FormatAmount(invoice.GrossAmount, invoice.Currency)}")
                                .Bold().FontSize(10);
                        });
                        footerCol.Item().Text(text =>
                        {
                            text.Span($"Загальна сума до оплати: {FormatAmount(invoice.GrossAmount, invoice.Currency)}")
                                .Italic().FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        footerCol.Item().PaddingTop(40).Text("___________________________").FontSize(10);
                        footerCol.Item().Text($"Signature / Підпис: {invoice.Supplier.Name}").FontSize(9);
                    });
                });
            });
        }).GeneratePdf(invoice.PdfPath);
    }

    private static void AddInfoRow(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(4)
            .Text(label).Bold().FontSize(8);
        table.Cell().Border(0.5f).Padding(4)
            .Text(value).FontSize(8);
    }

    private static void AddServicesHeaderCell(TableDescriptor table, string text)
    {
        table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(3)
            .AlignCenter().Text(text).Bold().FontSize(8);
    }

    private static void AddServicesCell(TableDescriptor table, string text)
    {
        table.Cell().Border(0.5f).Padding(3)
            .Text(text).FontSize(8);
    }

    private static void AddServicesTotalCell(TableDescriptor table, string text)
    {
        table.Cell().Border(0.5f).Background(Colors.Grey.Lighten3).Padding(3)
            .Text(text).Bold().FontSize(8);
    }

    private static string FormatAmount(decimal amount, string currency)
    {
        return $"{amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} {currency}";
    }
}
