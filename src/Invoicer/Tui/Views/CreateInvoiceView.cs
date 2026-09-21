using System.Globalization;
using System.Text;
using Invoicer.Config;
using Invoicer.Exchange;
using Invoicer.Generation;
using Invoicer.Models;
using Invoicer.Tui.Dialogs;
using Terminal.Gui;

namespace Invoicer.Tui.Views;

public class CreateInvoiceView : View
{
    private readonly AppConfig _config;
    private readonly List<ClientConfig> _enabledClients;
    private readonly RadioGroup _clientRadio;
    private readonly TextField _invoiceNumberField;
    private readonly TextField _dateField;
    private readonly TextField _amountField;
    private readonly Label _serviceMonthLabel;
    private readonly Label _rateDateLabel;
    private readonly TextField _rateDateField;
    private readonly Label _rateLabel;
    private readonly TextField _rateField;
    private readonly Button _refreshRateButton;
    private readonly Label _rateStatusLabel;
    private readonly View[] _belowRateRows;
    private readonly int[] _belowRateRowY;
    private readonly CheckBox _docxCheckBox;
    private readonly CheckBox _pdfCheckBox;
    private readonly CheckBox _xmlCheckBox;
    private readonly Label _previewLabel;

    /// <summary>Rows the exchange rate block occupies, reclaimed when it is hidden.</summary>
    private const int RateBlockHeight = 4;

    // The last rate resolved from NBP, kept so a rate the user has since typed over is not
    // attributed to a table it did not come from.
    private decimal? _fetchedRate;
    private DateTime? _fetchedRateDate;
    private string? _fetchedRateTable;
    private CancellationTokenSource? _rateLookup;

    private int SelectedClientIndex => _clientRadio.SelectedItem;

    private ClientConfig? SelectedClient =>
        SelectedClientIndex >= 0 && SelectedClientIndex < _enabledClients.Count
            ? _enabledClients[SelectedClientIndex]
            : null;

    private bool NeedsRate => ExchangeRateRules.RequiresRate(SelectedClient?.Currency);

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

        // Client selection (enabled clients only)
        _enabledClients = _config.Clients.Where(c => c.Enabled).ToList();
        var clientLabel = new Label { Text = "Client:", X = 1, Y = 1 };
        var clientLabels = _enabledClients.Select(c => c.Key).ToArray();
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
        if (_enabledClients.Count > 0)
            _amountField.Text = _enabledClients[0].DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
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

        // Exchange rate block. Shown only for a foreign-currency client; when hidden, the rows
        // below move up so a PLN invoice sees no gap where it never applies.
        _rateDateLabel = new Label { Text = "Rate Date:", X = 1, Y = row };
        _rateDateField = new TextField
        {
            X = 18,
            Y = row,
            Width = 15,
            ReadOnly = false,
        };
        row += 1;

        _rateLabel = new Label { Text = "Exchange Rate:", X = 1, Y = row };
        _rateField = new TextField
        {
            X = 18,
            Y = row,
            Width = 15,
            ReadOnly = false,
        };
        _refreshRateButton = new Button { Text = "Refresh", X = 35, Y = row };
        _refreshRateButton.Accepting += (_, e) =>
        {
            e.Cancel = true;
            StartRateLookup();
        };
        row += 1;

        _rateStatusLabel = new Label
        {
            X = 18,
            Y = row,
            Width = Dim.Fill(2),
            Text = "",
            HotKeySpecifier = (Rune)0xFFFF,
        };
        row += 2;

        var formatRowY = row;

