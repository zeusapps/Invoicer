## 1. Country reference data

- [x] 1.1 Add a `Countries` helper with `IsKnown`, `IsEu`, `EuVatPrefix` (GR→EL) and `FromEuVatPrefix` (EL→GR, XI→none), with hardcoded ISO and EU lists (design D1). Verify with unit tests for PL, DE, GR/EL, US, XI and an unknown code.
- [x] 1.2 Add a test that reads `TKodKraju` and `TKodyKrajowUE` from the vendored XSDs and asserts they equal the hardcoded lists, ignoring XI and applying the EL/GR swap. Verify it passes, and that it fails when a code is removed from the list.

## 2. Models and configuration

- [x] 2.1 Add `BillingAccountConfig` (Key, Label, Iban, Bank, Swift, Currency) and `AppConfig.BillingAccounts`. Add `Country` and `BillingAccount` to `ClientConfig`. Remove `Iban`/`Bank`/`Swift` from `SupplierConfig`. Verify `dotnet build` compiles once each usage site from groups 3–5 is updated.
- [x] 2.2 Add `AppConfig.ResolveBillingAccount(client)`, which throws `BillingAccountNotFoundException(clientKey, accountKey)` for an empty or unknown key, plus a non-throwing lookup for the preview. Verify with unit tests for found, empty and unknown keys.
- [x] 2.3 In `ConfigManager`, read and write `[[billing_accounts]]` and the client `country` (upper-cased) and `billing_account`, in the order from design D4. Stop writing supplier bank keys. Verify with a round-trip test: two accounts including a shared currency, client country and account preserved, no `iban`/`bank`/`swift` under `[supplier]`.
- [x] 2.4 Implement the legacy migration in `FromTomlTable`:
  - create a `DEFAULT` account from the raw supplier `iban`/`bank`/`swift` when there are no accounts, and assign it to clients without one;
  - infer an empty client country from an EU VAT prefix;
  - never overwrite an existing country, and skip the migration when accounts already exist.

  Verify with tests matching the "Migrate Legacy Supplier Bank Details" and "Infer Missing Country From EU VAT Prefix On Load" scenarios: PL, EL→GR, `N/A`, explicit `US` kept, already-migrated config untouched.
- [x] 2.5 Mark the config as migrated and make the next `Save` copy the existing file to `config.toml.bak` once before overwriting (design D5). Verify with a test: after loading a legacy file and saving, `config.toml.bak` equals the original content, and a second save doesn't overwrite it.
- [x] 2.6 Update `CreateDefault`: add one placeholder billing account, and give the sample client `Country = "PL"` and that account. Verify that `Generate_ProducesValidDocument_ForDefaultConfiguration`, adjusted to resolve the account, passes.

## 3. Invoice construction

- [x] 3.1 Add a required `BillingAccountConfig` parameter to `Invoice.Create` and an `Invoice.BillingAccount` property. Update callers and test helpers (`KsefXmlGeneratorTests`, `InvoiceOutputBehaviorTests`, `ConfigManagerTests`) to build accounts instead of supplier bank fields. Verify the existing test suite compiles and passes.

## 4. KSeF XML generation

- [x] 4.1 Implement `BuyerIdentification.Resolve(country, vat)`, returning `Nip` / `EuVat` / `ForeignId` / `NoId` with the normalization rules from design D2. Verify with unit tests: `PL9999999999`→NIP `9999999999`; `DE 123 456 789`→`DE`/`123456789`; GR + `EL123456789`→`EL`/`123456789`; US + `12-3456789`→`US`/`12-3456789`; US + empty→NoId.
- [x] 4.2 Rework `Validate` to report:
  - missing or unknown client country;
  - VAT required for PL/EU;
  - an invalid NIP pattern;
  - a VAT prefix that doesn't match the country;
  - an invalid `NrVatUE` pattern;
  - an empty billing-account IBAN.

  Drop the unconditional client VAT check. Verify with tests for each "Validate Required KSeF Fields Before Writing XML" scenario, each asserting that no XML file is written.
- [x] 4.3 Replace the buyer's `KsefParty` identifier fields with the resolved identification. Write `NIP`, `KodUE`+`NrVatUE`, `KodKraju`+`NrID` or `BrakID`=1 before `Nazwa`, and set `Podmiot2/Adres/KodKraju` from the client country. Remove `ExtractCountryCode`. Verify with a theory over PL, DE, GR, US-with-ID and US-without-ID that asserts child element names and values, and that each document passes `KsefSchemaValidator` with no messages.
- [x] 4.4 Write `Platnosc/RachunekBankowy` from `invoice.BillingAccount`: `NrRB` without whitespace, optional `SWIFT`, optional `NazwaBanku`, in schema order. Verify with tests for an account with SWIFT and bank and one with neither, both schema-valid.
- [x] 4.5 Replace `Generate_KeepsBuyerIdentificationCountryCode` with the theory from 4.3. Change `Generate_MatchesReferenceFixtures_ForKeyValues` to compare the fixture's buyer `NrID`, minus its `PL` prefix, with the generated `NIP`. Verify the full `KsefXmlGeneratorTests` class passes.

