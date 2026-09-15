using Invoicer.Update;
using Terminal.Gui;

namespace Invoicer.Tui.Dialogs;

public enum UpdateChoice
{
    /// <summary>Dismiss this version; the startup check stays quiet until something newer appears.</summary>
    Later,

    /// <summary>Download and swap in the new executable.</summary>
    Install,

    /// <summary>Open the release page and leave the running version alone.</summary>
    OpenReleasePage,
}

public static class UpdateDialog
{
    public static UpdateChoice Show(ReleaseInfo release, string currentVersion)
    {
        var choice = UpdateChoice.Later;

        var dialog = new Dialog
        {
            Title = "Update Available",
            Width = Dim.Percent(80),
            Height = Dim.Percent(70),
        };

        var summary = new Label
        {
            X = 0,
            Y = 0,
            Text = $"{release.Name} is available.\n"
                   + $"You are running {currentVersion}.\n"
                   + $"Download size: {release.AssetSizeDisplay}",
        };

        var notesFrame = new FrameView
        {
            Title = "Release Notes",
            X = 0,
            Y = 4,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
        };

        // Release notes are Markdown; shown as-is rather than rendered.
        var notes = new TextView
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            ReadOnly = true,
            WordWrap = true,
            Text = string.IsNullOrWhiteSpace(release.Notes)
                ? "(no release notes)"
                : release.Notes.Replace("\r\n", "\n"),
        };
        notesFrame.Add(notes);
        Scrolling.ShowScrollBarWhenNeeded(notes);

        var installButton = new Button { Text = "Install and Restart", IsDefault = true };
        installButton.Accepting += (_, e) =>
        {
            e.Cancel = true;
            choice = UpdateChoice.Install;
            Application.RequestStop(dialog);
        };

        var pageButton = new Button { Text = "Open Release Page" };
        pageButton.Accepting += (_, e) =>
        {
            e.Cancel = true;
            choice = UpdateChoice.OpenReleasePage;
            Application.RequestStop(dialog);
        };

        var laterButton = new Button { Text = "Later" };
        laterButton.Accepting += (_, e) =>
        {
            e.Cancel = true;
            choice = UpdateChoice.Later;
            Application.RequestStop(dialog);
        };

        // A release with no attached executable can only be installed by hand.
        if (release.HasAsset)
            dialog.AddButton(installButton);
        dialog.AddButton(pageButton);
        dialog.AddButton(laterButton);

        dialog.Add(summary, notesFrame);
        Application.Run(dialog);
        dialog.Dispose();

        return choice;
    }
}
