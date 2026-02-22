using Terminal.Gui;

namespace Invoicer.Tui.Dialogs;

public static class InvoiceResultDialog
{
    public static void Show(string invoiceNumber, List<string> filePaths)
    {
        var fileList = string.Join("\n", filePaths.Select(p => $"  {p}"));
        MessageBox.Query("Invoice Generated",
            $"Invoice {invoiceNumber} created successfully!\n\nGenerated files:\n{fileList}",
            "OK");
    }
}
