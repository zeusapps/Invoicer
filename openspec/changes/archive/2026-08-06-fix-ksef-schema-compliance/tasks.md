## 1. Vendor The FA(3) Schema Closure

- [x] 1.1 Create `tests/Invoicer.Tests/TestData/Schema/` and download the four schemas verbatim: `schemat.xsd` from `https://crd.gov.pl/wzor/2025/06/25/13775/schemat.xsd`, then `StrukturyDanych_v10-0E.xsd`, `ElementarneTypyDanych_v10-0E.xsd`, and `KodyKrajow_v10-0E.xsd` from `http://crd.gov.pl/xml/schematy/dziedzinowe/mf/2022/01/05/eD/DefinicjeTypy/`.
- [x] 1.2 Confirm the closure is complete — no `schemaLocation` in the four files points at a file that is not among them — and that `schemat.xsd` declares `targetNamespace="http://crd.gov.pl/wzor/2025/06/25/13775/"`.
- [x] 1.3 Add `TestData/Schema/README.md` recording each source URL, the retrieval date, and a note that the files are unmodified copies to be refreshed by re-downloading.
- [x] 1.4 Extend `Invoicer.Tests.csproj` to copy `TestData\Schema\*.xsd` to the output directory; the existing `TestData\*.xml` glob covers neither the extension nor the subdirectory.

## 2. Offline Schema Validation Harness

- [x] 2.1 Add a test-only `XmlResolver` that maps the `crd.gov.pl` schema URIs to the local files under `TestData/Schema/` and throws for any URI it does not recognise, so an accidental network reference fails loudly.
- [x] 2.2 Add a helper that builds and compiles an `XmlSchemaSet` from `schemat.xsd` using that resolver, and validates a document with `ValidationType.Schema` plus `XmlSchemaValidationFlags.ReportValidationWarnings`.
- [x] 2.3 Make the helper accumulate every validation error *and warning* with line and position, and fail with all of them reported rather than only the first.
- [x] 2.4 Add the validation test over freshly generated XML and confirm it FAILS against current output, naming `KodKraju` in `Podmiot1/DaneIdentyfikacyjne`. A pass at this point means the harness is not wired up — do not proceed until it fails for the right reason.

## 3. Fix The Generator

- [x] 3.1 Stop emitting `KodKraju` inside `Podmiot1/DaneIdentyfikacyjne` so seller identification is exactly `NIP` then `Nazwa`, leaving `Podmiot1/Adres/KodKraju` and the whole `Podmiot2` block untouched.
- [x] 3.2 Add an optional `TimeProvider` parameter to `KsefXmlGenerator.Generate`, defaulting to `TimeProvider.System`, and source `DataWytworzeniaFa` from `GetUtcNow()` instead of `invoice.InvoiceDate.Date`.
- [x] 3.3 Re-run the validation test from 2.4 and confirm it now passes.

## 4. Tests

- [x] 4.1 Assert that `Podmiot1/DaneIdentyfikacyjne` has exactly the child elements `NIP`, `Nazwa` in order, and that `Podmiot2/DaneIdentyfikacyjne` still emits `KodKraju` before `NrID`.
- [x] 4.2 Assert `DataWytworzeniaFa` equals a pinned instant when a fixed `TimeProvider` is supplied, and that it is independent of the invoice date.
- [x] 4.3 Add a validation test for an invoice dated before 2025-09-01, covering the schema's `minInclusive` bound on `DataWytworzeniaFa`.
- [x] 4.4 Update the determinism test to pin the clock and assert byte-identical output across two runs.
- [x] 4.5 Run the validator over the two existing `Wersja_robocza_*.xml` fixtures as an informational check; record the outcome in the change rather than asserting on it, since draft exports are not required to be valid documents.

## 5. Verify And Close Out

- [x] 5.1 Run `dotnet build` and `dotnet test`; the full suite must pass offline.
- [ ] 5.2 Generate a real invoice, confirm the XML validates, and import it into the KSeF Taxpayer Application to confirm the original rejection is gone.
  - Validation half done: the rejected invoice `2026/EL/0009` was validated as submitted and with the fix applied — 1 error (`KodKraju` in `Podmiot1/DaneIdentyfikacyjne`) before, 0 errors and 0 warnings after, across the real NIP, IBAN, SWIFT, amounts, and address. The midnight `DataWytworzeniaFa` in that file is schema-valid, confirming it was never the cause.
  - Import half is the user's to run; it needs the KSeF Taxpayer Application and cannot be automated here.
- [x] 5.3 Regenerate any previously produced XML that has not yet been submitted, including `output/2026/Invoices/20260511_SAMPLE_PL.xml`.
  - Regenerated from the default configuration and validated: 0 errors, 0 warnings. Real invoices live beside the user's own binary with their own `config.toml`, which is not in this repo, so those must be regenerated there.
- [x] 5.4 Note in the change whether the two open questions from `design.md` — value-level validation of config data, and runtime versus test-time validation — should become a follow-up change.

## 6. Default Configuration (Added During Implementation)

Regenerating the sample output surfaced a second, independent defect: the placeholder `Tin = "0000000000"`
in `ConfigManager.CreateDefault` fails the schema type `TNrNIP`, whose pattern
`[1-9]((\d[1-9])|([1-9]\d))\d{7}` forbids a leading zero. A first-run user could not produce a valid
invoice even after the structural fix. This is the value-level failure class the design predicted under
Risks, found in the repository's own sample data.

- [x] 6.1 Replace the default supplier `Tin`/`Vat` and sample client `Vat` with schema-valid placeholders, matching the identifiers already used by the reference fixtures.
- [x] 6.2 Add a test generating from `ConfigManager.CreateDefault()` and validating the result, so the shipped defaults cannot silently regress.
- [x] 6.3 Apply the same placeholders to `publish/config.toml`.
