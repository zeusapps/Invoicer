using System.Reflection;
using System.Globalization;
using System.Threading.Channels;
using Invoicer.Exchange;
using Invoicer.Generation;
using Invoicer.Models;
using Invoicer.Tui.Dialogs;
using Invoicer.Tui.Views;
using Terminal.Gui;
using Xunit;

namespace Invoicer.Tests;

public class ServicePeriodUiTests
{
    private static T Field<T>(object view, string name) =>
        (T)view.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(view)!;
    private static void Invoke(object view, string method) =>
        view.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(view, null);
    private static ServicePeriodDateField Start(CreateInvoiceView view) => Field<ServicePeriodDateField>(view, "_periodStartField");
    private static ServicePeriodDateField End(CreateInvoiceView view) => Field<ServicePeriodDateField>(view, "_periodEndField");
    private static RadioGroup Mode(CreateInvoiceView view) => Field<RadioGroup>(view, "_periodModeRadio");
    private static TextField Date(CreateInvoiceView view) => Field<TextField>(view, "_dateField");
    private static string Preview(CreateInvoiceView view) => Field<Label>(view, "_previewLabel").Text.ToString()!;

    [Fact]
    public void AutomaticAndCustomTransitionsPreserveTheChosenDates()
    {
        var first = Samples.Client("PL"); var second = Samples.Client("PL"); second.MonthOffsetRule = "early_current";
        using var view = new CreateInvoiceView(new AppConfig { Clients = [first, second] });
        Date(view).Text = "05.10.2026";
        Assert.Equal("01.09.2026", Start(view).Text.ToString()!.Trim());
        Assert.Equal("30.09.2026", End(view).Text.ToString()!.Trim());
        Assert.False(Start(view).Enabled); Assert.False(Field<Button>(view, "_periodEndPickButton").Enabled);
        Assert.Contains("01.09.2026 - 30.09.2026", Preview(view));
        Mode(view).SelectedItem = 1;
        Assert.True(Start(view).Enabled); Assert.True(Field<Button>(view, "_periodStartPickButton").Enabled);
        view.ApplyPickedDate(true, new DateTime(2026, 9, 15));
        view.ApplyPickedDate(false, new DateTime(2026, 10, 14));
        Date(view).Text = "05.11.2026";
        Assert.Contains("15.09.2026 - 14.10.2026", Preview(view));
        view.ApplyPickedDate(true, null);
        Assert.Equal(new DateTime(2026, 9, 15), Start(view).Date);
        Assert.True(view.TryGetDates(out var date, out var period, out _));
        Assert.Equal(new DateTime(2026, 11, 5), date); Assert.Equal(new DateTime(2026, 10, 14), period!.End);
        Mode(view).SelectedItem = 0;
        Assert.Contains("01.10.2026 - 31.10.2026", Preview(view));
        Mode(view).SelectedItem = 1;
        Field<RadioGroup>(view, "_clientRadio").SelectedItem = 1;
        Assert.Equal(0, Mode(view).SelectedItem); Assert.Contains("01.11.2026 - 30.11.2026", Preview(view));
        Mode(view).SelectedItem = 1;
        Invoke(view, "ResetDateSelection");
        Assert.Equal(0, Mode(view).SelectedItem);
        Assert.Equal("early_previous", first.MonthOffsetRule);
    }

    [Fact]
    public void InvalidInputHasNoValidLookingPreviewAndCanBeCorrected()
    {
        using var view = new CreateInvoiceView(new AppConfig { Clients = [Samples.Client("PL")] });
        Date(view).Text = "bad date";
        Assert.False(view.TryGetDates(out _, out _, out _)); Assert.Contains("valid invoice date", Preview(view));
        Date(view).Text = "05.10.2026";
        Mode(view).SelectedItem = 1;
        Start(view).Date = new DateTime(2026, 10, 15);
        Assert.False(view.TryGetDates(out _, out _, out _)); Assert.Contains("must not be after", Preview(view));
        Assert.Equal(1, Mode(view).SelectedItem);
        End(view).Date = new DateTime(2026, 10, 16);
        Assert.True(view.TryGetDates(out _, out _, out _)); Assert.Contains("15.10.2026 - 16.10.2026", Preview(view));
        End(view).Text = "";
        Assert.False(view.TryGetDates(out _, out _, out _)); Assert.Contains("valid date", Preview(view));
    }

