using System.Collections.ObjectModel;
using System.Globalization;
using Terminal.Gui;
using Invoicer.Models;
using Invoicer.Config;

namespace Invoicer.Tui.Views;

public class ClientListView : View
{
    private readonly AppConfig _config;
    private readonly Action _refresh;
    private readonly ListView _listView;
    private readonly TextField[] _fields;
    private int _previousClientIndex = -1;
    private readonly RadioGroup _monthRuleRadio;
    private readonly Button _toggleEnabledButton;
    private readonly Label _accountLabel;
    private readonly Label _accountWarningLabel;
    private string _selectedAccountKey = "";
    private readonly string[] _monthRuleValues = ["early_previous", "early_current"];
    private readonly string[] _fieldLabels =
    [
        "Key:", "Name:", "Name (UA):", "Address:", "Address (UA):", "Country:",
        "Currency:", "Default Amt:", "VAT:", "VAT Rate:",
        "Service Desc:", "Service (UA):", "Prefix:",
    ];

    public ClientListView(AppConfig config, Action refresh)
    {
        _config = config;
        _refresh = refresh;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        // Client list on the left
        var listFrame = new FrameView
        {
            Title = "Clients",
            X = 0,
            Y = 0,
            Width = Dim.Percent(30),
            Height = Dim.Fill(3),
            CanFocus = true,
        };

        _listView = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
        };
        RefreshListViewSource();
        _listView.SelectedItemChanged += (_, _) => OnSelectedClientChanged();
        listFrame.Add(_listView);

        // Detail editor on the right
        var detailFrame = new FrameView
        {
            Title = "Client Details",
            X = Pos.Percent(30),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            CanFocus = true,
        };

        // Pre-create all fields
        _fields = new TextField[_fieldLabels.Length];
        for (int i = 0; i < _fieldLabels.Length; i++)
        {
            int y = i * 2;
            detailFrame.Add(new Label { Text = _fieldLabels[i], X = 1, Y = y });
            _fields[i] = new TextField
            {
                X = 16,
                Y = y,
                Width = Dim.Fill(2),
                ReadOnly = false,
                Text = "",
            };
            detailFrame.Add(_fields[i]);
        }

        // Billing account: chosen from the defined accounts rather than typed, so it cannot
        // reference a key that does not exist.
        int accountY = _fieldLabels.Length * 2;
        detailFrame.Add(new Label { Text = "Account:", X = 1, Y = accountY });
        _accountLabel = new Label { X = 16, Y = accountY, Width = Dim.Fill(14), Text = "" };
        var chooseAccountButton = new Button { Text = "Choose…", X = Pos.AnchorEnd(12), Y = accountY };
        chooseAccountButton.Accepting += (_, e) => { e.Cancel = true; OnChooseAccount(); };
        // Below the button's shadow row, so a long warning is not drawn underneath it.
        _accountWarningLabel = new Label { X = 16, Y = accountY + 2, Width = Dim.Fill(2), Text = "" };
        detailFrame.Add(_accountLabel, chooseAccountButton, _accountWarningLabel);

        // Currency drives the mismatch warning, so re-check it once the field is edited.
        _fields[6].HasFocusChanged += (_, e) => { if (!e.NewValue) UpdateAccountDisplay(); };

        // Month Rule radio group
        int radioY = accountY + 3;
        detailFrame.Add(new Label { Text = "Month Rule:", X = 1, Y = radioY });
        _monthRuleRadio = new RadioGroup
        {
            X = 16,
            Y = radioY,
            RadioLabels = ["Early → Previous Month", "Early → Current Month"],
        };
        detailFrame.Add(_monthRuleRadio);

        Scrolling.EnableVertical(detailFrame);
        Scrolling.ShowScrollBarWhenNeeded(_listView);

        // Buttons at bottom
        var addButton = new Button { Text = "Add", X = 1, Y = Pos.AnchorEnd(2) };
        addButton.Accepting += (_, e) => { e.Cancel = true; OnAddClient(); };

        var deleteButton = new Button { Text = "Delete", X = 10, Y = Pos.AnchorEnd(2) };
        deleteButton.Accepting += (_, e) => { e.Cancel = true; OnDeleteClient(); };

        var saveButton = new Button { Text = "Save All", X = 22, Y = Pos.AnchorEnd(2) };
        saveButton.Accepting += (_, e) => { e.Cancel = true; OnSave(); };

        var moveUpButton = new Button { Text = "▲ Up", X = 36, Y = Pos.AnchorEnd(2) };
        moveUpButton.Accepting += (_, e) => { e.Cancel = true; OnMoveClient(-1); };

        var moveDownButton = new Button { Text = "▼ Down", X = 46, Y = Pos.AnchorEnd(2) };
        moveDownButton.Accepting += (_, e) => { e.Cancel = true; OnMoveClient(1); };

