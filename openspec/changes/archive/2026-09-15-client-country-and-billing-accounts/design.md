## Context

See proposal.md for motivation and specs/ for required behavior.

Current state that shapes the approach:

- **Buyer identification.** `KsefXmlGenerator.KsefInvoiceDocument.FromInvoice` builds the buyer `KsefParty` from `ExtractCountryCode(Client.Vat)`, which falls back to `"PL"`. `WriteParty` always writes the `KodKraju?` + `NrID` form for the buyer.
- **Bank details.** They live on `SupplierConfig` (`Iban`, `Bank`, `Swift`) and are read directly by `DocxGenerator`, `PdfGenerator` and `KsefXmlGenerator`. `SettingsView` edits them as supplier fields.
- **Config format.** `ConfigManager` hand-writes TOML: `[supplier]`, `[output]`, `[update]`, then `[[clients]]`. It reads the file tolerantly with defaults, which is how earlier fields such as `enabled` and `[update]` were added without a version marker.
- **Invoice creation.** `Invoice.Create(client, supplier, output, ...)` is the single construction point, and `CreateInvoiceView.OnGenerate` calls each generator in turn. A generator failing part-way leaves earlier files written.
- **Schema.** The vendored FA(3) schema defines:
  - `TKodKraju`: all ISO country codes;
  - `TKodyKrajowUE`: the 27 EU VAT prefixes plus `XI`, with Greece as `EL`, not `GR`;
  - `TNrVatUE`: `(\d|[A-Z]|\+|\*){1,12}`;
  - `TNrNIP`, and the `TRachunekBankowy` order `NrRB`, `SWIFT`, `RachunekWlasnyBanku`, `NazwaBanku`.
- **Reference fixtures.** The fixtures `Wersja_robocza_*.xml` identify a Polish buyer as `NrID` = `PL9999999999`. `Generate_MatchesReferenceFixtures_ForKeyValues` asserts that `NrID` matches.

## Goals / Non-Goals

**Goals:**
- Choose the buyer identification form in a single pure function, shared by validation and XML writing, so the two can never disagree.
- Resolve a client's billing account once, before any generator runs, so a bad reference fails all formats together.
- Keep existing `config.toml` files loading without manual editing.

**Non-Goals:**
- Northern Ireland (`XI`) buyers. A GB client is treated as non-EU (`KodKraju` + `NrID`). XI-prefixed VAT numbers are not specially handled.
- Validating VAT numbers against checksums or the VIES service.
- Picking accounts automatically by currency, or overriding the account per invoice.
- Changing the seller (`Podmiot1`) block or the output filename pattern. The `_PL` suffix in the default filename is not country-derived and stays as configured.
- A config version number or general migration framework.
- Domestic zero-rate and exemption codes (`0 KR`, `zw`, `oo`), the 4%/3% flat rates and OSS. A Polish client needing any of these gets a validation error instead of a wrong code.
- Exchange-rate data (`KursWaluty`, `P_14_xW`) for Polish-rated invoices issued in a foreign currency. Behavior there is unchanged from today.

## Decisions

### D1. Country reference data lives in one static class, checked against the schema by a test

A `Countries` helper exposes:
- `IsKnown(code)`, backed by the ISO codes allowed by `TKodKraju`;
- `IsEu(code)`;
- `EuVatPrefix(code)`, where `GR` becomes `EL`;
- `FromEuVatPrefix(prefix)`, where `EL` becomes `GR` and `XI` returns nothing.

The lists are hardcoded. A test reads `TKodKraju` and `TKodyKrajowUE` from the vendored XSDs and asserts that they are equal to the hardcoded lists. The comparison ignores `XI` and applies the `EL`/`GR` swap.

*Alternatives:*
- **Parse the XSD at runtime.** Rejected: the schema is test data and is not shipped in the single-file publish.
- **`System.Globalization.RegionInfo`.** Rejected: it doesn't match the schema's code set exactly and depends on the OS culture data.

### D2. Buyer identification is a closed set of record types resolved by a pure function

```
BuyerIdentification.Resolve(country, vat) ->
    Nip(value)                     country == PL
  | EuVat(kodUE, nrVatUE)          IsEu(country)
  | ForeignId(kodKraju, nrId)      !IsEu && vat non-empty
  | NoId                           !IsEu && vat empty
```

The steps:

1. **Normalize the VAT.** Remove whitespace and upper-case letters.
2. **Polish client.** Strip a leading `PL`.
3. **Other EU client.** Strip a leading two-letter prefix only if it equals `EuVatPrefix(country)`. Any other letter prefix is a validation error.
4. **Non-EU client.** `NrID` keeps the VAT exactly as entered, only trimmed.

