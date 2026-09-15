using Invoicer.Config;
using Invoicer.Models;
using Invoicer.Tui.Dialogs;
using Invoicer.Tui.Views;
using Invoicer.Update;
using Terminal.Gui;

namespace Invoicer.Tui;

public class InvoicerApp
{
    private readonly AppConfig _config;
    private Window _mainWindow = null!;

    public InvoicerApp(AppConfig config)
    {
        _config = config;
    }

    public void Run()
    {
        Application.Init();

        try
        {
            var top = new Toplevel
            {
                X = 0,
                Y = 0,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
            };

            _mainWindow = new Window
            {
                X = 0,
                Y = 1,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Title = "Invoicer",
            };

            var menuBar = new MenuBar
            {
                Menus =
                [
                    new MenuBarItem("_Invoice", new MenuItem[]
                    {
                        new("_Create New", "Ctrl+N", () => Application.Invoke(() => ShowCreateInvoice())),
                        new("E_xit", "Ctrl+Q", () => Application.RequestStop()),
                    }),
                    new MenuBarItem("_Clients", new MenuItem[]
                    {
                        new("_List/Edit Clients", "", () => Application.Invoke(() => ShowClientList())),
                        new("_Add Client", "", () => Application.Invoke(() => ShowAddClient())),
                    }),
                    new MenuBarItem("_Settings", new MenuItem[]
                    {
                        new("_Supplier Info", "", () => Application.Invoke(() => ShowSettings("supplier"))),
                        new("_Billing Accounts", "", () => Application.Invoke(() => ShowView(new BillingAccountListView(_config)))),
                        new("_Output Settings", "", () => Application.Invoke(() => ShowSettings("output"))),
                    }),
                    new MenuBarItem("_Help", new MenuItem[]
                    {
                        new("Check for _Updates", "", () => Application.Invoke(() => CheckForUpdates())),
                        new("_About", "", () => Application.Invoke(() => ShowAbout())),
                    }),
                ],
            };

            top.Add(menuBar, _mainWindow);
            ShowCreateInvoice();
            StartBackgroundUpdateCheck();
            Application.Run(top);
            top.Dispose();
        }
        finally
        {
            Application.Shutdown();
        }
    }

    private void ShowView(View view)
    {
        _mainWindow.RemoveAll();
        _mainWindow.Add(view);
        _mainWindow.SetNeedsDraw();
        view.SetFocus();
    }

    private void ShowCreateInvoice()
    {
        ShowView(new CreateInvoiceView(_config));
    }

    private void ShowClientList()
    {
        ShowView(new ClientListView(_config, () => ShowClientList()));
    }

    private void ShowAddClient()
    {
        var newClient = new ClientConfig
        {
            Key = $"CLIENT{_config.Clients.Count + 1}",
            Currency = "PLN",
            MonthOffsetRule = "early_previous",
            BillingAccount = _config.BillingAccounts.FirstOrDefault()?.Key ?? "",
        };
        _config.Clients.Add(newClient);
        var view = new ClientListView(_config, () => ShowClientList());
        view.SelectClient(_config.Clients.Count - 1);
        ShowView(view);
    }

    private void ShowSettings(string tab)
    {
        ShowView(new SettingsView(_config, tab));
    }

