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
    private readonly string[] _fieldLabels =
    [
        "Key:", "Name:", "Name (UA):", "Address:", "Address (UA):",
        "VAT:", "Currency:", "VAT Rate:", "Service Desc:", "Service (UA):",
        "Prefix:", "Default Amt:", "Month Rule:",
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
        _listView.SetSource(new ObservableCollection<string>(_config.Clients.Select(c => c.Key)));
        _listView.SelectedItemChanged += (_, _) => LoadClientIntoFields();
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

        // Buttons at bottom
        var addButton = new Button { Text = "Add", X = 1, Y = Pos.AnchorEnd(2) };
        addButton.Accepting += (_, e) => { e.Cancel = true; OnAddClient(); };

        var deleteButton = new Button { Text = "Delete", X = 10, Y = Pos.AnchorEnd(2) };
        deleteButton.Accepting += (_, e) => { e.Cancel = true; OnDeleteClient(); };

        var saveButton = new Button { Text = "Save All", X = 22, Y = Pos.AnchorEnd(2) };
        saveButton.Accepting += (_, e) => { e.Cancel = true; OnSave(); };

        Add(listFrame, detailFrame, addButton, deleteButton, saveButton);

        if (_config.Clients.Count > 0)
        {
            _listView.SelectedItem = 0;
            LoadClientIntoFields();
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
        client.Vat = _fields[5].Text?.ToString() ?? "";
        client.Currency = _fields[6].Text?.ToString() ?? "";
        if (int.TryParse(_fields[7].Text?.ToString(), out var rate)) client.VatRate = rate;
        client.ServiceDescription = _fields[8].Text?.ToString() ?? "";
        client.ServiceDescriptionUa = _fields[9].Text?.ToString() ?? "";
        client.InvoicePrefix = _fields[10].Text?.ToString() ?? "";
        if (decimal.TryParse(_fields[11].Text?.ToString(), CultureInfo.InvariantCulture, out var amt)) client.DefaultAmount = amt;
        client.MonthOffsetRule = _fields[12].Text?.ToString() ?? "";
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
        _fields[5].Text = client.Vat;
        _fields[6].Text = client.Currency;
        _fields[7].Text = client.VatRate.ToString();
        _fields[8].Text = client.ServiceDescription;
        _fields[9].Text = client.ServiceDescriptionUa;
        _fields[10].Text = client.InvoicePrefix;
        _fields[11].Text = client.DefaultAmount.ToString("F2", CultureInfo.InvariantCulture);
        _fields[12].Text = client.MonthOffsetRule;
    }

    private void OnAddClient()
    {
        SaveFieldsToClient();

        var newClient = new ClientConfig
        {
            Key = $"CLIENT{_config.Clients.Count + 1}",
            Currency = "PLN",
            MonthOffsetRule = "early_previous",
        };
        _config.Clients.Add(newClient);
        _listView.SetSource(new ObservableCollection<string>(_config.Clients.Select(c => c.Key)));
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
            _listView.SetSource(new ObservableCollection<string>(_config.Clients.Select(c => c.Key)));
            if (_config.Clients.Count > 0)
            {
                _listView.SelectedItem = Math.Min(idx, _config.Clients.Count - 1);
                LoadClientIntoFields();
            }
            else
            {
                foreach (var f in _fields) f.Text = "";
            }
        }
    }

    private void OnSave()
    {
        SaveFieldsToClient();

        // Update list view keys in case they changed
        _listView.SetSource(new ObservableCollection<string>(_config.Clients.Select(c => c.Key)));

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
