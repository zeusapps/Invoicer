using System.Reflection;
using Invoicer.Models;
using Invoicer.Tui.Views;
using Terminal.Gui;
using Xunit;

namespace Invoicer.Tests;

public class InvoiceTaxUiTests
{
    private static T Field<T>(object view, string name) => (T)view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;

    [Fact]
    public void EditorSwitchesChoicesPreservesContradictionsAndClearsOnlyOnCategoryEdit()
    {
        var config = new AppConfig { Clients = [Samples.Client("US"), Samples.Client("GB"), Samples.Client("EE", false), Samples.Client("PL")] };
        using var view = new ClientListView(config, () => { });
        var label = Field<Label>(view, "_taxTreatmentLabel");
        var radio = Field<RadioGroup>(view, "_reverseChargeRadio");
        var fields = Field<TextField[]>(view, "_fields");
        Assert.Contains("no reverse charge", label.Text.ToString()); Assert.Equal(1, radio.SelectedItem); Assert.True(radio.Visible);
        radio.SelectedItem = 2;
        Assert.Contains("Outside Polish VAT - reverse charge", label.Text.ToString());
        view.SelectClient(1);
        Assert.True(config.Clients[0].ReverseCharge); Assert.Equal(0, radio.SelectedItem);
        Assert.Contains("explicit reverse_charge", label.Text.ToString());
        radio.SelectedItem = 1;
        view.SelectClient(2);
        Assert.False(config.Clients[1].ReverseCharge);
        Assert.False(radio.Visible); Assert.Contains("require reverse_charge", label.Text.ToString());
        view.SelectClient(3);
        Assert.False(config.Clients[2].ReverseCharge); // Viewing it must not repair a contradictory TOML value.
        Assert.Equal("Domestic Polish VAT", label.Text.ToString());
        fields[5].Text = "EE";
        Assert.Contains("must be 0", label.Text.ToString());
        fields[9].Text = "0";
        Assert.Equal("EU B2B reverse charge", label.Text.ToString());
        fields[5].Text = "GB";
        Assert.Equal(0, radio.SelectedItem); Assert.True(radio.Visible);
        view.SelectClient(0);
        Assert.Equal("GB", config.Clients[3].Country); Assert.Null(config.Clients[3].ReverseCharge);
        Assert.Equal(2, radio.SelectedItem);
    }

    [Theory]
    [InlineData("PL", null, "Domestic Polish VAT", "230.00 PLN", "1,230.00 PLN", false)]
    [InlineData("EE", null, "EU B2B reverse charge", "0.00 PLN", "1,000.00 PLN", true)]
    [InlineData("US", null, "Outside Polish VAT - no reverse charge", "0.00 PLN", "1,000.00 PLN", false)]
    [InlineData("US", true, "Outside Polish VAT - reverse charge", "0.00 PLN", "1,000.00 PLN", true)]
    public void PreviewUsesSharedTaxAmountsAndAnnotation(string country, bool? choice, string treatment, string vat, string gross, bool reverse)
    {
        var client = Samples.Client(country, choice); client.Currency = "PLN"; // No live rate lookup in this UI check.
        using var view = new CreateInvoiceView(new AppConfig { Clients = [client] });
        var preview = Field<Label>(view, "_previewLabel").Text.ToString();
        Assert.Contains(treatment, preview); Assert.Contains(vat, preview); Assert.Contains(gross, preview);
        Assert.Equal(reverse, preview!.Contains("Reverse charge / odwrotne obciążenie"));
        client.Country = "EE"; client.VatRate = 23;
        typeof(CreateInvoiceView).GetMethod("UpdatePreview", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null);
        Assert.Contains("must be 0", Field<Label>(view, "_previewLabel").Text.ToString());
    }
}