    [Fact]
    public void CalendarKeepsSelectionSeparateUntilAccepted()
    {
        using var dialog = new ServiceDateDialog("From", new DateTime(2026, 9, 1));
        dialog.Picker.Date = new DateTime(2026, 9, 15);
        Assert.Null(dialog.SelectedDate);
        dialog.AcceptSelection();
        Assert.Equal(new DateTime(2026, 9, 15), dialog.SelectedDate);
    }

    [Fact]
    public void NativeFieldsRejectInvalidInputWithoutChangingAmbientCulture()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            using var view = new CreateInvoiceView(new AppConfig { Clients = [Samples.Client("PL")] });
            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Date(view).Text = "05.10.2026"; Mode(view).SelectedItem = 1;
            Start(view).Text = " 15.09.2026";
            Assert.True(view.TryGetDates(out _, out _, out _));
            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Start(view).Text = "31.09.2026";
            Assert.False(view.TryGetDates(out _, out _, out _));
            view.ApplyPickedDate(true, new DateTime(2026, 9, 15));
            Assert.True(view.TryGetDates(out _, out _, out _));
            using var dialog = new ServiceDateDialog("From", Start(view).Date);
            Assert.Equal("en-US", CultureInfo.CurrentCulture.Name);
            Assert.Equal(new DateTime(2026, 9, 15), dialog.Picker.Subviews.OfType<DateField>().Single().Date);
        }
        finally { CultureInfo.CurrentCulture = original; }
    }

    [Fact]
    public async Task UnavailableRatesRemainEditableWithoutFalseProvenance()
    {
        var host = new RateHost();
        using var view = host.Create(Samples.Client("US"));
        var request = await host.Next();
        request.Result.SetResult(NbpRateResult.Failed("NBP unavailable"));
        var dispatch = await host.Dispatches.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        dispatch();
        Assert.Equal("NBP unavailable", Field<Label>(view, "_rateStatusLabel").Text.ToString());
        Assert.Equal("", Field<TextField>(view, "_rateField").Text.ToString());
        Field<TextField>(view, "_rateField").Text = "3.80";
        Invoke(view, "UpdatePreview");
        Assert.Contains("entered by hand", Preview(view));
        Mode(view).SelectedItem = 1;
        Assert.Equal("3.80", Field<TextField>(view, "_rateField").Text.ToString());
        Assert.False(host.Pending.Reader.TryRead(out _));
    }

    [Fact]
    public async Task RateDefaultsFollowPeriodEndAndManualOverridesSurviveChanges()
    {
        var host = new RateHost();
        using var view = host.Create(Samples.Client("US"));
        await host.CompleteNext();
        Date(view).Text = "25.10.2026";
        await host.CompleteNext();
        Date(view).Text = "05.10.2026";
        await host.CompleteNext();
        Assert.Equal("30.09.2026", Field<TextField>(view, "_rateDateField").Text.ToString());
        Mode(view).SelectedItem = 1;
        End(view).Date = new DateTime(2026, 9, 15);
        Assert.Equal("15.09.2026", Field<TextField>(view, "_rateDateField").Text.ToString());
        Assert.Equal("", Field<TextField>(view, "_rateField").Text.ToString());
        await host.CompleteNext();
        Field<TextField>(view, "_rateDateField").Text = "10.09.2026";
        await host.CompleteNext();
        End(view).Date = new DateTime(2026, 9, 20);
        Date(view).Text = "05.11.2026";
        Assert.Equal("10.09.2026", Field<TextField>(view, "_rateDateField").Text.ToString());
        Field<TextField>(view, "_rateField").Text = "3.80";
        Start(view).Date = new DateTime(2026, 9, 5);
        Assert.Equal("3.80", Field<TextField>(view, "_rateField").Text.ToString());
        Assert.False(host.Pending.Reader.TryRead(out _));
        Field<TextField>(view, "_rateDateField").Text = "20.09.2026";
        await host.CompleteNext();
        End(view).Date = new DateTime(2026, 9, 25);
        Assert.Equal("25.09.2026", Field<TextField>(view, "_rateDateField").Text.ToString());
        Assert.Equal("", Field<TextField>(view, "_rateField").Text.ToString());
        await host.CompleteNext();
        Assert.Contains("NBP A TEST", Preview(view));
        Field<TextField>(view, "_rateField").Text = "3.80";
        Invoke(view, "UpdatePreview");
        Assert.Contains("entered by hand", Preview(view)); Assert.DoesNotContain("NBP A TEST", Preview(view));
    }

    [Fact]
    public async Task SupersededCallbacksAndInvalidInputsCannotApplyOrStartLookups()
    {
        var host = new RateHost();
        using var view = host.Create(Samples.Client("US"), Samples.Client("PL"));
        var initial = await host.Next();
        initial.Result.SetResult(RateHost.Result(4m));
        var obsoleteCallback = await host.Dispatches.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        Date(view).Text = "25.10.2026";
        await host.CompleteNext(3m);
        obsoleteCallback();
        Assert.Equal("3", Field<TextField>(view, "_rateField").Text.ToString());
        Date(view).Text = "invalid";
        Assert.False(view.TryGetDates(out _, out _, out _));
        Assert.Equal("", Field<TextField>(view, "_rateField").Text.ToString());
        Assert.False(host.Pending.Reader.TryRead(out _));
        Date(view).Text = "25.10.2026";
        await host.CompleteNext();
        Field<RadioGroup>(view, "_clientRadio").SelectedItem = 1;
        Assert.False(Field<TextField>(view, "_rateField").Visible);
        Assert.False(host.Pending.Reader.TryRead(out _));
        Field<RadioGroup>(view, "_clientRadio").SelectedItem = 0;
        await host.CompleteNext();
        Assert.True(Field<TextField>(view, "_rateField").Visible);
    }

    [Fact]
    public async Task RateEnteredDuringALookupIsNotOverwrittenByTheResponse()
    {
        var host = new RateHost();
        using var view = host.Create(Samples.Client("US"));
        var request = await host.Next();
        Field<TextField>(view, "_rateField").Text = "3.80";
        request.Result.SetResult(RateHost.Result(4m));
        var dispatch = await host.Dispatches.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        dispatch();
        Assert.Equal("3.80", Field<TextField>(view, "_rateField").Text.ToString());
        Invoke(view, "UpdatePreview");
        Assert.Contains("entered by hand", Preview(view));
    }

    private sealed record Pending(string Currency, DateTime Date, CancellationToken Token, TaskCompletionSource<NbpRateResult> Result);
    private sealed class RateHost
    {
        internal Channel<Pending> Pending { get; } = Channel.CreateUnbounded<Pending>();
        internal Channel<Action> Dispatches { get; } = Channel.CreateUnbounded<Action>();
        internal CreateInvoiceView Create(params ClientConfig[] clients) => new(new AppConfig { Clients = clients.ToList() },
            (currency, date, token) =>
            {
                var request = new Pending(currency, date, token, new(TaskCreationOptions.RunContinuationsAsynchronously));
                Pending.Writer.TryWrite(request); return request.Result.Task;
            }, action => Dispatches.Writer.TryWrite(action));
        internal Task<Pending> Next() => Pending.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
        internal async Task CompleteNext(decimal rate = 3.7998m)
        {
            var request = await Next(); Assert.False(request.Token.IsCancellationRequested);
            request.Result.SetResult(Result(rate));
            var action = await Dispatches.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            action();
        }
        internal static NbpRateResult Result(decimal rate) => NbpRateResult.Success(new NbpRate(rate, new DateTime(2026, 9, 9), "TEST"));
    }
}