    /// <summary>
    /// Fire-and-forget check that stays completely silent unless a newer version exists.
    /// An update check is never worth an error dialog, let alone delaying startup.
    /// </summary>
    private void StartBackgroundUpdateCheck()
    {
        if (!_config.Update.CheckOnStartup)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                var result = await UpdateChecker.CheckAsync(_config.Update.Repository, AppVersion.Current);

                if (result.Status != UpdateCheckStatus.UpdateAvailable || result.Release is null)
                    return;

                // The user chose to skip this exact version on an earlier run.
                if (result.Release.Tag == _config.Update.DismissedVersion)
                    return;

                Application.Invoke(() =>
                {
                    // The check can outlive the TUI; there is nothing to show once it has stopped.
                    if (Application.Top is null)
                        return;

                    PresentUpdate(result.Release);
                });
            }
            catch
            {
                // Offline, rate-limited, or GitHub changed something. Silence is the whole point.
            }
        });
    }

    private void CheckForUpdates()
    {
        var result = RunWithProgress(
            "Checking for updates...",
            () => UpdateChecker.CheckAsync(_config.Update.Repository, AppVersion.Current),
            UpdateCheckResult.Failed);

        switch (result.Status)
        {
            case UpdateCheckStatus.UpdateAvailable when result.Release is not null:
                PresentUpdate(result.Release);
                break;

            case UpdateCheckStatus.UpToDate:
                MessageBox.Query("Check for Updates",
                    $"Invoicer v{AppVersion.Display} is up to date.", "OK");
                break;

            default:
                MessageBox.ErrorQuery("Check for Updates",
                    $"Could not check for updates.\n\n{result.FailureReason}", "OK");
                break;
        }
    }

    private void PresentUpdate(ReleaseInfo release)
    {
        switch (UpdateDialog.Show(release, AppVersion.Display))
        {
            case UpdateChoice.Install:
                InstallUpdate(release);
                break;

            case UpdateChoice.OpenReleasePage:
                OpenReleasePage(release);
                break;

            case UpdateChoice.Later:
                DismissVersion(release);
                break;
        }
    }

    private void InstallUpdate(ReleaseInfo release)
    {
        var result = RunWithProgress(
            $"Downloading {release.Name} ({release.AssetSizeDisplay})...",
            () => UpdateInstaller.InstallAsync(release),
            InstallResult.Fail);

        if (result.Success)
        {
            // The new executable is already running; this instance steps aside.
            Application.RequestStop();
            return;
        }

        MessageBox.ErrorQuery("Update Failed",
            $"The update could not be installed. Invoicer is unchanged.\n\n{result.Error}\n\n"
            + $"You can download it manually from:\n{release.ReleaseUrl}",
            "OK");
    }

    private void DismissVersion(ReleaseInfo release)
    {
        if (_config.Update.DismissedVersion == release.Tag)
            return;

        try
        {
            _config.Update.DismissedVersion = release.Tag;
            ConfigManager.Save(_config);
        }
        catch
        {
            // Failing to remember the dismissal only means asking again next time.
        }
    }

    private static void OpenReleasePage(ReleaseInfo release)
    {
        if (string.IsNullOrEmpty(release.ReleaseUrl))
            return;

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = release.ReleaseUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            MessageBox.ErrorQuery("Open Release Page",
                $"Could not open the browser.\n\n{release.ReleaseUrl}\n\n{ex.Message}", "OK");
        }
    }

    /// <summary>
    /// Runs a network operation off the UI thread behind a modal notice, so the TUI keeps
    /// redrawing while it is in flight.
    /// </summary>
    private static T RunWithProgress<T>(string message, Func<Task<T>> operation, Func<string, T> onError)
    {
        var dialog = new Dialog
        {
            Title = "Please Wait",
            Width = Dim.Percent(60),
            Height = 5,
        };
        dialog.Add(new Label { X = 0, Y = 0, Width = Dim.Fill(), Text = message });

        var result = onError("The operation did not complete.");

        _ = Task.Run(async () =>
        {
            try
            {
                result = await operation();
            }
            catch (Exception ex)
            {
                result = onError(ex.Message);
            }
            finally
            {
                Application.Invoke(() => Application.RequestStop(dialog));
            }
        });

        Application.Run(dialog);
        dialog.Dispose();

        return result;
    }

    private void ShowAbout()
    {
        MessageBox.Query("About Invoicer",
            $"Invoicer v{AppVersion.Display}\n\nBilingual invoice generator\n(English / Ukrainian)\n\nGenerates DOCX, PDF, and KSeF XML invoices.",
            "OK");
    }
}
