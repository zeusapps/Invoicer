using Terminal.Gui;
using Invoicer.Models;
using Invoicer.Config;

namespace Invoicer.Tui.Views;

public class SettingsView : View
{
    private readonly AppConfig _config;

    public SettingsView(AppConfig config, string tab)
    {
        _config = config;

        X = 0;
        Y = 0;
        Width = Dim.Fill();
        Height = Dim.Fill();
        CanFocus = true;

        var title = tab == "supplier" ? "Supplier Info" : "Output Settings";
        var frame = new FrameView
        {
            Title = title,
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(3),
            CanFocus = true,
        };

        if (tab == "supplier")
            BuildSupplierFields(frame);
        else
            BuildOutputFields(frame);

        var saveButton = new Button { Text = "Save", X = 1, Y = Pos.AnchorEnd(2) };
        saveButton.Accepting += (_, e) => { e.Cancel = true; OnSave(); };

        Add(frame, saveButton);
    }

    private void BuildSupplierFields(View container)
    {
        var fields = new (string Label, Func<string> Get, Action<string> Set)[]
        {
            ("Name:", () => _config.Supplier.Name, v => _config.Supplier.Name = v),
            ("Name (UA):", () => _config.Supplier.NameUa, v => _config.Supplier.NameUa = v),
            ("TIN:", () => _config.Supplier.Tin, v => _config.Supplier.Tin = v),
            ("REGON:", () => _config.Supplier.Regon, v => _config.Supplier.Regon = v),
            ("VAT:", () => _config.Supplier.Vat, v => _config.Supplier.Vat = v),
            ("Address:", () => _config.Supplier.Address, v => _config.Supplier.Address = v),
            ("Address (UA):", () => _config.Supplier.AddressUa, v => _config.Supplier.AddressUa = v),
            ("IBAN:", () => _config.Supplier.Iban, v => _config.Supplier.Iban = v),
            ("Bank:", () => _config.Supplier.Bank, v => _config.Supplier.Bank = v),
            ("SWIFT:", () => _config.Supplier.Swift, v => _config.Supplier.Swift = v),
        };

        int y = 1;
        foreach (var (label, get, set) in fields)
        {
            container.Add(new Label { Text = label, X = 1, Y = y });
            var tf = new TextField
            {
                X = 16,
                Y = y,
                Width = Dim.Fill(2),
                ReadOnly = false,
                Text = get(),
            };
            var capturedSet = set;
            tf.HasFocusChanged += (_, e) =>
            {
                if (!e.NewValue) capturedSet(tf.Text?.ToString() ?? "");
            };
            container.Add(tf);
            y += 2;
        }
    }

    private void BuildOutputFields(View container)
    {
        var fields = new (string Label, string Help, Func<string> Get, Action<string> Set)[]
        {
            ("Directory:", "Base output directory", () => _config.Output.Directory, v => _config.Output.Directory = v),
            ("Pattern:", "Subfolder: {year}", () => _config.Output.Pattern, v => _config.Output.Pattern = v),
            ("Filename:", "{date}, {client}", () => _config.Output.Filename, v => _config.Output.Filename = v),
        };

        int y = 1;
        foreach (var (label, help, get, set) in fields)
        {
            container.Add(new Label { Text = label, X = 1, Y = y });
            var tf = new TextField
            {
                X = 16,
                Y = y,
                Width = Dim.Fill(2),
                ReadOnly = false,
                Text = get(),
            };
            var capturedSet = set;
            tf.HasFocusChanged += (_, e) =>
            {
                if (!e.NewValue) capturedSet(tf.Text?.ToString() ?? "");
            };
            container.Add(tf);
            y++;
            container.Add(new Label { Text = $"  ({help})", X = 16, Y = y });
            y += 2;
        }
    }

    private void OnSave()
    {
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
