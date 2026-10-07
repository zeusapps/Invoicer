# Design

## Context

See `proposal.md` for the motivation and scope, and the four delta specifications for the behavior contract. This design is required because tax resolution crosses configuration, mutable invoice data, TUI preview, three generators and generation preflight.

Observed integration points:

- `ClientConfig` holds `Country` and an integer `VatRate`, but no reverse-charge choice. `ConfigManager` loads/saves TOML and already infers a missing country from an EU VAT prefix.
- `Invoice.Create` calculates amounts from the numeric rate. `CreateInvoiceView.UpdatePreview` duplicates that calculation and treats a zero rate as `N/A`.
- `TaxRateCoding.Resolve` contains correct domestic/EU/non-EU FA(3) mappings, but unconditionally returns reverse charge for every foreign client. It is currently XML-specific.
- `BuyerIdentification.Resolve` already validates country and buyer identifier syntax. That validation runs only in XML generation.
- `InvoiceText` shares customer and bank text, while PDF and DOCX duplicate tax labels, amounts and supplier text.
- `CreateInvoiceView.OnGenerate` mutates output preferences, constructs the invoice, then writes DOCX/PDF before XML validation runs. Numbering is saved after generation succeeds.
- Existing tests validate XML against an unmodified, locally vendored official FA(3) schema closure. EU tests currently use a seller with an empty EU VAT identifier and must be updated to valid party data.

Optional provenance for the decisions below (not implementation prerequisites):