        // Output format checkboxes
        var formatLabel = new Label { Text = "Output Format:", X = 1, Y = row };
        _docxCheckBox = new CheckBox
        {
            Text = "DOCX",
            X = 18,
            Y = row,
            CheckedState = _config.Output.GenerateDocxByDefault ? CheckState.Checked : CheckState.UnChecked,
        };
        _pdfCheckBox = new CheckBox
        {
            Text = "PDF",
            X = 30,
            Y = row,
            CheckedState = _config.Output.GeneratePdfByDefault ? CheckState.Checked : CheckState.UnChecked,
        };
        _xmlCheckBox = new CheckBox
        {
            Text = "KSeF XML",
            X = 40,
            Y = row,
            CheckedState = _config.Output.GenerateXmlByDefault ? CheckState.Checked : CheckState.UnChecked,
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

        _belowRateRows = [formatLabel, _docxCheckBox, _pdfCheckBox, _xmlCheckBox, generateButton];
        _belowRateRowY =
        [
            formatRowY, formatRowY, formatRowY, formatRowY, formatRowY + 3,
        ];

        formFrame.Add(
            clientLabel, _clientRadio,
            numberLabel, _invoiceNumberField,
            dateLabel, _dateField,
            amountLabel, _amountField,
            serviceMonthTitle, _serviceMonthLabel,
            _rateDateLabel, _rateDateField,
            _rateLabel, _rateField, _refreshRateButton,
            _rateStatusLabel,
            formatLabel, _docxCheckBox, _pdfCheckBox, _xmlCheckBox,
            generateButton
        );

        // Preview: sized to its text so the frame can scroll when the preview is taller than the window.
        _previewLabel = new Label
        {
            X = 1,
            Y = 1,
            Width = Dim.Fill(1),
            Height = Dim.Auto(DimAutoStyle.Text),
            Text = "",
            HotKeySpecifier = (Rune)0xFFFF,
        };
        previewFrame.Add(_previewLabel);

        Scrolling.EnableVertical(formFrame);
        Scrolling.EnableVertical(previewFrame);

        Add(formFrame, previewFrame);

        // Wire up change events
        _clientRadio.SelectedItemChanged += (_, _) => OnClientChanged();
        _invoiceNumberField.HasFocusChanged += (_, e) => { if (!e.NewValue) UpdatePreview(); };
        _amountField.HasFocusChanged += (_, e) => { if (!e.NewValue) UpdatePreview(); };
        _dateField.HasFocusChanged += (_, e) =>
        {
            if (e.NewValue)
                return;

            _serviceMonthLabel.Text = CalculateServiceMonthText();
            // The rate date derives from the invoice date, so it follows it until edited.
            ResetRateDateToDefault();
            UpdatePreview();
        };
        _rateDateField.HasFocusChanged += (_, e) => { if (!e.NewValue) StartRateLookup(); };
        _rateField.HasFocusChanged += (_, e) => { if (!e.NewValue) UpdatePreview(); };

        ApplyRateVisibility();
        ResetRateDateToDefault();
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
        if (SelectedClientIndex >= 0 && SelectedClientIndex < _enabledClients.Count)
        {
            var client = _enabledClients[SelectedClientIndex];
            _invoiceNumberField.Text = (client.LastInvoiceNumber + 1).ToString();
            _amountField.Text = client.DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
        }

        _serviceMonthLabel.Text = CalculateServiceMonthText();
        // A rate belongs to the currency it was resolved for, so it never survives a change of
        // client. Clearing first also makes the lookup below unconditional: two clients can share
        // a rate date while needing entirely different currencies.
        ClearRate();
        ApplyRateVisibility();
        ResetRateDateToDefault();
        UpdatePreview();
    }

    /// <summary>
    /// Shows the exchange rate rows only for a currency that needs one, and moves the rows
    /// below up into the reclaimed space when it does not.
    /// </summary>
    private void ApplyRateVisibility()
    {
        var visible = NeedsRate;

        _rateDateLabel.Visible = visible;
        _rateDateField.Visible = visible;
        _rateLabel.Visible = visible;
        _rateField.Visible = visible;
        _refreshRateButton.Visible = visible;
        _rateStatusLabel.Visible = visible;

        var offset = visible ? 0 : -RateBlockHeight;
        for (var i = 0; i < _belowRateRows.Length; i++)
            _belowRateRows[i].Y = _belowRateRowY[i] + offset;

        if (!visible)
            ClearRate();
    }

    private void ClearRate()
    {
        _rateLookup?.Cancel();
        _rateField.Text = "";
        _rateStatusLabel.Text = "";
        _fetchedRate = null;
        _fetchedRateDate = null;
        _fetchedRateTable = null;
    }