`Validate` calls `Resolve` and turns failures into messages:
- missing or unknown country;
- missing VAT for PL/EU;
- a Polish value that doesn't match `TNrNIP`;
- a prefix that doesn't match the country;
- an `NrVatUE` that doesn't match `TNrVatUE`.

`KsefParty` replaces `IdentifierCountryCode`/`Nip`/`Identifier` with this identification value. `WriteParty` switches on it. Only the buyer uses the variants; the seller keeps writing `NIP` + `Nazwa`.

*Alternatives:*
- **Keep nullable fields and emit whichever are set.** Rejected: this is what allowed the invalid `NrID="N/A"` and PL fallback. The discriminated shape makes illegal combinations unrepresentable.

### D3. Billing account is resolved in `Invoice.Create` and carried on `Invoice`

- **New model.** `BillingAccountConfig { Key, Label, Iban, Bank, Swift, Currency }`.
- **Config.** `AppConfig.BillingAccounts` is a `List<BillingAccountConfig>`, and `ClientConfig.BillingAccount` is a string key.
- **Construction.** `Invoice.Create` takes the resolved `BillingAccountConfig` as a new required parameter and sets it on `Invoice.BillingAccount`.
- **Lookup.** `AppConfig.ResolveBillingAccount(client)` does the lookup. It throws `BillingAccountNotFoundException(clientKey, accountKey)` for an empty or unknown key.
- **Create Invoice screen.** `CreateInvoiceView.OnGenerate` resolves the account before calling `Invoice.Create`. It shows the exception as an error dialog, so no generator runs. The preview uses the same lookup, but without throwing.
- **Generators.** All three read `invoice.BillingAccount` instead of `invoice.Supplier.Iban`/`Bank`/`Swift`. The KSeF validation additionally requires a non-empty IBAN.

*Alternatives:*
- **Pass `AppConfig` into generators.** Rejected: generators are currently config-agnostic and tests build `Invoice` directly.
- **Each generator validates the account.** Rejected: that duplicates the check and allows partial output.

### D4. Config shape and in-memory migration in `ConfigManager.FromTomlTable`

Written order: `[supplier]` (without bank keys), `[[billing_accounts]]`, `[output]`, `[update]`, `[[clients]]`.

| Account key | TOML key |
|---|---|
| `Key` | `key` |
| `Label` | `label` |
| `Iban` | `iban` |
| `Bank` | `bank` |
| `Swift` | `swift` |
| `Currency` | `currency` |

Clients gain `country` and `billing_account`.

Migration runs after parsing, entirely in memory:

1. **Billing accounts.** If there are no accounts and the legacy `supplier.iban`/`bank`/`swift` in the raw table are non-empty, add a `DEFAULT` account and assign it to every client whose `billing_account` is empty.
2. **Client country.** For each client with an empty `country`, set it to `Countries.FromEuVatPrefix(first two letters of VAT)` when that returns a value.

`SupplierConfig` loses `Iban`/`Bank`/`Swift`, so the legacy values are read from the raw `TomlTable` only during migration. `Save` always writes the new shape.

*Alternatives:*
- **Keep bank fields on `SupplierConfig` as a fallback.** Rejected: that silent fallback is exactly what the specs forbid.
- **Rewrite the file immediately on load.** Rejected: loading has never written except on first run. The existing pattern, as with `[update]`, is to materialize changes on the next save.

### D5. Back up the config before the first save of a migrated file

Downgrading to an older Invoicer after saving would lose the bank details, because older versions read them only from `[supplier]`. When `FromTomlTable` performed the billing-account migration, it marks the config as migrated. The next `Save` then copies the existing file to `config.toml.bak` once before overwriting it.

*Alternatives:*
- **No backup.** Rejected: the file holds the user's real banking data and client numbering (`last_invoice_number`).

### D6. TUI

- **Billing Accounts screen.**
  - `BillingAccountListView` copies the list + detail layout of `ClientListView`: fields Key, Label, IBAN, Bank, SWIFT, Currency, with Add / Delete / Save All buttons.
  - Menu entry: `Settings > _Billing Accounts`.
  - **Key renames.** Each row remembers the key it was loaded with. On Save All, the view first rejects empty or duplicate keys. It then rewrites `client.BillingAccount` for every changed key, and only then saves.
  - **Delete.** Checks `_config.Clients` for references and refuses with their keys listed.
- **Client editor.**
  - `ClientListView` gains a `Country:` text field.
  - The `Account:` row is a read-only label plus a `Choose…` button. The button opens a small dialog with a `ListView` of `"{Key} – {Label} ({Currency})"`.
  - A warning label under the account row shows the currency mismatch and updates on load and after choosing.
  - The dialog is chosen over a `ComboBox`, which is awkward in Terminal.Gui v2 inside a scrolling detail frame. It also matches the existing `MessageBox`/dialog usage.
  - `OnAddClient` assigns the first defined account, if any, and an empty country.
