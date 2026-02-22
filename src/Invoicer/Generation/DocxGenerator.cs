using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Invoicer.Models;

namespace Invoicer.Generation;

public static class DocxGenerator
{
    public static void Generate(Invoice invoice)
    {
        Directory.CreateDirectory(invoice.OutputDirectory);
        using var doc = WordprocessingDocument.Create(invoice.DocxPath, WordprocessingDocumentType.Document);
        var mainPart = doc.AddMainDocumentPart();
        mainPart.Document = new Document();
        var body = mainPart.Document.AppendChild(new Body());

        // Page setup
        var sectionProps = new SectionProperties(
            new PageSize { Width = 11906, Height = 16838 }, // A4
            new PageMargin { Top = 720, Right = 720, Bottom = 720, Left = 720, Header = 0, Footer = 0 }
        );

        // Title
        AddTitle(body, $"Faktura / Invoice {invoice.FormattedNumber}",
            $"Рахунок-фактура {invoice.FormattedNumber}");

        body.AppendChild(new Paragraph());

        // Info table
        AddInfoTable(body, invoice);

        body.AppendChild(new Paragraph());

        // Services table
        AddServicesTable(body, invoice);

        body.AppendChild(new Paragraph());

        // Footer
        AddFooter(body, invoice);

        body.AppendChild(sectionProps);
    }

    private static void AddTitle(Body body, string titleEn, string titleUa)
    {
        var para = new Paragraph();
        var pProps = new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { After = "0" }
        );
        para.AppendChild(pProps);

        var run = new Run();
        run.AppendChild(new RunProperties(
            new Bold(),
            new FontSize { Val = "28" }
        ));
        run.AppendChild(new Text(titleEn));
        para.AppendChild(run);
        body.AppendChild(para);

