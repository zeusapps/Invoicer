using Terminal.Gui;
using Invoicer.Models;
using Invoicer.Config;
using Invoicer.Tui.Views;

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
                        new("_Output Settings", "", () => Application.Invoke(() => ShowSettings("output"))),
                    }),
                    new MenuBarItem("_Help", new MenuItem[]
                    {
                        new("_About", "", () => Application.Invoke(() => ShowAbout())),
                    }),
                ],
            };

            top.Add(menuBar, _mainWindow);
            ShowCreateInvoice();
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

    private void ShowAbout()
    {
        MessageBox.Query("About Invoicer",
            "Invoicer v1.0\n\nBilingual invoice generator\n(English / Ukrainian)\n\nGenerates DOCX and PDF invoices.",
            "OK");
    }
}
