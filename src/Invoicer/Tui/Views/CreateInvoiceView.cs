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
    private readonly RadioGroup _periodModeRadio;
    private readonly ServicePeriodDateField _periodStartField;
    private readonly ServicePeriodDateField _periodEndField;
    private readonly Button _periodStartPickButton;
    private readonly Button _periodEndPickButton;
    private bool _updatingPeriod;
    private bool _settingRateDate;
    private bool _rateDateManual;
    private (string Currency, DateTime Date)? _rateKey;
    private readonly Func<string, DateTime, CancellationToken, Task<NbpRateResult>> _getRate;
    private readonly Action<Action> _dispatch;
    private bool _disposed;
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
        : this(config, (currency, date, token) => NbpRateProvider.GetRateAsync(currency, date, token), Application.Invoke)
    {
    }

    internal CreateInvoiceView(AppConfig config,
        Func<string, DateTime, CancellationToken, Task<NbpRateResult>> getRate, Action<Action> dispatch)
    {
        _config = config;
        _getRate = getRate;
        _dispatch = dispatch;

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

        var periodTitle = new Label { Text = "Service Period:", X = 1, Y = row };
        _periodModeRadio = new RadioGroup
        {
            X = 18, Y = row, RadioLabels = ["Automatic", "Custom"], SelectedItem = 0,
        };
        row += 2;
        var periodStartTitle = new Label { Text = "From:", X = 1, Y = row };
        _periodStartField = new ServicePeriodDateField(DateTime.Today) { X = 18, Y = row };
        _periodStartPickButton = new Button { Text = "Pick", X = 31, Y = row, NoPadding = true, ShadowStyle = ShadowStyle.None };
        row++;
        var periodEndTitle = new Label { Text = "To:", X = 1, Y = row };
        _periodEndField = new ServicePeriodDateField(DateTime.Today) { X = 18, Y = row };
        _periodEndPickButton = new Button { Text = "Pick", X = 31, Y = row, NoPadding = true, ShadowStyle = ShadowStyle.None };
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
            periodTitle, _periodModeRadio, periodStartTitle, _periodStartField, _periodStartPickButton,
            periodEndTitle, _periodEndField, _periodEndPickButton,
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
        _dateField.TextChanged += (_, _) => RefreshDates();
        _periodModeRadio.SelectedItemChanged += (_, _) => RefreshDates();
        _periodStartField.TextChanged += (_, _) => { if (!_updatingPeriod) RefreshDates(); };
        _periodEndField.TextChanged += (_, _) => { if (!_updatingPeriod) RefreshDates(); };
        _periodStartField.InputValidityChanged += () => { if (!_updatingPeriod) RefreshDates(); };
        _periodEndField.InputValidityChanged += () => { if (!_updatingPeriod) RefreshDates(); };
        _periodStartField.HasFocusChanged += (_, e) => { if (!e.NewValue) RefreshDates(); };
        _periodEndField.HasFocusChanged += (_, e) => { if (!e.NewValue) RefreshDates(); };
        _periodStartPickButton.Accepting += (_, e) => { e.Cancel = true; PickPeriodDate(true); };
        _periodEndPickButton.Accepting += (_, e) => { e.Cancel = true; PickPeriodDate(false); };
        _rateDateField.TextChanged += (_, _) =>
        {
            if (_settingRateDate) return;
            _rateDateManual = true;
            if (TryGetDates(out var date, out var period, out _) && TryParseField(_rateDateField, out var entered))
                _rateDateManual = entered != ExchangeRateRules.RelevantDateForPeriodEnd(date, period!.End);
            RefreshDates();
        };
        _rateField.HasFocusChanged += (_, e) => { if (!e.NewValue) UpdatePreview(); };

        ApplyRateVisibility();
        RefreshDates();
    }

    private static bool TryParseField(TextField field, out DateTime date) =>
        DateTime.TryParseExact(field.Text?.ToString().Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out date) && date.Date != default;

    internal bool TryGetDates(out DateTime date, out ServicePeriod? period, out string? error)
    {
        period = null;
        error = null;
        if (!TryParseField(_dateField, out date))
        { error = "Please enter a valid invoice date (dd.MM.yyyy)."; return false; }
        if (SelectedClient is null) { error = "Select a client."; return false; }
        try
        {
            if (_periodModeRadio.SelectedItem == 1)
            {
                if (_periodStartField.InputError is { } fromError)
                { error = "Service period From: " + fromError; return false; }
                if (_periodEndField.InputError is { } toError)
                { error = "Service period To: " + toError; return false; }
                if (!TryParseField(_periodStartField, out var start) || !TryParseField(_periodEndField, out var end))
                { error = "Service period requires valid From and To dates (dd.MM.yyyy)."; return false; }
                period = ServicePeriod.Resolve(default, start, end);
            }
            else period = ServicePeriod.Resolve(Invoice.CalculateServiceMonth(date, SelectedClient.MonthOffsetRule));
            return true;
        }
        catch (InvoiceValidationException ex) { error = ex.Message; return false; }
    }

    private void RefreshDates()
    {
        if (_updatingPeriod || _disposed) return;
        var custom = _periodModeRadio.SelectedItem == 1;
        _periodStartField.Enabled = _periodEndField.Enabled = custom;
        _periodStartPickButton.Enabled = _periodEndPickButton.Enabled = custom;
        if (!TryGetDates(out var date, out var period, out _))
        {
            ClearRate();
            UpdatePreview();
            return;
        }
        if (!custom)
        {
            _updatingPeriod = true;
            try { _periodStartField.Date = period!.Start; _periodEndField.Date = period.End; }
            finally { _updatingPeriod = false; }
        }
        if (NeedsRate)
        {
            if (!_rateDateManual)
            {
                _settingRateDate = true;
                try { _rateDateField.Text = ExchangeRateRules.RelevantDateForPeriodEnd(date, period!.End)
                    .ToString("dd.MM.yyyy", CultureInfo.InvariantCulture); }
                finally { _settingRateDate = false; }
            }
            if (TryParseField(_rateDateField, out var rateDate))
            {
                var key = (ExchangeRateRules.Normalize(SelectedClient!.Currency), rateDate);
                if (_rateKey != key) StartRateLookup();
            }
            else { ClearRate(); _rateStatusLabel.Text = "Please enter a valid rate date (dd.MM.yyyy)."; }
        }
        UpdatePreview();
    }

    private void PickPeriodDate(bool start)
    {
        var field = start ? _periodStartField : _periodEndField;
        if (_periodModeRadio.SelectedItem != 1) return;
        using var culture = new DateCultureScope();
        var currentDate = TryParseField(field, out var displayedDate) ? displayedDate : field.Date;
        using var dialog = new ServiceDateDialog(start ? "Service period From" : "Service period To", currentDate);
        Application.Run(dialog);
        ApplyPickedDate(start, dialog.SelectedDate);
    }

    internal void ApplyPickedDate(bool start, DateTime? date)
    {
        if (_periodModeRadio.SelectedItem != 1 || date is null) return;
        (start ? _periodStartField : _periodEndField).Date = date.Value;
        RefreshDates();
    }

    private void ResetDateSelection()
    {
        _rateDateManual = false;
        _updatingPeriod = true;
        try { _periodModeRadio.SelectedItem = 0; }
        finally { _updatingPeriod = false; }
        ClearRate();
        RefreshDates();
    }

    private void OnClientChanged()
    {
        if (SelectedClientIndex >= 0 && SelectedClientIndex < _enabledClients.Count)
        {
            var client = _enabledClients[SelectedClientIndex];
            _invoiceNumberField.Text = (client.LastInvoiceNumber + 1).ToString();
            _amountField.Text = client.DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
        }

        // A rate belongs to the currency it was resolved for, so it never survives a change of
        // client. Clearing first also makes the lookup below unconditional: two clients can share
        // a rate date while needing entirely different currencies.
        ClearRate();
        ApplyRateVisibility();
        ResetDateSelection();
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
        _rateLookup = null;
        _rateKey = null;
        _rateField.Text = "";
        _rateStatusLabel.Text = "";
        _fetchedRate = null;
        _fetchedRateDate = null;
        _fetchedRateTable = null;
    }

    /// <summary>
    /// Looks the rate up off the UI thread. Terminal.Gui views may only be touched from the UI
    /// thread, so the result is applied through Application.Invoke.
    /// </summary>
    private void StartRateLookup()
    {
        if (!NeedsRate || _disposed)
            return;
        if (!TryGetDates(out _, out _, out _) || !TryParseField(_rateDateField, out var relevantDate))
        { ClearRate(); UpdatePreview(); return; }
        ClearRate();
        var lookup = new CancellationTokenSource();
        _rateLookup = lookup;

        var currency = SelectedClient!.Currency;
        _rateKey = (ExchangeRateRules.Normalize(currency), relevantDate);

        _rateStatusLabel.Text = $"Looking up NBP rate for {ExchangeRateRules.Normalize(currency)}...";

        _ = Task.Run(async () =>
        {
            NbpRateResult result;
            try { result = await _getRate(currency, relevantDate, lookup.Token); }
            catch (OperationCanceledException) { return; }
            if (lookup.IsCancellationRequested || _disposed) return;
            _dispatch(() =>
            {
                // A newer lookup, or a switch to a PLN client, supersedes this one.
                if (_disposed || lookup.IsCancellationRequested || !ReferenceEquals(_rateLookup, lookup))
                    return;
                // A value entered while this request was pending belongs to the user.
                if (!string.IsNullOrWhiteSpace(_rateField.Text))
                {
                    _rateStatusLabel.Text = "Rate entered by hand.";
                    UpdatePreview();
                    return;
                }

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
        if (!TryGetDates(out var date, out var period, out var dateError))
        { _previewLabel.Text = dateError; return; }
        if (!int.TryParse(_invoiceNumberField.Text?.ToString(), out var invNum)) invNum = 1;
        if (!decimal.TryParse(_amountField.Text?.ToString(), CultureInfo.InvariantCulture, out var amount))
            amount = client.DefaultAmount;

        InvoiceTaxTreatment treatment;
        try { treatment = InvoiceTaxTreatment.ForClient(client); }
        catch (InvoiceValidationException ex)
        {
            _previewLabel.Text = ex.Message;
            return;
        }

        var formattedNum = $"{date:yyyy}/{client.InvoicePrefix}/{invNum:D4}";
        var amounts = treatment.Calculate(amount);
        var vatAmount = amounts.Vat;
        var gross = amounts.Gross;

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
            $"Service: {InvoiceText.ServicePeriodDates(period!)}\n" +
            $"\n" +
            $"Net:     {amount.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency}\n" +
            $"Tax:     {treatment.Label}\n" +
            $"VAT:     {InvoiceText.FormatAmount(vatAmount, client.Currency)} ({InvoiceText.TaxLabel(treatment)})\n" +
            $"Gross:   {gross.ToString("N2", CultureInfo.InvariantCulture)} {client.Currency}\n" +
            (treatment.ReverseCharge ? InvoiceText.ReverseChargeAnnotation + "\n" : "") +
            rateBlock +
            $"\n" +
            $"Output:  {outputDir}/\n" +
            $"File:    {filename}";
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

        var client = _enabledClients[SelectedClientIndex];
        RefreshDates();
        if (!TryGetDates(out var date, out var period, out var dateError))
        { MessageBox.ErrorQuery("Invalid Dates", dateError!, "OK"); return; }
        if (NeedsRate && !TryParseField(_rateDateField, out _))
        { MessageBox.ErrorQuery("Invalid Dates", "Please enter a valid rate date (dd.MM.yyyy).", "OK"); return; }
        var (exchangeRate, exchangeRateDate, exchangeRateTable) = ResolveRateForInvoice();

        try
        {
            var (invoice, generatedFiles) = InvoiceGeneration.CreateAndGenerate(
                _config, client, invoiceNumber, date, amount, generateDocx, generatePdf, generateXml,
                exchangeRate, exchangeRateDate, exchangeRateTable,
                servicePeriodStart: period!.Start, servicePeriodEnd: period.End);

            InvoiceResultDialog.Show(invoice.FormattedNumber, generatedFiles.ToList());

            // Update fields for next invoice
            _invoiceNumberField.Text = (invoiceNumber + 1).ToString();
            ApplyRateVisibility();
            ResetDateSelection();
        }
        catch (KsefValidationException ex)
        {
            var details = string.Join("\n", ex.Errors.Select(error => $"- {error}"));
            MessageBox.ErrorQuery("KSeF Validation Failed", details, "OK");
        }
        catch (InvoiceValidationException ex)
        {
            MessageBox.ErrorQuery("Invoice Validation Failed", ex.Message, "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Generation Failed", $"Error: {ex.Message}", "OK");
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { _disposed = true; _rateLookup?.Cancel(); }
        base.Dispose(disposing);
    }
}
