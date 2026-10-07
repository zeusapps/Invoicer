# Tasks

Apply this checklist from the repository root using `$openspec-apply-change support-invoice-tax-treatments`. Read all paths returned by `openspec instructions apply --change support-invoice-tax-treatments --json`, including the fresh-context handoff, complete fictional TOML example and acceptance matrix in `design.md`. The original conversation/research attachment and real client data are not dependencies. Missing US settings mean false; other non-EU missing settings remain unresolved. All 19 tasks start unchecked and must be verified before completion.

## 1. Shared Tax Rules And Invoice Preparation

- [x] 1.1 Introduce shared immutable tax treatment and pure resolution for domestic Poland, EU B2B services and non-EU services with an independent reverse-charge choice; verify Poland's five supported rates, Estonia, US default false, explicit non-EU true, and invalid country/rate/choice combinations in domain tests.
- [x] 1.2 Centralize monetary calculation and use it in invoice creation; add preparation checks for copied rate, current client settings, VAT and gross consistency; verify existing rounding, foreign net-equals-gross and rejection of mutated/stale invoice amounts without silently repairing them.
- [x] 1.3 Reuse buyer identification validation for all formats and add normalized seller NIP/EU VAT consistency checks; verify missing Polish/EU buyer identifiers, wrong EE prefix, seller PL/NIP mismatch, valid prefixed/unprefixed EE numbers and optional US identifiers in shared validation tests.

## 2. Configuration And Compatibility

- [x] 2.1 Add nullable `ClientConfig.ReverseCharge` and strict TOML boolean parsing; verify explicit true/false and malformed values, and ensure missing settings remain distinguishable from explicit false.
- [x] 2.2 Implement the confirmed missing-US-setting default after country inference and matching effective defaults for new/in-memory clients; persist false on the next US save, preserve explicit overrides, and retain unresolved settings for other non-EU clients; verify load/save round trips and repeated-save idempotence in `ConfigManagerTests`.
- [x] 2.3 Preserve existing country inference, billing accounts, invoice numbering and other configuration fields during migration; verify legacy US, EU, Polish, unknown-country and unresolved GB fixtures without inventing countries or replacing explicit choices.

## 3. Client Editor And Invoice Preview

- [x] 3.1 Add a plain-language resolved treatment display and non-EU Yes/No/unresolved reverse-charge control to `ClientListView`; refresh it on country/rate edits, handle category changes deliberately and keep incomplete drafts saveable; verify client switching, both non-EU choices and visibility of contradictory loaded settings in focused UI checks.
- [x] 3.2 Use shared tax resolution, monetary calculation and text in `CreateInvoiceView` preview; verify Poland's VAT total, Estonia's reverse charge, US default without reverse charge, explicit non-EU override, and actionable errors for the Poland-to-Estonia transition with a stale 23% rate.

## 4. Consistent Document And XML Output

- [x] 4.1 Extend shared invoice text with treatment label, zero foreign Polish VAT amount, normalized EU identifier display and conditional `Reverse charge / odwrotne obciążenie` annotation with Ukrainian explanation; verify shared text for the three contracts and the explicit non-EU reverse-charge case.
- [x] 4.2 Use shared tax text in PDF and DOCX, update the tax heading, wrap the NP label and preserve existing bilingual/payment content; verify generated DOCX text for numeric domestic VAT, NP labels, zero foreign VAT, annotations only when applicable and omitted empty US VAT identification.
- [x] 4.3 Adapt `TaxRateCoding` and `KsefXmlGenerator` to consume the shared treatment; verify US `np I`/`P_13_8`/`P_18=2`, explicit non-EU `P_18=1`, Estonia `np II`/`P_13_9`/`P_18=1`/seller `PL`, correct buyer identification and unchanged domestic summary pairs.

## 5. Validation Before Generation And State Changes

- [x] 5.1 Extract selected-output generation orchestration and run shared plus selected XML validation before directory creation or opening any output file; verify combined failures with missing/overprecise exchange rates, invalid identifiers and inconsistent rates leave no partial output and preserve existing sentinel files.
- [x] 5.2 Integrate the orchestration into `CreateInvoiceView`, put invoice preparation inside the handled error boundary and update numbering/output preferences only after successful generation; verify failed validation leaves both in-memory and persisted preferences/numbering unchanged, while successful generation retains existing numbering behavior.
- [x] 5.3 Enforce shared validation in standalone PDF/DOCX/XML entry points and preserve format-specific restrictions; verify direct calls reject invalid tax data before writing, PDF/DOCX without XML can proceed without an exchange rate, and mixed Polish foreign-currency requests fail before writing any selected file.

