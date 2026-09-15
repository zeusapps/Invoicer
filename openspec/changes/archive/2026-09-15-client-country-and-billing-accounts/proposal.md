## Why

Clients have no country, so KSeF generation guesses it from the first two letters of the client's VAT number and falls back to `PL`. A US client has no VAT, so the invoice `20260912_GREENFLOW_PL.xml` went out with `KodKraju PL` in both the buyer's tax identifier and address, and with a fake `NrID` of `N/A`. That value was entered only because KSeF validation made client VAT mandatory. Separately, the supplier has exactly one bank account, but invoices are now issued in PLN and USD and must be paid into different accounts. More accounts, including several in the same currency, are expected later.

## What Changes

- **Client country:** add a required `country` field (ISO 3166-1 alpha-2) to each client, editable in the client editor.
- **Optional client VAT:** VAT is required only for clients in Poland or another EU member state. Clients elsewhere may leave it empty.
- **KSeF buyer identification** in `Podmiot2/DaneIdentyfikacyjne` is chosen from the client's country and VAT, using the four forms the FA(3) schema defines:
  - Polish client: `NIP`
  - other EU client: `KodUE` + `NrVatUE`
  - non-EU client with a tax ID: `KodKraju` + `NrID`
  - client without a tax ID: `BrakID`
  - **BREAKING** for generated XML: Polish buyers move from `KodKraju` + `NrID` to `NIP`, and EU buyers move to `KodUE` + `NrVatUE`.
- **KSeF tax-rate coding** is derived from the client's country. Today every invoice is coded as if it were 23%: the net amount goes into `P_13_1` and the VAT into `P_14_1`, and a 0% client gets `P_12 = 0`, which the schema rejects.
  - non-EU client: `P_12 = np I`, net in `P_13_8`, no VAT total, `P_18 = 1`;
  - EU client: `P_12 = np II`, net in `P_13_9`, no VAT total, `P_18 = 1`, and the seller is marked with `PrefiksPodatnika = PL`;
  - Polish client: numeric rate 23/22, 8/7 or 5, with net and VAT in `P_13_1`/`P_14_1`, `P_13_2`/`P_14_2` or `P_13_3`/`P_14_3`;
  - a foreign client with a non-zero VAT rate, or a Polish client with a rate outside that set, fails KSeF validation.
- **KSeF buyer address:** `Podmiot2/Adres/KodKraju` always carries the client's country. The system never falls back to `PL` silently; a missing or invalid country fails validation.
- **DOCX/PDF VAT line:** the customer's VAT line is left out when the client has no VAT.
- **Billing accounts:** add `[[billing_accounts]]` entries (key, label, IBAN, bank, SWIFT, optional currency), managed on a new Billing Accounts screen under the Settings menu.
- **Account per client:** each client references exactly one billing account by key, chosen in the client editor. Invoices use that account in DOCX, PDF and KSeF `Platnosc/RachunekBankowy`, which also gains `NazwaBanku`.
- **Account on Create Invoice:** the preview shows the client's billing account, read-only. To use a different account, the user changes it on the client.
- **BREAKING** config shape: `iban`, `bank` and `swift` move off `[supplier]`. Existing configs migrate on load: the old supplier bank details become a single billing account assigned to every client that has none.
- **Config migration:** clients loaded without `country` get it from their VAT prefix when that prefix is an EU VAT code. Otherwise it stays empty until the user sets it.
- **Currency mismatch warning:** the client editor warns when a client's currency differs from its account's currency. It does not block saving or generation.

## Capabilities

### New Capabilities
- `billing-accounts`: defining multiple supplier bank accounts, assigning one to each client, migrating the legacy single account, and how the assigned account appears on generated invoices and in the invoice preview.
- `client-country`: the client country field, the country-dependent VAT requirement, migration from existing configs, and how client VAT appears on DOCX/PDF invoices.

### Modified Capabilities
- `ksef-xml-generation`:
  - buyer identification becomes country-driven (NIP / KodUE+NrVatUE / KodKraju+NrID / BrakID), replacing the "buyer identification is unchanged" scenario;
  - the buyer address country comes from the client;
  - validation requires client country instead of client VAT;
  - payment details come from the client's billing account;
  - the tax-rate code, net/VAT summary fields and reverse-charge annotation follow the client's country.

## Impact

- **Models:** `ClientConfig` gains `Country` and `BillingAccount`. There is a new `BillingAccountConfig`. `AppConfig` gains `BillingAccounts`. `SupplierConfig` loses `Iban`, `Bank` and `Swift`. `Invoice` carries the resolved billing account.
- **Config:** `ConfigManager` handles load, save and migration of the new sections and fields, and `CreateDefault` gets a default account and client country.
- **Generation:**
  - `KsefXmlGenerator`: buyer identification, address country, validation and payment block.
  - `DocxGenerator` and `PdfGenerator`: bank block from the account, and the VAT line becomes conditional.
- **TUI:**
  - `ClientListView`: country field, account picker, currency warning.
  - new billing accounts view.
  - `SettingsView`: bank fields removed from supplier info.
  - `InvoicerApp`: menu entry.
  - `CreateInvoiceView`: account shown in the preview.
- **Tests:**
  - `KsefXmlGeneratorTests`: the buyer identification test and the reference fixture assertion on `NrID` change for Polish buyers; new schema-validated cases for EU, non-EU and no-ID buyers.
  - `ConfigManagerTests` and `InvoiceOutputBehaviorTests`: fixtures move from supplier bank fields to accounts.
- **No new dependencies.**