- The original research attachment described ordinary B2B IT services from a Polish business to an Estonian VAT-registered company. Its relevant conclusions are recorded here: no Polish VAT charged, explicit reverse-charge wording, PL/EE EU VAT identifiers, and FA(3) `np II`. The attachment itself is not needed to apply the change.
- [MF FA(3) brochure, March 2026](https://ksef.podatki.gov.pl/media/jknpcymf/broszura-informacyjna-dotyczaca-struktury-logicznej-fa-3-04032026.pdf): printed pages 51-52 define the foreign-service net summaries; page 55 describes buyer tax liability for `P_18`; pages 92-93 define NP codes.
- [Estonian Tax and Customs Board](https://www.emta.ee/en/business-client/taxes-and-payment/value-added-tax/calculation-and-refund-vat/taxable-transactions-and-acts): recipient accounting for acquired foreign services.

The user confirmed that the existing US contract should use services outside Polish VAT without reverse charge. This is the migration choice for that contract, not a rule asserting that every conceivable US transaction has identical tax consequences.

## Fresh-Context Implementation Handoff

### Entry Point And Required Context

Apply this change in the repository containing `Invoicer.slnx`; no previous chat, personal download, registered OpenSpec store, tax-service credentials or real customer configuration is required. The project targets .NET 9 and Windows (`win-x64`) and uses Terminal.Gui, Tomlyn, QuestPDF and DocumentFormat.OpenXml. Package versions in `src/Invoicer/Invoicer.csproj` are authoritative; use the existing dependencies and embedded fonts.

Start a fresh session with:

```text
$openspec-apply-change support-invoice-tax-treatments
```

The CLI context can also be inspected from the repository root:

```powershell
openspec status --change support-invoice-tax-treatments --json
openspec instructions apply --change support-invoice-tax-treatments --json
```

Read every file listed in `contextFiles`: proposal, this design, all four delta specifications and tasks. The apply response must be ready and expose the implementation checklist. At handoff it reports 0 of 19 tasks complete. A planning status of 4/4 artifacts complete describes file availability, not implemented behavior. Consult relevant existing main specifications for retained behavior; the deltas explicitly replace the changed requirements.

### Recorded Decisions And Boundaries

These are settled implementation inputs, not questions to reopen merely because the conversation is unavailable:

1. The supplier is a Polish business invoicing ordinary B2B services. The supported Estonia case is services supplied to the Estonian VAT-registered business under the general rule; special place-of-supply cases and B2C are outside this change.
2. Preserve domestic numeric VAT, including the existing supported rates 23, 22, 8, 7 and 5. Do not add Polish 0%-rate or exempt regimes.
3. Estonia uses EU reverse charge with no Polish VAT added. The customer accounts for local VAT; the software does not add an Estonian VAT percentage or simulate its tax return.
4. The US contract uses no Polish VAT and no reverse-charge wording. Missing US choices default to false and are persisted on save. Explicit non-EU true/false choices remain authoritative; other non-EU countries with no choice remain unresolved until an operator chooses.
5. An internal numeric zero for foreign services means no Polish VAT charged. NP classification and reverse-charge responsibility are distinct. Use the NP label and zero monetary VAT amount; do not display 0%, VAT-exempt or N/A tax cells.
6. Every selected output must pass shared validation. XML-specific checks are additionally required only if XML is selected. A combined request must not write any file before those checks pass.
7. Required EU identifiers are checked for syntax and seller consistency, not live registration. No VIES API, country-specific VAT checksum service or network lookup is required for implementation. Do not add checksum enforcement beyond the specified existing format rules.
8. KSeF submission, accepted invoice numbers, QR visualization and correction/reissuance of previous invoices are excluded. Complete the software/tests and fictional samples without modifying operational client details or submitting invoices.

All implementation-critical decisions are captured. Real supplier/buyer details and proof of active registration are needed to use the application for an actual invoice, not to implement or verify the change. Do not infer those details from a published binary's configuration or replace an existing Polish client with a fabricated Estonian entity.

### Repository Integration Map

| Area | Existing files to inspect |
| --- | --- |
| Domain and country reference data | `src/Invoicer/Models/ClientConfig.cs`, `src/Invoicer/Models/Invoice.cs`, `src/Invoicer/Models/Countries.cs`, `src/Invoicer/Models/SupplierConfig.cs` |
| Configuration and account resolution | `src/Invoicer/Config/ConfigManager.cs`, `src/Invoicer/Models/AppConfig.cs`, `src/Invoicer/Models/BillingAccountRules.cs` |
| Tax and party mapping | `src/Invoicer/Generation/TaxRateCoding.cs`, `src/Invoicer/Generation/BuyerIdentification.cs`, `src/Invoicer/Generation/KsefXmlGenerator.cs` |
| Shared text and customer documents | `src/Invoicer/Generation/InvoiceText.cs`, `src/Invoicer/Generation/DocxGenerator.cs`, `src/Invoicer/Generation/PdfGenerator.cs` |
| Editor, preview and generation | `src/Invoicer/Tui/Views/ClientListView.cs`, `src/Invoicer/Tui/Views/CreateInvoiceView.cs` |
| Test entry points | `tests/Invoicer.Tests/TaxRateCodingTests.cs`, `tests/Invoicer.Tests/BuyerIdentificationTests.cs`, `tests/Invoicer.Tests/ConfigManagerTests.cs`, `tests/Invoicer.Tests/InvoiceDocumentTextTests.cs`, `tests/Invoicer.Tests/InvoiceOutputBehaviorTests.cs`, `tests/Invoicer.Tests/KsefXmlGeneratorTests.cs`, `tests/Invoicer.Tests/KsefExchangeRateTests.cs` |
| Offline schema validation | `tests/Invoicer.Tests/KsefSchemaValidator.cs`, `tests/Invoicer.Tests/TestData/Schema/README.md` and its XSD files |

All paths are relative to the repository root. Add shared domain/preparation and orchestration files as described in Decisions; new class names are an implementation choice. Do not replace the official XSDs to make generated XML pass. Preserve `http://crd.gov.pl/wzor/2025/06/25/13775/`, FA(3) version metadata, the injected XML clock and recorded exchange-rate precision.

### Self-Contained Sample Configuration

The following is a complete sample shape for tests/documentation after implementation. All identities, account details and names are fictional format-valid placeholders, not verified tax registrations or payment instructions. Use it in an isolated test or sample directory, never as the user's operational configuration. `ConfigManager.ConfigPath` supports a temporary path; restore it after configuration tests, as existing tests do.

```toml
[supplier]
name = "Example Polish Supplier"
name_ua = "Тестовий польський постачальник"
tin = "1111111111"
vat = "PL1111111111"
regon = ""
address = "1 Example Street, 00-001 Warsaw, Poland"
address_ua = "Тестова адреса, Варшава, Польща"

[[billing_accounts]]
key = "SAMPLE"
label = "Fictional sample account"
iban = "PL00102010260000004270201111"
bank = "Example Bank"
swift = ""
currency = ""

[output]
directory = "./output/tax-treatment-samples"
pattern = "{year}/Invoices"
filename = "{date}_{client}"
generate_docx_by_default = true
generate_pdf_by_default = true
generate_xml_by_default = true

[[clients]]
key = "POLAND"
name = "Example Polish Buyer"
name_ua = "Тестовий польський замовник"
address = "2 Example Street, 00-001 Warsaw, Poland"
address_ua = "Тестова адреса, Варшава, Польща"
country = "PL"
vat = "PL9999999999"
billing_account = "SAMPLE"
currency = "PLN"
vat_rate = 23
service_description = "B2B IT services"
service_description_ua = "ІТ-послуги для бізнесу"
invoice_prefix = "PL"
default_amount = 1000.00
month_offset_rule = "early_previous"
last_invoice_number = 0
enabled = true

[[clients]]
key = "ESTONIA"
name = "Example Estonian Buyer"
name_ua = "Тестовий естонський замовник"
address = "3 Example Street, Tallinn, Estonia"
address_ua = "Тестова адреса, Таллінн, Естонія"
country = "EE"
vat = "EE123456789"
billing_account = "SAMPLE"
currency = "EUR"
vat_rate = 0
service_description = "B2B IT services"
service_description_ua = "ІТ-послуги для бізнесу"
invoice_prefix = "EE"
default_amount = 1000.00
month_offset_rule = "early_previous"
last_invoice_number = 0
enabled = true

[[clients]]
key = "USA"
name = "Example US Buyer"
name_ua = "Тестовий замовник зі США"
address = "4 Example Street, Boston, USA"
address_ua = "Тестова адреса, Бостон, США"
country = "US"
vat = ""
billing_account = "SAMPLE"
currency = "USD"
vat_rate = 0
reverse_charge = false
service_description = "B2B IT services"
service_description_ua = "ІТ-послуги для бізнесу"
invoice_prefix = "US"
default_amount = 1000.00
month_offset_rule = "early_previous"
last_invoice_number = 0
enabled = true
```

For deterministic sample generation, use invoice date 2026-10-07, invoice number 1 per client, net 1000, and a fixed XML generation instant of 2026-10-07T12:00:00Z. Supply manual illustrative rates `4.25` for EUR and `3.7998` for USD when XML is selected; leave rate source/date/table metadata unset because these sample rates are not attributed to an NBP lookup. These values are test inputs, not current exchange-rate advice. Rates and their PLN equivalents do not appear in customer documents.

### Acceptance Matrix And Edge Cases

| Sample | Polish VAT / total in invoice currency | `P_12` | Net summary | `P_18` | Seller EU prefix | Buyer identification | Reverse-charge wording |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Poland, 23%, PLN | 230 / 1230 | `23` | `P_13_1=1000`, `P_14_1=230` | `2` | Absent | `NIP=9999999999` | Absent |
| Estonia, EUR | 0 / 1000 | `np II` | `P_13_9=1000`, no `P_14_x` | `1` | `PL` | `KodUE=EE`, `NrVatUE=123456789` | Present |
| US, USD, false | 0 / 1000 | `np I` | `P_13_8=1000`, no `P_14_x` | `2` | Absent | `BrakID=1` | Absent |
| US, USD, explicit true | 0 / 1000 | `np I` | `P_13_8=1000`, no `P_14_x` | `1` | Absent | `BrakID=1` | Present |

For the fourth case, clone the USA test client with a distinct key/prefix (for example `USA_RC`/`USRC`) and set true; no actual US liability is being asserted. All cases use seller `NIP=1111111111`, buyer address country matching the sample country and `P_15` equal to the shown total. Foreign-currency XML has the supplied `FaWiersz/KursWaluty`; PLN XML omits it.

Build tests for the following variants from this configuration:

- Remove the US key to exercise its false default and next-save persistence; keep explicit true to verify it is not overwritten. Change country to `GB` and omit the setting to exercise unresolved non-EU behavior.
- Store Estonia's number as `123456789` or with whitespace/lower-case prefix; customer documents display the complete normalized `EE` identifier, XML still splits it.
- Give Estonia `vat_rate = 23`, no buyer VAT, a `DE` prefix, a missing/mismatched seller EU VAT, or explicit false; each request fails before writing.
- Mutate invoice totals or copied rate after preparation and verify revalidation catches contradictions.
- Test mixed output with absent rate and with `0.00021425` (more than six meaningful decimal places); existing files, numbering and saved preferences survive failure. The valid foreign PDF/DOCX-only request still succeeds without a rate.
- Change the Polish rate to each supported rate and retain the corresponding summary pair. Preserve the existing two-decimal midpoint rounding (`Math.Round` defaults to `MidpointRounding.ToEven`). Do not introduce a new rounding policy.

Customer documents share `NP - not subject to Polish VAT`, zero monetary VAT and the exact conditional annotation `Reverse charge / odwrotne obciążenie`. Add a suitable Ukrainian explanation in both layouts (for example `Не підлягає оподаткуванню ПДВ у Польщі` and `Зворотне нарахування ПДВ`), using the existing embedded Lato fonts. The specs require the meaning and preserved bilingual layout; these Ukrainian examples do not require an additional language consultation.

### Completion Evidence

Run the build, complete test suite and strict OpenSpec validation listed in tasks. Schema validation uses the local XSD closure and requires neither the original PDF nor a live KSeF session. Inspect the actual sample PDF/DOCX artifacts for wrapping, identifier text, zero VAT and conditional annotations. Report sample paths and inspection results in the apply completion report, and only then mark the corresponding verification tasks complete. If external PDF rendering tooling is needed for inspection, that is a local verification aid, not an application runtime dependency.

All 19 tasks remain required. This handoff does not authorize dropping tests, converting unresolved non-EU settings to false, adding tax regimes, or declaring schema validation equivalent to legal issuance. An unexpected repository or tooling failure is a new implementation finding, not a missing conversational decision.

## Goals / Non-Goals

**Goals:** Centralize tax semantics independently of XML field names, preserve existing numeric rate configuration, and make invalid selected-output requests fail before filesystem or numbering changes. Keep output naming, billing-account assignment, invoice numbering rules and NBP exchange-rate precision intact.

**Non-Goals:** A general tax engine or automated legal determination. The resolver supports the service cases in the specifications; the operator establishes that the business transaction qualifies. Runtime schema validation, new tax regimes, file-system transactions for arbitrary I/O failures, and remote registration/submission are not necessary for this design. The preflight guarantee concerns validation failures, not rollback after a disk or renderer failure.

## Decisions

### 1. Separate shared tax semantics from FA(3) coding

Introduce a pure shared resolver in the domain layer, returning an immutable treatment record or actionable errors. Its inputs are normalized country, rate and nullable non-EU reverse-charge choice. Treatment distinguishes domestic VAT, EU B2B services and non-EU services; reverse charge is a separate resolved boolean. Unknown countries must fail before being interpreted as non-EU.

Keep XML codes and summary indices in `TaxRateCoding`, but map them from the resolved treatment rather than independently guessing country liability. Keep a single shared monetary calculation using the existing `Math.Round(net * rate / 100m, 2)` rule; foreign services have zero Polish VAT and gross equal to net. `Invoice.Create` and preview use this calculation rather than duplicate it.

Preparation/validation must compare invoice amounts and copied `VatRate` with current tax inputs, rejecting stale or manually contradictory invoice data. Because existing `Invoice` and client models are mutable, an immutable result prepared for a generation request should be passed through the orchestration to its renderers; direct generator wrappers prepare and validate independently. Do not rely on a cached treatment that survives a client/rate edit.

Alternatives: Adding Estonia checks independently to each renderer would repeat the current drift. A complete persisted tax-regime enum would duplicate country-driven distinctions and require a wider migration; it is unnecessary for the supported service cases.

### 2. Persist only the independent non-EU choice

Add nullable `ClientConfig.ReverseCharge` and TOML `reverse_charge`. Null distinguishes an unresolved setting from explicit false. Parse only TOML booleans, preserving errors for malformed values. Apply compatibility defaults after the existing country inference:

| Country | Missing choice | Explicit choice |
| --- | --- | --- |
| `PL` | Derived false | False accepted; true rejected |
| Other EU, including `EE` | Derived true | True accepted; false rejected |
| `US` | False, persisted on next save | Either value preserved |
| Other non-EU | Unresolved until chosen | Either value preserved |
| Missing/unknown | Unresolved country error | Never substitutes a country |

Use the same effective-choice logic for loaded configuration and new/in-memory clients so behavior does not depend on calling `ConfigManager.Load`. Omit unresolved null values on save. Do not materialize guessed choices for countries other than the confirmed US default. Retain explicit matching Polish/EU values if entered in TOML; contradiction is reported during generation. The editor normally displays the derived treatment and does not offer an override for those categories.

Country edits must refresh the controls and avoid carrying a non-EU override into a derived category. When the operator changes categories in the editor, clear the no-longer-applicable override; do not silently clear a contradictory value loaded from TOML simply by viewing it. A change from Poland to Estonia must leave a stale 23% rate visibly invalid until corrected.

Alternative: Preserve automatic reverse charge for all legacy non-EU clients. Rejected because the US setting was explicitly corrected and liability must not be inferred from merely being foreign.

### 3. Reuse identification validation across formats

Move or expose the existing buyer-identification rules for shared validation instead of maintaining another regex set. Normalize seller NIP and EU VAT identifiers consistently; for EU treatment require the seller VAT identifier to be `PL` plus the same NIP. Apply existing format constraints, without claiming checksum or active VIES registration checks that the code does not perform.

Render complete prefixed EU buyer identifiers from their normalized identification; XML continues to use separate `KodUE` and unprefixed `NrVatUE`. Retain optional non-EU identification and omission of empty customer VAT lines. Supplier and buyer identifiers shown on customer documents must agree with those used in XML.

Alternative: Validate party identifiers only for XML. Rejected because selecting PDF-only should not permit issuing an incomplete EU reverse-charge invoice.

### 4. Share document tax text and retain bilingual layouts

Extend shared invoice text helpers with tax label, tax amount and conditional annotation, and use them in DOCX/PDF and the preview. Change the tax column heading to accommodate NP as well as percentages. Foreign-service tax cells show the NP label and `0.00` in invoice currency; the footer carries `Reverse charge / odwrotne obciążenie` only when resolved true, accompanied by a Ukrainian explanation. Wrap the longer NP label and adjust the column proportions if necessary so the table remains legible on A4.

Domestic layout keeps its rate and amount. The presence of an exchange rate does not add text to PDF/DOCX, preserving the `exchange-rate` capability. A zero displayed Polish VAT amount is a monetary amount, not a zero-percent rate or a claim that the recipient owes no local tax.

Alternative: Add a free-text note to client service descriptions. Rejected because it can drift from the XML flag and survive switching contracts incorrectly.

### 5. Add a testable preflight before any selected generator

Extract generation orchestration from the TUI into a small service that receives prepared invoice data and selected formats. Its preflight performs shared tax/identifier/amount validation, existing billing-account resolution, and all current `KsefXmlGenerator.Validate` checks if XML is selected. Preparation stays free of filesystem writes and remote lookups; exchange rates are supplied from the existing TUI flow.

Only after successful preflight may the service call generators and create directories. Update invoice numbering and persist selected output preferences only after successful generation; validation failure must also avoid changing those preferences in memory, so a later unrelated save cannot persist a failed selection. Put any newly throwing invoice preparation inside the handled error boundary. Each public generator performs its own shared preflight before opening its output file; XML retains its format-specific checks.

The XML-only restrictions on currency and exchange rate remain scoped to requests including XML. Thus a missing EUR rate blocks a mixed PDF/XML request entirely, while a valid PDF-only invoice remains possible. Existing files are protected on validation failure because no writer is opened before preflight succeeds.

Alternative: Generate XML first. Rejected because it still provides no common validation for PDF-only requests and can write XML before discovering another selected-format validation issue.

### 6. Verify semantic output as well as schema structure

Use a contract matrix with domestic rates 23/22/8/7/5, Estonia with and without a stored VAT prefix, US default false, and explicit non-EU true. Verify treatment, monetary values, annotations and buyer/seller identification; validate generated XML with the existing local schema validator. Include PLN and a foreign currency for Estonia/US, with fixed clock and valid exchange rate where XML is selected.

Extend configuration tests for missing/explicit values, false round trips, malformed values and migration idempotence. Extend document tests to read generated DOCX text and test shared PDF text inputs, then inspect actual generated PDFs for label wrapping, zero VAT and conditional annotations. Existing PDF tests only check nonempty output; manual inspection of the generated matrix PDFs is required in addition to those tests. No new runtime dependency is needed.

Exercise the extracted orchestration with invalid selected XML, invalid tax data, existing sentinel files, and unchanged numbering/preferences. Test direct generator calls and mutated invoice totals/settings. Update EU fixtures to consistent supplier EU VAT data; retain domestic reference fixture assertions and the existing default-configuration schema test.

## Risks / Trade-offs

- Stricter validation can block previously permitted PDF/DOCX output. Mitigation: identify client and missing field in errors, keep incomplete configurations saveable, and document migration requirements.
- The US compatibility default is specific to the confirmed contract assumptions. Mitigation: make the effective choice visible and retain a deliberate non-EU override; require a choice for other unresolved non-EU countries.
- Country alone cannot establish the recipient or applicability of the general B2B rule. Mitigation: state the supported transaction scope in configuration guidance and require the operator to establish applicable registration/service facts outside the resolver.
- Longer bilingual tax text can overflow narrow tables. Mitigation: wrap cells and inspect actual A4 PDF/DOCX samples.
- Schema validity alone cannot prove correct tax semantics or successful KSeF issuance. Mitigation: assert exact business fields separately and document the external submission/recipient-visualization boundary.

## Migration Plan

1. Keep existing `country`, `vat_rate`, billing-account and numbering data. Apply the US missing-choice default in memory and persist false on the next ordinary save; retain explicit values.
2. Document that the existing US XML flag changes to `P_18=2`, other non-EU clients may need an explicit choice, and Polish/EU document generation now needs valid required identifiers.
3. Configure a new Estonian client with `country = "EE"`, `vat_rate = 0`, the actual EE VAT identifier, service details, currency and billing account. Retain the former Polish client separately if historical regeneration is needed; do not manufacture real identifiers or rewrite prior invoices.
4. Run the contract matrix and inspect generated customer documents before using the updated application for issuance.
5. Existing binaries ignore the new TOML key, but restore the old automatic foreign reverse-charge behavior if rolled back. Preserve a copy of the prior configuration before upgrading operational use; rollback requires reviewing the tax behavior rather than assuming equivalent outputs. This change does not auto-submit or reissue any invoice.
