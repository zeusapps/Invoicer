using Invoicer.Config;
using Invoicer.Tui;
using Invoicer.Update;

// Remove the executable a previous update set aside. It could not be deleted while it was
// the running image, but it can be now.
UpdateInstaller.CleanupPreviousUpdate();

var config = ConfigManager.Load();
var app = new InvoicerApp(config);
app.Run();
