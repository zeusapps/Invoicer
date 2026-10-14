using System.Globalization;
using System.Text;
using Invoicer.Tui.Dialogs;
using Terminal.Gui;

namespace Invoicer.Tui.Views;

/// <summary>Native date editing with visible rejection instead of silent clamping or stale input.</summary>
internal sealed class ServicePeriodDateField : DateField
{
    internal string? InputError { get; private set; }
    internal event Action? InputValidityChanged;

    internal ServicePeriodDateField(DateTime date) : base(date)
    {
        using (new DateCultureScope()) Culture = ServiceDateDialog.DateCulture;
        TextChanging += (_, args) =>
        {
            SetError(args.Cancel || !IsValid(args.NewValue)
                ? "Enter a valid date (dd.MM.yyyy), or select it with Pick." : null);
        };
        AddCommand(Command.Paste, () => { using var scope = new DateCultureScope(); Paste(); return true; });
    }

    // The package uses the ambient culture while parsing. Scope only native operations,
    // restoring the caller's culture afterwards instead of changing application formatting.
    public new DateTime Date
    {
        get => base.Date;
        set { using var scope = new DateCultureScope(); base.Date = value; SetError(IsValid(base.Text) ? null : "Enter a valid date (dd.MM.yyyy)."); }
    }

    public new string Text
    {
        get => base.Text;
        set { using var scope = new DateCultureScope(); base.Text = value; }
    }

    protected override bool OnKeyDownNotHandled(Key key)
    {
        using var scope = new DateCultureScope();
        if (key >= Key.D0 && key <= Key.D9 && !ReadOnly)
        {
            var candidate = Text.ToCharArray();
            if (CursorPosition < candidate.Length)
            {
                candidate[CursorPosition] = (char)((Rune)key).Value;
                if (!IsValid(new string(candidate)))
                {
                    SetError($"Invalid date: {new string(candidate).Trim()}. Correct it or use Pick.");
                    return true;
                }
                SetError(null);
            }
        }
        return base.OnKeyDownNotHandled(key);
    }

    public override void DeleteCharLeft(bool useOldCursorPos = true)
    {
        using var scope = new DateCultureScope();
        if (!ReadOnly && !CanReplaceWithZero()) return;
        base.DeleteCharLeft(useOldCursorPos);
    }

    public override void DeleteCharRight()
    {
        using var scope = new DateCultureScope();
        if (!ReadOnly && !CanReplaceWithZero()) return;
        base.DeleteCharRight();
    }

    private bool CanReplaceWithZero()
    {
        var candidate = Text.ToCharArray();
        if (CursorPosition >= candidate.Length) return false;
        candidate[CursorPosition] = '0';
        if (IsValid(new string(candidate))) { SetError(null); return true; }
        SetError($"Invalid date: {new string(candidate).Trim()}. Correct it or use Pick.");
        return false;
    }

    private static bool IsValid(string text) => DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy",
        CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && date.Date != default;

    private void SetError(string? error)
    {
        if (InputError == error) return;
        InputError = error;
        InputValidityChanged?.Invoke();
    }
}

internal sealed class DateCultureScope : IDisposable
{
    private readonly CultureInfo _original = CultureInfo.CurrentCulture;
    internal DateCultureScope() => CultureInfo.CurrentCulture = ServiceDateDialog.DateCulture;
    public void Dispose() => CultureInfo.CurrentCulture = _original;
}