- **Supplier Info.** `SettingsView` drops the IBAN, Bank and SWIFT rows.
- **Create Invoice preview.** A new line, `Account: {Label} {Iban}`, or `Account: (none assigned)`.

### D7. DOCX/PDF customer block

The customer text is built from a list of lines, and the `VAT: …` line is included only when `Client.Vat` is not blank. The bank block uses `invoice.BillingAccount`. The layout doesn't change otherwise.

### D9. Tax-rate coding is a second pure function beside buyer identification

Implementation surfaced a second defect in the same code path. `WriteInvoiceBody` wrote every invoice as a 23% invoice: the net amount in `P_13_1` and the VAT in `P_14_1`. `P_12` was the integer rate, so a 0% client produced `P_12 = 0`, which `TStawkaPodatku` rejects.

The correct coding was checked against the MF "Broszura informacyjna dotycząca struktury logicznej FA(3)" (04.03.2026, pp. 46–55, 92) and the XSD documentation:
- **`np I`:** services outside Poland other than art. 100 ust. 1 pkt 4. Summary field `P_13_8`.
- **`np II`:** art. 100 ust. 1 pkt 4 services to EU VAT-identified buyers. Summary field `P_13_9`.
- **`oo`:** limited to domestic reverse charge. Its pair is `P_13_10`.
- **`P_18`:** its definition covers buyers liable for "VAT or a similar tax", so it is `1` for both foreign cases. For US buyers the user chose the conservative, prevailing reading.

`TaxRateCoding.Resolve(country, vatRate)` returns either an error or a record with:
- `RateCode`, the `P_12` value;
- `SummaryIndex`, one of 1, 2, 3, 8 or 9, which selects `P_13_n`;
- `HasTaxAmount`, which is true only for indices 1–3 and selects `P_14_n`;
- `ReverseCharge`, the `P_18` value;
- `SellerEuPrefix`, `"PL"` for `np II` and otherwise null.

`Validate` reports its error next to the identification error, and `WriteInvoiceBody` and `WriteParty` consume the record. The EU/non-EU split uses `Countries.IsEu`, the same helper as D2, so identification and rate coding can never disagree about a client.

*Alternatives:*
- **An explicit per-client rate code.** Rejected by the user. For the foreign cases the code follows from the country by statute, and a free choice would be easy to set wrong.
- **Deriving from the client's VAT prefix.** Rejected for the same reason as D2.

### D8. Test updates

- **Reference fixture test.** `Generate_MatchesReferenceFixtures_ForKeyValues` compares the fixture's buyer `NrID`, with the `PL` prefix stripped, to the generated `NIP`. The fixtures were drafts made before this change, and their key values (number, date, totals, seller NIP) stay authoritative.
- **Buyer identification test.** `Generate_KeepsBuyerIdentificationCountryCode` is replaced by a theory covering PL, DE, GR, US with ID and US without ID. Each case asserts element names and values and validates against the schema.
- **Other fixtures.** Test fixtures that set `Supplier.Iban`/`Swift` move to a `BillingAccountConfig`.

## Risks / Trade-offs

- **Polish buyers now receive `NIP` instead of `KodKraju` + `NrID`.** KSeF delivers the invoice to the buyer's KSeF account only when it is identified by NIP, so this is the intended correct behavior. It is still a visible change from previously submitted invoices. → Called out as BREAKING in the proposal. It is covered by schema validation and explicit tests.
- **Existing clients whose VAT has no EU prefix end up with an empty country.** GreenFlow is one such client. → KSeF generation fails with "client country is required" until the user sets it. DOCX/PDF are unaffected. This is deliberate, replacing the silent wrong `PL`.
- **Clients storing a placeholder such as `N/A` as VAT keep that value.** For a US client it would be emitted as `NrID` = `N/A`. → No automatic cleanup; guessing which strings are placeholders is unreliable. The release notes and the tasks mention clearing it. The user's config is small.
- **Hardcoded country lists can drift from a future schema version.** → The D1 test fails when the vendored schema is updated.
- **Downgrading after migration loses bank details.** → The `config.toml.bak` written once before the first migrated save (D5).
- **Renaming an account key in one view while clients are edited in another** could leave stale references. Both views edit the same in-memory `AppConfig`, and generation fails loudly on an unknown key. → Acceptable.

## Migration Plan

1. The user installs the new version. On first load, a `DEFAULT` account is created from the existing supplier bank fields and assigned to all clients. Countries are inferred from EU VAT prefixes.
2. The first save writes `config.toml.bak`, then the new shape.
3. The user adds the second account in Settings > Billing Accounts. For GreenFlow they set `country = US`, clear VAT and assign the USD account.
4. **Rollback:** restore `config.toml.bak` and reinstall the previous version.
