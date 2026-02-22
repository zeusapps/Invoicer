using System.Globalization;
using Terminal.Gui;
using Invoicer.Models;
using Invoicer.Config;
using Invoicer.Generation;
using Invoicer.Tui.Dialogs;

namespace Invoicer.Tui.Views;

public class CreateInvoiceView : View
{
    private readonly AppConfig _config;
    private readonly RadioGroup _clientRadio;
    private readonly TextField _invoiceNumberField;
    private readonly TextField _dateField;
    private readonly TextField _amountField;
    private readonly Label _serviceMonthLabel;
    private readonly CheckBox _docxCheckBox;
    private readonly CheckBox _pdfCheckBox;
    private readonly Label _previewLabel;

    private int SelectedClientIndex => _clientRadio.SelectedItem;

    public CreateInvoiceView(AppConfig config)
    {
        _config = config;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        var formFrame = new FrameView
        {
            Title = "Create Invoice",
            X = 0,
            Y = 0,
            Width = Dim.Percent(50),
            Height = Dim.Fill(),
            CanFocus = true,
        };

        var previewFrame = new FrameView
        {
            Title = "Preview",
            X = Pos.Percent(50),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };

        // Client selection
        var clientLabel = new Label { Text = "Client:", X = 1, Y = 1 };
        var clientLabels = _config.Clients.Select(c => c.Key).ToArray();
        _clientRadio = new RadioGroup
        {
            X = 18,
            Y = 1,
            Width = Dim.Fill(2),
            Height = clientLabels.Length,
            RadioLabels = clientLabels,
            SelectedItem = clientLabels.Length > 0 ? 0 : -1,
        };

        int row = 1 + Math.Max(clientLabels.Length, 1) + 1;

        // Invoice number
        var numberLabel = new Label { Text = "Invoice Number:", X = 1, Y = row };
        _invoiceNumberField = new TextField
        {
            X = 18,
            Y = row,
            Width = 15,
            ReadOnly = false,
            Text = GetNextInvoiceNumber().ToString(),
        };
        row += 2;

        // Invoice date
        var dateLabel = new Label { Text = "Invoice Date:", X = 1, Y = row };
        _dateField = new TextField
        {
            X = 18,
            Y = row,
            Width = 15,
            ReadOnly = false,
            Text = DateTime.Today.ToString("dd.MM.yyyy"),
        };
        row += 2;

        // Amount
        var amountLabel = new Label { Text = "Amount:", X = 1, Y = row };
        _amountField = new TextField
        {
            X = 18,
            Y = row,
            Width = 20,
            ReadOnly = false,
        };
        if (_config.Clients.Count > 0)
            _amountField.Text = _config.Clients[0].DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
        row += 2;

        // Service month (read-only)
        var serviceMonthTitle = new Label { Text = "Service Month:", X = 1, Y = row };
        _serviceMonthLabel = new Label
        {
            X = 18,
            Y = row,
            Width = Dim.Fill(2),
            Text = CalculateServiceMonthText(),
        };
        row += 2;

        // Output format checkboxes
        var formatLabel = new Label { Text = "Output Format:", X = 1, Y = row };
        _docxCheckBox = new CheckBox
        {
            Text = "DOCX",
            X = 18,
            Y = row,
            CheckedState = CheckState.Checked,
        };
        _pdfCheckBox = new CheckBox
        {
            Text = "PDF",
            X = 30,
            Y = row,
            CheckedState = CheckState.Checked,
        };
        row += 3;

        // Generate button
        var generateButton = new Button
        {
            Text = "Generate",
            X = 18,
            Y = row,
        };
        generateButton.Accepting += (_, e) =>
        {
            e.Cancel = true;
            OnGenerate();
        };

        formFrame.Add(
            clientLabel, _clientRadio,
            numberLabel, _invoiceNumberField,
            dateLabel, _dateField,
            amountLabel, _amountField,
            serviceMonthTitle, _serviceMonthLabel,
            formatLabel, _docxCheckBox, _pdfCheckBox,
            generateButton
        );

        // Preview
        _previewLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Fill(1),
            Text = "",
        };
        previewFrame.Add(_previewLabel);

        Add(formFrame, previewFrame);

        // Wire up change events
        _clientRadio.SelectedItemChanged += (_, _) => OnClientChanged();
        _invoiceNumberField.HasFocusChanged += (_, e) => { if (!e.NewValue) UpdatePreview(); };
        _amountField.HasFocusChanged += (_, e) => { if (!e.NewValue) UpdatePreview(); };
        _dateField.HasFocusChanged += (_, e) => { if (!e.NewValue) { _serviceMonthLabel.Text = CalculateServiceMonthText(); UpdatePreview(); } };