        var paraUa = new Paragraph();
        var pPropsUa = new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new SpacingBetweenLines { After = "0" }
        );
        paraUa.AppendChild(pPropsUa);
        var runUa = new Run();
        runUa.AppendChild(new RunProperties(
            new Italic(),
            new FontSize { Val = "22" },
            new Color { Val = "666666" }
        ));
        runUa.AppendChild(new Text(titleUa));
        paraUa.AppendChild(runUa);
        body.AppendChild(paraUa);
    }

    private static void AddInfoTable(Body body, Invoice invoice)
    {
        var table = CreateTable(2);

        AddInfoRow(table,
            "Invoice date / Дата рахунку-фактури:",
            invoice.InvoiceDate.ToString("dd.MM.yyyy"));

        AddInfoRow(table,
            "Service period / Період надання послуг:",
            $"{new DateTime(invoice.ServiceMonth.Year, invoice.ServiceMonth.Month, 1):MMMM yyyy}");

        AddInfoRow(table,
            "Service / Послуга:",
            $"{invoice.ServiceDescription}\n{invoice.ServiceDescriptionUa}");

        AddInfoRow(table,
            "Supplier / Постачальник:",
            $"{invoice.Supplier.Name} / {invoice.Supplier.NameUa}\n" +
            $"NIP/TIN: {invoice.Supplier.Tin}, REGON: {invoice.Supplier.Regon}\n" +
            $"VAT EU: {invoice.Supplier.Vat}\n" +
            $"{invoice.Supplier.Address}\n{invoice.Supplier.AddressUa}");

        AddInfoRow(table,
            "Customer / Замовник:",
            $"{invoice.Client.Name} / {invoice.Client.NameUa}\n" +
            $"VAT: {invoice.Client.Vat}\n" +
            $"{invoice.Client.Address}\n{invoice.Client.AddressUa}");

        AddInfoRow(table,
            "Bank account / Банківський рахунок:",
            $"IBAN: {invoice.Supplier.Iban}\n" +
            $"Bank: {invoice.Supplier.Bank}\n" +
            $"SWIFT: {invoice.Supplier.Swift}");

        AddInfoRow(table,
            "Payment method / Спосіб оплати:",
            "Bank transfer / Банківський переказ");

        AddInfoRow(table,
            "Payment due / Термін оплати:",
            $"{invoice.InvoiceDate.AddDays(14):dd.MM.yyyy} (14 days / днів)");

        body.AppendChild(table);
    }

    private static void AddServicesTable(Body body, Invoice invoice)
    {
        var table = CreateTable(6);

        // Header row
        var headerRow = new TableRow();
        AddCell(headerRow, "No.\nНомер", true, "600");
        AddCell(headerRow, "Description / Опис", true, "4266");
        AddCell(headerRow, "Net / Нетто", true, "1600");
        AddCell(headerRow, "VAT % / ПДВ %", true, "1000");
        AddCell(headerRow, "VAT / ПДВ", true, "1400");
        AddCell(headerRow, "Gross / Брутто", true, "1600");
        table.AppendChild(headerRow);

        // Service row
        var serviceRow = new TableRow();
        AddCell(serviceRow, "1", false, "600");
        AddCell(serviceRow, $"{invoice.ServiceDescription}\n{invoice.ServiceDescriptionUa}", false, "4266");
        AddCell(serviceRow, FormatAmount(invoice.NetAmount, invoice.Currency), false, "1600");
        AddCell(serviceRow, invoice.VatRate > 0 ? $"{invoice.VatRate}%" : "N/A", false, "1000");
        AddCell(serviceRow, invoice.VatRate > 0 ? FormatAmount(invoice.VatAmount, invoice.Currency) : "N/A", false, "1400");
        AddCell(serviceRow, FormatAmount(invoice.GrossAmount, invoice.Currency), false, "1600");
        table.AppendChild(serviceRow);

        // Total row
        var totalRow = new TableRow();
        AddCell(totalRow, "", true, "600");
        AddCell(totalRow, "Total / Всього", true, "4266");
        AddCell(totalRow, FormatAmount(invoice.NetAmount, invoice.Currency), true, "1600");
        AddCell(totalRow, "", true, "1000");
        AddCell(totalRow, invoice.VatRate > 0 ? FormatAmount(invoice.VatAmount, invoice.Currency) : "N/A", true, "1400");
        AddCell(totalRow, FormatAmount(invoice.GrossAmount, invoice.Currency), true, "1600");
        table.AppendChild(totalRow);

        body.AppendChild(table);
    }

    private static void AddFooter(Body body, Invoice invoice)
    {
        AddBilingualParagraph(body,
            $"Total amount due: {FormatAmount(invoice.GrossAmount, invoice.Currency)}",
            $"Загальна сума до оплати: {FormatAmount(invoice.GrossAmount, invoice.Currency)}");

        body.AppendChild(new Paragraph());

        var signaturePara = new Paragraph();
        signaturePara.AppendChild(new ParagraphProperties(
            new SpacingBetweenLines { Before = "600" }
        ));
        var signatureRun = new Run();
        signatureRun.AppendChild(new RunProperties(new FontSize { Val = "20" }));
        signatureRun.AppendChild(new Text("___________________________"));
        signaturePara.AppendChild(signatureRun);
        body.AppendChild(signaturePara);

        AddBilingualParagraph(body,
            $"Signature / Підпис: {invoice.Supplier.Name}",
            null);
    }

    private static void AddBilingualParagraph(Body body, string textEn, string? textUa)
    {
        var para = new Paragraph();
        para.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
        var run = new Run();
        run.AppendChild(new RunProperties(new Bold(), new FontSize { Val = "20" }));
        run.AppendChild(new Text(textEn));
        para.AppendChild(run);
        body.AppendChild(para);

        if (textUa != null)
        {
            var paraUa = new Paragraph();
            paraUa.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
            var runUa = new Run();
            runUa.AppendChild(new RunProperties(
                new Italic(),
                new FontSize { Val = "18" },
                new Color { Val = "666666" }
            ));
            runUa.AppendChild(new Text(textUa));
            paraUa.AppendChild(runUa);
            body.AppendChild(paraUa);
        }
    }

    private static Table CreateTable(int columns)
    {
        var table = new Table();
        var tblProps = new TableProperties(
            new TableBorders(
                new TopBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new RightBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" },
                new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "000000" }
            ),
            new TableWidth { Width = "10466", Type = TableWidthUnitValues.Dxa }
        );
        table.AppendChild(tblProps);
        return table;
    }

    private static void AddInfoRow(Table table, string label, string value)
    {
        var row = new TableRow();

        // Label cell
        var labelCell = new TableCell();
        labelCell.AppendChild(new TableCellProperties(
            new TableCellWidth { Width = "3500", Type = TableWidthUnitValues.Dxa },
            new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" }
        ));

        var labelPara = new Paragraph();
        labelPara.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
        var labelRun = new Run();
        labelRun.AppendChild(new RunProperties(new Bold(), new FontSize { Val = "18" }));
        labelRun.AppendChild(new Text(label));
        labelPara.AppendChild(labelRun);
        labelCell.AppendChild(labelPara);
        row.AppendChild(labelCell);

        // Value cell
        var valueCell = new TableCell();
        valueCell.AppendChild(new TableCellProperties(
            new TableCellWidth { Width = "6966", Type = TableWidthUnitValues.Dxa }
        ));

        var lines = value.Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var valPara = new Paragraph();
            valPara.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
            var valRun = new Run();
            valRun.AppendChild(new RunProperties(new FontSize { Val = "18" }));
            valRun.AppendChild(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            valPara.AppendChild(valRun);
            valueCell.AppendChild(valPara);
        }

        row.AppendChild(valueCell);
        table.AppendChild(row);
    }

    private static void AddCell(TableRow row, string text, bool bold, string width)
    {
        var cell = new TableCell();
        var cellProps = new TableCellProperties(
            new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa }
        );
        if (bold)
        {
            cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Fill = "F2F2F2" });
        }
        cell.AppendChild(cellProps);

        var lines = text.Split('\n');
        foreach (var line in lines)
        {
            var para = new Paragraph();
            para.AppendChild(new ParagraphProperties(new SpacingBetweenLines { After = "0" }));
            var run = new Run();
            var runProps = new RunProperties(new FontSize { Val = "18" });
            if (bold) runProps.AppendChild(new Bold());
            run.AppendChild(runProps);
            run.AppendChild(new Text(line) { Space = SpaceProcessingModeValues.Preserve });
            para.AppendChild(run);
            cell.AppendChild(para);
        }

        row.AppendChild(cell);
    }

    private static string FormatAmount(decimal amount, string currency)
    {
        return $"{amount.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)} {currency}";
    }
}
