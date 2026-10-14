using System.Globalization;
using Terminal.Gui;
using Invoicer.Tui.Views;

namespace Invoicer.Tui.Dialogs;

internal sealed class ServiceDateDialog : Dialog
{
    internal static CultureInfo DateCulture { get; } = CreateDateCulture();
    internal DatePicker Picker { get; }
    internal DateTime? SelectedDate { get; private set; }

    internal ServiceDateDialog(string title, DateTime date)
    {
        Title = title;
        Width = 38;
        Height = 18;
        using var culture = new DateCultureScope();
        Picker = new DatePicker(date) { X = 1, Y = 1 };
        var dateDisplay = Picker.Subviews.OfType<DateField>().Single();
        dateDisplay.Date = date;
        dateDisplay.Enabled = false;
        foreach (var button in Picker.Subviews.OfType<Button>())
            button.Accepting += (_, e) => e.Cancel = true;
        Add(Picker);
        var accept = new Button { Text = "OK", IsDefault = true };
        var cancel = new Button { Text = "Cancel" };
        accept.Accepting += (_, e) => { e.Cancel = true; AcceptSelection(); Application.RequestStop(this); };
        cancel.Accepting += (_, e) => { e.Cancel = true; Application.RequestStop(this); };
        AddButton(accept);
        AddButton(cancel);
    }

    internal void AcceptSelection() => SelectedDate = Picker.Date.Date;

    private static CultureInfo CreateDateCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-GB").Clone();
        culture.DateTimeFormat.ShortDatePattern = "dd.MM.yyyy";
        culture.DateTimeFormat.DateSeparator = ".";
        return culture;
    }
}
