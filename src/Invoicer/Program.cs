using Invoicer.Config;
using Invoicer.Tui;

var config = ConfigManager.Load();
var app = new InvoicerApp(config);
app.Run();