        UpdatePreview();
    }

    private DateTime ParseDate()
    {
        var text = _dateField.Text?.ToString() ?? "";
        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            return date;
        return DateTime.Today;
    }

    private void OnClientChanged()
    {
        if (SelectedClientIndex >= 0 && SelectedClientIndex < _config.Clients.Count)
        {
            var client = _config.Clients[SelectedClientIndex];
            _invoiceNumberField.Text = (client.LastInvoiceNumber + 1).ToString();
            _amountField.Text = client.DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
        }

        _serviceMonthLabel.Text = CalculateServiceMonthText();
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (SelectedClientIndex < 0 || SelectedClientIndex >= _config.Clients.Count)
        {
            _previewLabel.Text = "Select a client to see preview.";
            return;
        }

        var client = _config.Clients[SelectedClientIndex];
        if (!int.TryParse(_invoiceNumberField.Text?.ToString(), out var invNum)) invNum = 1;
        if (!decimal.TryParse(_amountField.Text?.ToString(), CultureInfo.InvariantCulture, out var amount))
            amount = client.DefaultAmount;

        var date = ParseDate();
        var serviceMonth = Invoice.CalculateServiceMonth(date, client.MonthOffsetRule);
        var formattedNum = $"{date:yyyy}/{client.InvoicePrefix}/{invNum:D4}";
        var vatAmount = Math.Round(amount * client.VatRate / 100m, 2);
        var gross = amount + vatAmount;

        var outputDir = _config.Output.Pattern
            .Replace("{year}", date.Year.ToString());
        var filename = _config.Output.Filename
            .Replace("{date}", date.ToString("yyyyMMdd"))
            .Replace("{client}", client.Key);

        _previewLabel.Text =
            $"Invoice: {formattedNum}\n" +
            $"Client:  {client.Name}\n" +
            $"Date:    {date:dd.MM.yyyy}\n" +
            $"Service: {serviceMonth:MMMM yyyy}\n" +
            $"\n" +
            $"Net:     {amount.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency}\n" +
            $"VAT:     {(client.VatRate > 0 ? $"{vatAmount.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency} ({client.VatRate}%)" : "N/A")}\n" +
            $"Gross:   {gross.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency}\n" +
            $"\n" +
            $"Output:  {outputDir}/\n" +
            $"File:    {filename}";
    }

    private string CalculateServiceMonthText()
    {
        if (SelectedClientIndex < 0 || SelectedClientIndex >= _config.Clients.Count)
            return "N/A";

        var client = _config.Clients[SelectedClientIndex];
        var date = ParseDate();
        var serviceMonth = Invoice.CalculateServiceMonth(date, client.MonthOffsetRule);
        return serviceMonth.ToString("MMMM yyyy");
    }

    private int GetNextInvoiceNumber()
    {
        if (_config.Clients.Count == 0) return 1;
        return _config.Clients[0].LastInvoiceNumber + 1;
    }

    private void OnGenerate()
    {
        if (SelectedClientIndex < 0 || SelectedClientIndex >= _config.Clients.Count)
        {
            MessageBox.ErrorQuery("Error", "Please select a client.", "OK");
            return;
        }

        if (!int.TryParse(_invoiceNumberField.Text?.ToString(), out var invoiceNumber) || invoiceNumber <= 0)
        {
            MessageBox.ErrorQuery("Error", "Please enter a valid invoice number.", "OK");
            return;
        }

        if (!decimal.TryParse(_amountField.Text?.ToString(), CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            MessageBox.ErrorQuery("Error", "Please enter a valid amount.", "OK");
            return;
        }

        var generateDocx = _docxCheckBox.CheckedState == CheckState.Checked;
        var generatePdf = _pdfCheckBox.CheckedState == CheckState.Checked;

        if (!generateDocx && !generatePdf)
        {
            MessageBox.ErrorQuery("Error", "Please select at least one output format.", "OK");
            return;
        }

        var client = _config.Clients[SelectedClientIndex];
        var invoice = Invoice.Create(
            client,
            _config.Supplier,
            _config.Output,
            invoiceNumber,
            ParseDate(),
            amount,
            generateDocx,
            generatePdf
        );

        try
        {
            var generatedFiles = new List<string>();

            if (invoice.GenerateDocx)
            {
                DocxGenerator.Generate(invoice);
                generatedFiles.Add(invoice.DocxPath);
            }

            if (invoice.GeneratePdf)
            {
                PdfGenerator.Generate(invoice);
                generatedFiles.Add(invoice.PdfPath);
            }

            // Update last invoice number
            client.LastInvoiceNumber = invoiceNumber;
            ConfigManager.Save(_config);

            InvoiceResultDialog.Show(invoice.FormattedNumber, generatedFiles);

            // Update fields for next invoice
            _invoiceNumberField.Text = (invoiceNumber + 1).ToString();
            UpdatePreview();
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Generation Failed", $"Error: {ex.Message}", "OK");
        }
    }
}