        _toggleEnabledButton = new Button { Text = "Disable", X = 58, Y = Pos.AnchorEnd(2) };
        _toggleEnabledButton.Accepting += (_, e) => { e.Cancel = true; OnToggleEnabled(); };

        Add(listFrame, detailFrame, addButton, deleteButton, saveButton, moveUpButton, moveDownButton, _toggleEnabledButton);

        if (_config.Clients.Count > 0)
        {
            _listView.SelectedItem = 0;
            LoadClientIntoFields();
            UpdateToggleButtonLabel();
        }
    }

    public void SelectClient(int index)
    {
        if (index >= 0 && index < _config.Clients.Count)
        {
            _listView.SelectedItem = index;
            LoadClientIntoFields();
        }
    }

    private void SaveFieldsToClient(int? overrideIndex = null)
    {
        var idx = overrideIndex ?? _listView.SelectedItem;
        if (idx < 0 || idx >= _config.Clients.Count) return;

        var client = _config.Clients[idx];
        client.Key = _fields[0].Text?.ToString() ?? "";
        client.Name = _fields[1].Text?.ToString() ?? "";
        client.NameUa = _fields[2].Text?.ToString() ?? "";
        client.Address = _fields[3].Text?.ToString() ?? "";
        client.AddressUa = _fields[4].Text?.ToString() ?? "";
        client.Country = Countries.Normalize(_fields[5].Text?.ToString());
        client.Currency = _fields[6].Text?.ToString() ?? "";
        if (decimal.TryParse(_fields[7].Text?.ToString(), CultureInfo.InvariantCulture, out var amt)) client.DefaultAmount = amt;
        client.Vat = _fields[8].Text?.ToString() ?? "";
        if (int.TryParse(_fields[9].Text?.ToString(), out var rate)) client.VatRate = rate;
        client.ServiceDescription = _fields[10].Text?.ToString() ?? "";
        client.ServiceDescriptionUa = _fields[11].Text?.ToString() ?? "";
        client.InvoicePrefix = _fields[12].Text?.ToString() ?? "";
        client.BillingAccount = _selectedAccountKey;
        client.MonthOffsetRule = _monthRuleValues[_monthRuleRadio.SelectedItem];
    }

    private void LoadClientIntoFields()
    {
        // Save previous client first
        if (_previousClientIndex >= 0)
            SaveFieldsToClient(_previousClientIndex);

        var idx = _listView.SelectedItem;
        _previousClientIndex = idx;
        if (idx < 0 || idx >= _config.Clients.Count) return;

        var client = _config.Clients[idx];
        _fields[0].Text = client.Key;
        _fields[1].Text = client.Name;
        _fields[2].Text = client.NameUa;
        _fields[3].Text = client.Address;
        _fields[4].Text = client.AddressUa;
        _fields[5].Text = client.Country;
        _fields[6].Text = client.Currency;
        _fields[7].Text = client.DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
        _fields[8].Text = client.Vat;
        _fields[9].Text = client.VatRate.ToString();
        _fields[10].Text = client.ServiceDescription;
        _fields[11].Text = client.ServiceDescriptionUa;
        _fields[12].Text = client.InvoicePrefix;
        _selectedAccountKey = client.BillingAccount;
        UpdateAccountDisplay();
        var ruleIndex = Array.IndexOf(_monthRuleValues, client.MonthOffsetRule);
        _monthRuleRadio.SelectedItem = ruleIndex >= 0 ? ruleIndex : 0;
    }

    private void UpdateAccountDisplay()
    {
        var account = _config.BillingAccounts.FirstOrDefault(a =>
            _selectedAccountKey.Length > 0 && a.Key == _selectedAccountKey);

        _accountLabel.Text = account is not null
            ? $"{account.Key} – {account.Label}"
            : _selectedAccountKey.Length > 0
                ? $"(unknown account '{_selectedAccountKey}')"
                : "(none assigned)";

        var editedCurrency = new ClientConfig { Currency = _fields[6].Text?.ToString() ?? "" };
        var warning = BillingAccountRules.CurrencyMismatchWarning(editedCurrency, account);
        _accountWarningLabel.Text = warning is null ? "" : $"⚠ {warning}";
    }

    private void OnChooseAccount()
    {
        if (_config.BillingAccounts.Count == 0)
        {
            MessageBox.ErrorQuery("No Billing Accounts",
                "Define an account in Settings > Billing Accounts first.", "OK");
            return;
        }

        var dialog = new Dialog
        {
            Title = "Choose Billing Account",
            Width = Dim.Percent(60),
            Height = Math.Min(_config.BillingAccounts.Count + 6, 20),
        };

        var items = _config.BillingAccounts
            .Select(a => string.IsNullOrWhiteSpace(a.Currency) ? $"{a.Key} – {a.Label}" : $"{a.Key} – {a.Label} ({a.Currency})")
            .ToList();
        var list = new ListView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };
        list.SetSource(new ObservableCollection<string>(items));
        var currentIndex = _config.BillingAccounts.FindIndex(a => a.Key == _selectedAccountKey);
        list.SelectedItem = currentIndex >= 0 ? currentIndex : 0;

        string? chosenKey = null;
        void Choose()
        {
            if (list.SelectedItem >= 0 && list.SelectedItem < _config.BillingAccounts.Count)
                chosenKey = _config.BillingAccounts[list.SelectedItem].Key;
            Application.RequestStop(dialog);
        }

        list.OpenSelectedItem += (_, _) => Choose();

        var okButton = new Button { Text = "OK", IsDefault = true };
        okButton.Accepting += (_, e) => { e.Cancel = true; Choose(); };
        var cancelButton = new Button { Text = "Cancel" };
        cancelButton.Accepting += (_, e) => { e.Cancel = true; Application.RequestStop(dialog); };

        dialog.Add(list);
        dialog.AddButton(okButton);
        dialog.AddButton(cancelButton);
        list.SetFocus();
        Application.Run(dialog);
        dialog.Dispose();

        if (chosenKey is null) return;

        _selectedAccountKey = chosenKey;
        UpdateAccountDisplay();
    }

    private void RefreshListViewSource()
    {
        _listView.SetSource(new ObservableCollection<string>(
            _config.Clients.Select(c => c.Enabled ? c.Key : ApplyStrikethrough(c.Key))));
    }

    private static string ApplyStrikethrough(string text)
    {
        return string.Concat(text.SelectMany(c => new[] { c, '\u0336' }));
    }

    private void OnSelectedClientChanged()
    {
        LoadClientIntoFields();
        UpdateToggleButtonLabel();
    }

    private void UpdateToggleButtonLabel()
    {
        var idx = _listView.SelectedItem;
        if (idx >= 0 && idx < _config.Clients.Count)
            _toggleEnabledButton.Text = _config.Clients[idx].Enabled ? "Disable" : "Enable";
    }

    private void OnMoveClient(int direction)
    {
        SaveFieldsToClient();
        var idx = _listView.SelectedItem;
        var newIdx = idx + direction;
        if (idx < 0 || idx >= _config.Clients.Count) return;
        if (newIdx < 0 || newIdx >= _config.Clients.Count) return;

        (_config.Clients[idx], _config.Clients[newIdx]) = (_config.Clients[newIdx], _config.Clients[idx]);
        _previousClientIndex = -1;
        RefreshListViewSource();
        _listView.SelectedItem = newIdx;
        LoadClientIntoFields();
    }

    private void OnToggleEnabled()
    {
        var idx = _listView.SelectedItem;
        if (idx < 0 || idx >= _config.Clients.Count) return;

        _config.Clients[idx].Enabled = !_config.Clients[idx].Enabled;
        RefreshListViewSource();
        _listView.SelectedItem = idx;
        UpdateToggleButtonLabel();
    }

    private void OnAddClient()
    {
        SaveFieldsToClient();

        var newClient = new ClientConfig
        {
            Key = $"CLIENT{_config.Clients.Count + 1}",
            Currency = "PLN",
            MonthOffsetRule = "early_previous",
            BillingAccount = _config.BillingAccounts.FirstOrDefault()?.Key ?? "",
        };
        _config.Clients.Add(newClient);
        RefreshListViewSource();
        _listView.SelectedItem = _config.Clients.Count - 1;
        LoadClientIntoFields();
    }

    private void OnDeleteClient()
    {
        var idx = _listView.SelectedItem;
        if (idx < 0 || idx >= _config.Clients.Count) return;

        var client = _config.Clients[idx];
        var result = MessageBox.Query("Confirm Delete",
            $"Delete client '{client.Key}'?", "Yes", "No");
        if (result == 0)
        {
            _config.Clients.RemoveAt(idx);
            RefreshListViewSource();
            if (_config.Clients.Count > 0)
            {
                _listView.SelectedItem = Math.Min(idx, _config.Clients.Count - 1);
                LoadClientIntoFields();
            }
            else
            {
                foreach (var f in _fields) f.Text = "";
                _selectedAccountKey = "";
                UpdateAccountDisplay();
            }
        }
    }

    private void OnSave()
    {
        SaveFieldsToClient();

        // Update list view keys in case they changed
        RefreshListViewSource();

        try
        {
            ConfigManager.Save(_config);
            MessageBox.Query("Saved", "Configuration saved successfully.", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
