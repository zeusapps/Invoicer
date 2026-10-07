# Screenshot provenance

Refreshed on 2026-10-07 from the current application and the fictional complete TOML example in the repository README. No operational configuration is used.

- `create_invoice.png`: Estonia, EUR 1000 net, EU B2B reverse charge, manually entered illustrative EUR/PLN rate 4.25, DOCX/PDF/XML selected.
- `create_invoice_us.png`: US, USD 1000 net, no reverse charge, manually entered illustrative USD/PLN rate 3.7998, DOCX/PDF/XML selected.
- `clients_estonia.png`: the Estonian client's derived EU treatment and VAT identifier.
- `clients.png`: the US client's independent reverse-charge choice, set to No.
- `settings.png`: fictional Polish supplier with matching NIP/EU VAT identifiers.
- `pdf_output.png`: the generated Estonian sample PDF, rendered with Windows.Data.Pdf; invoice date 2026-10-07, number 2026/EE/0001.

TUI images are rendered from the actual Terminal.Gui character/color buffers while running `InvoicerApp` with its off-screen `FakeDriver`. They show the application's real views and controls, without terminal-window chrome. Capture sizes are 140×32 cells for invoice previews, 176×49 for client editors, and 140×25 for supplier settings. PNG rendering uses Consolas; terminal colors/fonts can differ on another machine. Background update checks and pending rate lookups are disabled/cancelled for deterministic captures.

The temporary capture harness and buffers live in the ignored `output/readme-screenshots/` directory. Regenerate the fictional invoice documents with the sample command in the root README. The PDF image comes from the original application output, without inspection-tool watermarks.