- [x] 4.6 Implement `TaxRateCoding.Resolve(country, vatRate)` (design D9):
  - non-EU → `np I`/`P_13_8`/`P_18=1`;
  - EU → `np II`/`P_13_9`/`P_18=1`/`PrefiksPodatnika=PL`;
  - PL 23|22 → `P_13_1`/`P_14_1`, 8|7 → `P_13_2`/`P_14_2`, 5 → `P_13_3`/`P_14_3`, with `P_18=2`;
  - errors for a foreign client with a non-zero rate and for a PL client with any other rate.

  Verify with unit tests covering each branch and both errors.
- [x] 4.7 Wire `TaxRateCoding` into `Validate`, `WriteInvoiceBody` (`P_12` code, `P_13_n`, optional `P_14_n`, `P_18`) and `WriteParty` (`Podmiot1/PrefiksPodatnika` before `DaneIdentyfikacyjne`). Update the existing foreign-buyer tests to use rate 0. Verify with schema-validated generator tests for US (np I), DE (np II, PrefiksPodatnika), PL 23% and PL 8%, plus the two validation-error scenarios writing no file.

## 5. DOCX and PDF generation

- [x] 5.1 In `DocxGenerator` and `PdfGenerator`, build the customer block so the `VAT:` line appears only for a non-blank `Client.Vat`, and take IBAN/Bank/SWIFT from `invoice.BillingAccount`. Verify with tests that generate both formats for a client with VAT and one without, and assert the extracted document text contains or omits `VAT:` and contains the account's IBAN. DOCX text comes from OpenXml; PDF text is checked via the source composition model, or failing that, by generating without error plus a manual check in 7.2.

## 6. TUI

- [x] 6.1 Remove the IBAN, Bank and SWIFT rows from `SettingsView` Supplier Info. Verify by running the app: Settings > Supplier Info shows no bank fields.
- [x] 6.2 Add `BillingAccountListView` (list + detail: Key, Label, IBAN, Bank, SWIFT, Currency; Add / Delete / Save All) and a `Settings > Billing Accounts` menu item in `InvoicerApp`. Verify by running the app: an account can be added, saved and seen in `config.toml`.
- [x] 6.3 In `BillingAccountListView`, enforce non-empty and unique keys on Save All, rewrite client references for renamed keys, and refuse to delete an account referenced by clients, naming them. Extract the rename/validation/delete-check logic into a testable helper. Verify with unit tests for duplicate key, rename propagation and blocked delete.
- [x] 6.4 In `ClientListView`, add a `Country:` field (upper-cased on save), an `Account:` label with a `Choose…` button opening a `ListView` dialog of accounts, and a currency mismatch warning label. `OnAddClient` assigns the first account. Verify by running the app: the country and account persist after Save All, and the warning appears for a USD client on a PLN account but not for an account without currency.
- [x] 6.5 In `CreateInvoiceView`, show `Account: {Label} {Iban}`, or `(none assigned)`, in the preview, updating on client change. Resolve the account in `OnGenerate` before `Invoice.Create`, and show `BillingAccountNotFoundException` as an error dialog without generating any file. Verify by running the app: switching clients updates the account line, and a client with an unknown account key produces the error and no files.

- [x] 6.6 Add a shared `Scrolling` helper and apply it to every form so a short console window scrolls instead of clipping: Create Invoice form and preview, Client Details, Billing Account details, Supplier Info and Output Settings. The helper sets the content height from the lowest subview, shows a vertical scrollbar only when the content overflows, and scrolls a focused off-screen control into view. List views and the update dialog's release notes show their scrollbar when they overflow. Before this, the client editor's Account row, Prefix and Month Rule were unreachable in a window shorter than about 35 lines. Verify headless at 70×16: every form shows a scrollbar, and focusing Generate, Choose…, the last supplier field and Currency scrolls each into view.

## 7. End-to-end verification

- [x] 7.1 Run `dotnet build` and `dotnet test` and verify both succeed with no failing tests.
- [x] 7.2 Against a copy of a legacy `config.toml`, start the app and verify:
  - the `DEFAULT` account appears, and saving writes `config.toml.bak`;
  - a US client with country `US`, empty VAT and a USD account generates DOCX/PDF with no VAT line and the USD bank details;
  - its KSeF XML has `BrakID`, `Adres/KodKraju` = `US`, `P_12` = `np I`, `P_13_8`, `P_18` = `1` and the USD `NrRB`, and passes `KsefSchemaValidator`;
  - a PL client's XML contains `NIP`.