    /// <summary>
    /// Puts the derived relevant date back in the field and looks the rate up again. The derived
    /// date is a starting point, not a verdict: the user may overwrite it.
    /// </summary>
    private void ResetRateDateToDefault()
    {
        if (!NeedsRate)
            return;

        var invoiceDate = ParseDate();
        var serviceMonth = Invoice.CalculateServiceMonth(invoiceDate, SelectedClient!.MonthOffsetRule);
        var relevantDate = ExchangeRateRules.RelevantDate(invoiceDate, serviceMonth);
        var text = relevantDate.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

        var dateChanged = _rateDateField.Text?.ToString() != text;
        if (dateChanged)
            _rateDateField.Text = text;

        // Look the rate up again when the date moved, and also when there is simply no rate to
        // show: switching from a PLN client back to a foreign-currency one clears the rate while
        // leaving the date alone, and an early return there would strand the field empty.
        if (dateChanged || _fetchedRate is null)
            StartRateLookup();
    }

    private DateTime ParseRateDate()
    {
        var text = _rateDateField.Text?.ToString() ?? "";
        return DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date)
            ? date
            : ParseDate();
    }

    /// <summary>
    /// Looks the rate up off the UI thread. Terminal.Gui views may only be touched from the UI
    /// thread, so the result is applied through Application.Invoke.
    /// </summary>
    private void StartRateLookup()
    {
        if (!NeedsRate)
            return;

        _rateLookup?.Cancel();
        var lookup = new CancellationTokenSource();
        _rateLookup = lookup;

        var currency = SelectedClient!.Currency;
        var relevantDate = ParseRateDate();

        _rateStatusLabel.Text = $"Looking up NBP rate for {ExchangeRateRules.Normalize(currency)}...";

        _ = Task.Run(async () =>
        {
            var result = await NbpRateProvider.GetRateAsync(currency, relevantDate, lookup.Token);

            Application.Invoke(() =>
            {
                // A newer lookup, or a switch to a PLN client, supersedes this one.
                if (lookup.IsCancellationRequested || !ReferenceEquals(_rateLookup, lookup))
                    return;

                ApplyRateResult(result);
            });
        }, lookup.Token);
    }

    private void ApplyRateResult(NbpRateResult result)
    {
        if (result.Rate is { } rate)
        {
            _fetchedRate = rate.Rate;
            _fetchedRateDate = rate.EffectiveDate;
            _fetchedRateTable = rate.TableNumber;
            _rateField.Text = rate.Rate.ToString("0.######", CultureInfo.InvariantCulture);
            _rateStatusLabel.Text =
                $"NBP {rate.TableNumber} of {rate.EffectiveDate:dd.MM.yyyy}";
        }
        else
        {
            // Nothing is guessed in: the field stays empty and the user can type the rate.
            _fetchedRate = null;
            _fetchedRateDate = null;
            _fetchedRateTable = null;
            _rateField.Text = "";
            _rateStatusLabel.Text = result.Error ?? "The rate could not be retrieved.";
        }

        UpdatePreview();
    }

    /// <summary>
    /// The rate to put on the invoice, together with the NBP table it came from. A rate the user
    /// typed over the fetched one carries no table: it did not come from one.
    /// </summary>
    private (decimal? Rate, DateTime? Date, string? Table) ResolveRateForInvoice()
    {
        if (!NeedsRate)
            return (null, null, null);

        var text = (_rateField.Text?.ToString() ?? "").Trim();
        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            return (null, null, null);

        return rate == _fetchedRate
            ? (rate, _fetchedRateDate, _fetchedRateTable)
            : (rate, null, null);
    }

    private void UpdatePreview()
    {
        if (SelectedClientIndex < 0 || SelectedClientIndex >= _enabledClients.Count)
        {
            _previewLabel.Text = _enabledClients.Count == 0
                ? "No enabled clients available."
                : "Select a client to see preview.";
            return;
        }

        var client = _enabledClients[SelectedClientIndex];
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

        var account = _config.FindBillingAccount(client);
        var accountText = account is null ? "(none assigned)" : $"{account.Label} {account.Iban}";

        var (rate, rateDate, rateTable) = ResolveRateForInvoice();
        var rateBlock = "";
        if (NeedsRate)
        {
            if (rate is { } value)
            {
                var source = rateTable is null
                    ? "entered by hand"
                    : $"NBP A {rateTable}, effective {rateDate:dd.MM.yyyy}";

                rateBlock =
                    "\n" +
                    $"Rate:    {value.ToString("0.######", CultureInfo.InvariantCulture)} PLN/{ExchangeRateRules.Normalize(client.Currency)}\n" +
                    $"         {source}\n" +
                    $"Net PLN: {Math.Round(amount * value, 2).ToString("N2", CultureInfo.InvariantCulture)}\n";
            }
            else
            {
                rateBlock = "\nRate:    (not set - KSeF XML needs one)\n";
            }
        }

        _previewLabel.Text =
            $"Invoice: {formattedNum}\n" +
            $"Client:  {client.Name}\n" +
            $"Account: {accountText}\n" +
            $"Date:    {date:dd.MM.yyyy}\n" +
            $"Service: {serviceMonth:MMMM yyyy}\n" +
            $"\n" +
            $"Net:     {amount.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency}\n" +
            $"VAT:     {(client.VatRate > 0 ? $"{vatAmount.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency} ({client.VatRate}%)" : "N/A")}\n" +
            $"Gross:   {gross.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency}\n" +
            rateBlock +
            $"\n" +
            $"Output:  {outputDir}/\n" +
            $"File:    {filename}";
    }

    private string CalculateServiceMonthText()
    {
        if (SelectedClientIndex < 0 || SelectedClientIndex >= _enabledClients.Count)
            return "N/A";

        var client = _enabledClients[SelectedClientIndex];
        var date = ParseDate();
        var serviceMonth = Invoice.CalculateServiceMonth(date, client.MonthOffsetRule);
        return serviceMonth.ToString("MMMM yyyy");
    }

    private int GetNextInvoiceNumber()
    {
        if (_enabledClients.Count == 0) return 1;
        return _enabledClients[0].LastInvoiceNumber + 1;
    }

    private void OnGenerate()
    {
        if (SelectedClientIndex < 0 || SelectedClientIndex >= _enabledClients.Count)
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
        var generateXml = _xmlCheckBox.CheckedState == CheckState.Checked;

        if (!generateDocx && !generatePdf && !generateXml)
        {
            MessageBox.ErrorQuery("Error", "Please select at least one output format (DOCX, PDF, or KSeF XML).", "OK");
            return;
        }

        _config.Output.GenerateDocxByDefault = generateDocx;
        _config.Output.GeneratePdfByDefault = generatePdf;
        _config.Output.GenerateXmlByDefault = generateXml;

        var client = _enabledClients[SelectedClientIndex];

        // Resolved before any generator runs, so a bad account reference produces no files at all.
        BillingAccountConfig billingAccount;
        try
        {
            billingAccount = _config.ResolveBillingAccount(client);
        }
        catch (BillingAccountNotFoundException ex)
        {
            MessageBox.ErrorQuery("Billing Account Missing", ex.Message, "OK");
            return;
        }

        var (exchangeRate, exchangeRateDate, exchangeRateTable) = ResolveRateForInvoice();

        var invoice = Invoice.Create(
            client,
            _config.Supplier,
            billingAccount,
            _config.Output,
            invoiceNumber,
            ParseDate(),
            amount,
            generateDocx,
            generatePdf,
            generateXml,
            exchangeRate,
            exchangeRateDate,
            exchangeRateTable
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

            if (invoice.GenerateXml)
            {
                KsefXmlGenerator.Generate(invoice);
                generatedFiles.Add(invoice.XmlPath);
            }

            // Update last invoice number
            client.LastInvoiceNumber = invoiceNumber;
            ConfigManager.Save(_config);

            InvoiceResultDialog.Show(invoice.FormattedNumber, generatedFiles);

            // Update fields for next invoice
            _invoiceNumberField.Text = (invoiceNumber + 1).ToString();
            ApplyRateVisibility();
            UpdatePreview();
        }
        catch (KsefValidationException ex)
        {
            var details = string.Join("\n", ex.Errors.Select(error => $"- {error}"));
            MessageBox.ErrorQuery("KSeF Validation Failed", details, "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Generation Failed", $"Error: {ex.Message}", "OK");
        }
    }
}