## 6. Contract Matrix, Documentation And Final Verification

- [x] 6.1 Extend the generated XML contract matrix for domestic rates 23/22/8/7/5, Estonia in PLN and EUR with prefixed/unprefixed buyer VAT, and US in PLN/USD with reverse charge false/true; provide valid seller EU VAT data to EU fixtures; verify exact fields and validate every generated XML against the existing local FA(3) schema closure with no errors or warnings.
- [x] 6.2 Retain domestic reference fixtures, default-configuration schema validity, deterministic generation with a fixed clock, optional US identification and exchange-rate precision behavior; verify their existing regression tests still pass with valid updated fixtures.
- [x] 6.3 Update README with separate Poland/Estonia/US TOML examples based on the complete fictional configuration in `design.md`, the `reverse_charge` field, migration/default behavior and meaning of numeric zero for foreign services; verify examples map to the specified treatments and explain external VIES checks and KSeF submission/recipient-visualization responsibilities without claiming XML export issues an accepted invoice.
- [x] 6.4 Generate sample PDF/DOCX/XML artifacts using the fictional configuration, fixed dates/clock, manual sample rates and acceptance matrix in `design.md` for Poland, Estonia, US without reverse charge and an explicit non-EU override; inspect actual PDFs and DOCX layouts for A4 wrapping, bilingual text, zero VAT, correct identifiers and conditional annotations; report inspection results and artifact locations without changing operational configuration or submitting invoices.
- [x] 6.5 Run `dotnet build`, `dotnet test tests/Invoicer.Tests/Invoicer.Tests.csproj` and `openspec validate support-invoice-tax-treatments --strict`; verify all required checks pass and record any operational limitations, with no changes to the vendored official schemas.

## Implementation Verification (2026-10-07)

- `dotnet build`: passed, zero warnings/errors. Full test suite: 336 passed, none skipped. Strict OpenSpec validation: passed. Official FA(3) schema files unchanged.
- Tax/configuration tests cover the three contracts, all five domestic rates, explicit foreign choices, malformed TOML, legacy country/account migration, unresolved non-EU choices, normalized required identifiers, stale copied rates/totals and midpoint rounding. Focused TUI checks cover switching clients, choice visibility, loaded contradictions and stale Poland-to-Estonia rates.
- Selected-output and workflow tests verify validation before directory/file creation, preserved sentinel files, numbering and preferences on failure, successful persistence, standalone-generator checks and document-only operation without XML exchange rates. Generated XML matrix checks exact business fields and the local schema closure. DOCX tests also validate OpenXML structure; layout work corrected missing table grids and existing formatting-element order defects.
- Samples: repository-relative `output/tax-treatment-samples/2026/Invoices/20261007_{POLAND,ESTONIA,USA,USA_RC}.{pdf,docx,xml}`. All use fictional data, date 2026-10-07, net 1000, number 1, fixed XML clock 12:00 UTC, manual EUR 4.25/USD 3.7998 rates. Each XML passes offline FA(3) validation. Output is ignored by Git and can be regenerated with the README command/test.
- Actual PDFs rendered with Windows.Data.Pdf and visually inspected; all four are one A4 page. The Ukrainian number heading fits, the NP label wraps inside the tax column, amounts/identifiers/payment details remain readable, and no content clips. Actual DOCX files independently rendered with a local Aspose.Words 26.9 evaluation tool and visually inspected; all four are one A4 page with readable tables, bilingual text and matching annotations/amounts. The tool is isolated under the ignored sample output directory and adds no application dependency. Evaluation watermarks appear only in inspection PNGs, not in the original DOCX/PDF/XML artifacts. Installed Word opened the corrected files but stalled during PDF export; its inspection-only processes were stopped.
- Poland shows 23%, VAT 230 PLN and gross 1230 PLN with no reverse-charge annotation. Estonia shows NP, VAT 0 EUR, gross 1000 EUR, complete PL/EE VAT identifiers and the bilingual reverse-charge annotation. US shows NP, VAT 0 USD and gross 1000 USD with no buyer VAT line or reverse-charge annotation; USA_RC adds only the reverse-charge wording/XML flag.
- Operational boundaries remain as specified: fictional samples are not real issued invoices; no operational configuration changed, no submission to KSeF occurred, and live VIES checks/submission/recipient QR visualization remain external responsibilities. Preflight protects against validation failures; arbitrary filesystem failures are not transactional.
