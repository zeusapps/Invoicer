# Proposal

## Why

Switching a Polish B2B IT-services contract to an Estonian customer requires an invoice without Polish VAT and with explicit reverse-charge wording. Today PDF/DOCX show only `N/A`, while KSeF assumes reverse charge for every foreign customer and validates tax data after customer documents have already been written.

## What Changes

- Resolve a shared invoice tax treatment for domestic Polish VAT, EU B2B reverse charge, and services outside Poland without reverse charge. Keep the existing numeric `vat_rate`, with `0` representing no Polish VAT charged for the supported foreign-service cases, not an ordinary zero-rated or exempt sale.
- Make reverse charge configurable separately for non-EU customers. Existing and new US clients without a setting use no reverse charge, as confirmed for this contract; explicit choices remain authoritative. Other non-EU clients without a choice require one before generation.
- Show the resolved treatment in the client editor and invoice preview. Render `NP - not subject to Polish VAT`, a zero Polish VAT amount, and reverse-charge wording only when applicable in both PDF and DOCX.
- Validate country, VAT identifiers, tax treatment and totals for every format before writing files. Run additional XML checks before any output when XML is selected.
- Retain Estonia's existing FA(3) mapping (`np II`, `P_13_9`, `P_18=1`, seller prefix `PL`), and emit `P_18=2` for the confirmed US configuration (`np I`, `P_13_8`).
- Add semantic, document-content, configuration migration, generation-preflight and FA(3) schema tests for Poland, Estonia and both non-EU reverse-charge choices. Document the three contract configurations.
- **BREAKING:** PDF/DOCX generation will reject invalid or incomplete tax data, including missing Polish/EU VAT identifiers; existing US XML changes from the automatic `P_18=1` to `P_18=2` unless reverse charge is explicitly enabled. Other non-EU clients need an explicit choice instead of the old automatic assumption.

## Capabilities

### New Capabilities

- `invoice-tax-treatment`: Shared tax resolution, non-EU reverse-charge configuration and migration, consistent customer-document text, preview, totals and preflight validation.

### Modified Capabilities

- `client-country`: Require valid Polish/EU buyer VAT identifiers for all invoice formats while retaining optional identification for non-EU buyers.
- `ksef-xml-generation`: Consume the shared treatment, make non-EU `P_18` conditional, and validate selected XML output before producing any customer documents.
- `exchange-rate`: Clarify that an unavailable rate leaves document-only generation available, while a selected XML output must pass rate validation before any files are written.

## Impact

Changes affect `ClientConfig`, `Invoice`, TOML load/save, `TaxRateCoding`, `BuyerIdentification`, `InvoiceText`, PDF/DOCX/XML generators, client editing and invoice creation, nearby tests, and README configuration guidance. No new runtime package or external service is required.

Scope is ordinary B2B services under the general place-of-supply rule, with Poland's existing supported domestic VAT rates. This change does not determine VIES registration, US state tax liability, or applicability of special service rules. It does not add KSeF submission, UPO retrieval, KSeF-number/QR visualization, domestic zero-rated/exempt sales, goods, or corrections to previously issued invoices. XML export alone is not submission; the existing external workflow remains responsible for KSeF acceptance and the recipient visualization after submission.

## Applying From A Fresh Context

This change contains the implementation decisions and acceptance criteria; the original conversation and research attachment are not prerequisites. Read the four delta specifications, `design.md` (including its fresh-context handoff and sample configuration), and `tasks.md`. Start from the repository root with `$openspec-apply-change support-invoice-tax-treatments`. All 19 tasks are initially unchecked: completed planning artifacts do not mean the implementation is complete. Implement and verify the software with fictional data; production client setup and actual invoice issuance require actual party/contract information and are separate from completing these tasks.
