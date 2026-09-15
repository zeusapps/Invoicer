using System.Collections.ObjectModel;
using Terminal.Gui;
using Invoicer.Models;
using Invoicer.Config;

namespace Invoicer.Tui.Views;

public class BillingAccountListView : View
{
    private readonly AppConfig _config;
    private readonly ListView _listView;
    private readonly TextField[] _fields;
    private int _previousIndex = -1;

    // Key each account had when this view last saved (null for accounts added since), so a
    // key edit can be carried over to the clients that reference it.
    private readonly List<string?> _savedKeys;

    private readonly string[] _fieldLabels =
    [
        "Key:", "Label:", "IBAN:", "Bank:", "SWIFT:", "Currency:",
    ];

    public BillingAccountListView(AppConfig config)
    {
        _config = config;
        _savedKeys = _config.BillingAccounts.Select(a => (string?)a.Key).ToList();

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        var listFrame = new FrameView
        {
            Title = "Billing Accounts",
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
        _listView.SelectedItemChanged += (_, _) => LoadAccountIntoFields();
        listFrame.Add(_listView);

        var detailFrame = new FrameView
        {
            Title = "Account Details",
            X = Pos.Percent(30),
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            CanFocus = true,
        };

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

        Scrolling.EnableVertical(detailFrame);
        Scrolling.ShowScrollBarWhenNeeded(_listView);

        var addButton = new Button { Text = "Add", X = 1, Y = Pos.AnchorEnd(2) };
        addButton.Accepting += (_, e) => { e.Cancel = true; OnAddAccount(); };

        var deleteButton = new Button { Text = "Delete", X = 10, Y = Pos.AnchorEnd(2) };
        deleteButton.Accepting += (_, e) => { e.Cancel = true; OnDeleteAccount(); };

        var saveButton = new Button { Text = "Save All", X = 22, Y = Pos.AnchorEnd(2) };
        saveButton.Accepting += (_, e) => { e.Cancel = true; OnSave(); };

        Add(listFrame, detailFrame, addButton, deleteButton, saveButton);

        if (_config.BillingAccounts.Count > 0)
        {
            _listView.SelectedItem = 0;
            LoadAccountIntoFields();
        }
    }

    private void SaveFieldsToAccount(int? overrideIndex = null)
    {
        var idx = overrideIndex ?? _listView.SelectedItem;
        if (idx < 0 || idx >= _config.BillingAccounts.Count) return;

        var account = _config.BillingAccounts[idx];
        account.Key = _fields[0].Text?.ToString()?.Trim() ?? "";
        account.Label = _fields[1].Text?.ToString() ?? "";
        account.Iban = _fields[2].Text?.ToString() ?? "";
        account.Bank = _fields[3].Text?.ToString() ?? "";
        account.Swift = _fields[4].Text?.ToString() ?? "";
        account.Currency = (_fields[5].Text?.ToString() ?? "").Trim().ToUpperInvariant();
    }

    private void LoadAccountIntoFields()
    {
        if (_previousIndex >= 0)
            SaveFieldsToAccount(_previousIndex);

        var idx = _listView.SelectedItem;
        _previousIndex = idx;
        if (idx < 0 || idx >= _config.BillingAccounts.Count) return;

        var account = _config.BillingAccounts[idx];
        _fields[0].Text = account.Key;
        _fields[1].Text = account.Label;
        _fields[2].Text = account.Iban;
        _fields[3].Text = account.Bank;
        _fields[4].Text = account.Swift;
        _fields[5].Text = account.Currency;
    }

    private void RefreshListViewSource()
    {
        _listView.SetSource(new ObservableCollection<string>(_config.BillingAccounts.Select(a => a.Key)));
    }

    private void OnAddAccount()
    {
        SaveFieldsToAccount();

        _config.BillingAccounts.Add(new BillingAccountConfig { Key = $"ACCOUNT{_config.BillingAccounts.Count + 1}" });
        _savedKeys.Add(null);
        RefreshListViewSource();
        _listView.SelectedItem = _config.BillingAccounts.Count - 1;
        LoadAccountIntoFields();
    }

    private void OnDeleteAccount()
    {
        var idx = _listView.SelectedItem;
        if (idx < 0 || idx >= _config.BillingAccounts.Count) return;

        var account = _config.BillingAccounts[idx];

        // Clients still point at the key as it was last saved, not at an unsaved edit.
        var referencedKey = _savedKeys[idx] ?? account.Key;
        var clients = BillingAccountRules.ClientsReferencing(_config.Clients, referencedKey);
        if (clients.Count > 0)
        {
            MessageBox.ErrorQuery("Account In Use",
                $"Billing account '{referencedKey}' is assigned to: {string.Join(", ", clients)}.\n" +
                "Assign those clients another account first.", "OK");
            return;
        }

        var result = MessageBox.Query("Confirm Delete", $"Delete billing account '{account.Key}'?", "Yes", "No");
        if (result != 0) return;

        _config.BillingAccounts.RemoveAt(idx);
        _savedKeys.RemoveAt(idx);
        _previousIndex = -1;
        RefreshListViewSource();
        if (_config.BillingAccounts.Count > 0)
        {
            _listView.SelectedItem = Math.Min(idx, _config.BillingAccounts.Count - 1);
            LoadAccountIntoFields();
        }
        else
        {
            foreach (var f in _fields) f.Text = "";
        }
    }

    private void OnSave()
    {
        SaveFieldsToAccount();

        var error = BillingAccountRules.ValidateKeys(_config.BillingAccounts);
        if (error is not null)
        {
            MessageBox.ErrorQuery("Cannot Save", error, "OK");
            return;
        }

        var renames = _config.BillingAccounts
            .Select((account, i) => (OldKey: _savedKeys[i], NewKey: account.Key))
            .Where(r => r.OldKey is not null)
            .Select(r => (r.OldKey!, r.NewKey));
        BillingAccountRules.ApplyKeyRenames(_config.Clients, renames);

        RefreshListViewSource();

        try
        {
            ConfigManager.Save(_config);
            for (var i = 0; i < _config.BillingAccounts.Count; i++)
                _savedKeys[i] = _config.BillingAccounts[i].Key;
            MessageBox.Query("Saved", "Configuration saved successfully.", "OK");
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
