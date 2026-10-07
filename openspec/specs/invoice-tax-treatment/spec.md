# invoice-tax-treatment Specification

## Purpose

Keeps the tax treatment, totals and annotations of ordinary B2B service invoices consistent across the client editor, invoice preview, customer documents and KSeF XML.

## Requirements

### Requirement: Resolve A Shared Invoice Tax Treatment

For supported ordinary B2B services, every output and the preview SHALL use the same treatment resolved from client country, numeric VAT rate and applicable reverse-charge choice:

- Poland: domestic VAT at a supported rate of 23, 22, 8, 7 or 5; no reverse charge.
- Another EU member state: services under art. 28b and art. 100 ust. 1 pkt 4, with no Polish VAT charged and reverse charge applicable.
- Outside the EU: services outside Poland with no Polish VAT charged; reverse charge follows the non-EU choice independently of that classification.

Foreign-service configurations SHALL require `vat_rate = 0`. This SHALL NOT be presented as an ordinary `0%` or VAT-exempt sale. Missing or unrecognized countries, unsupported domestic rates, nonzero foreign rates and contradictory reverse-charge choices SHALL produce actionable validation errors identifying the client and setting. The system SHALL NOT silently change country or rate to make an invoice valid.

#### Scenario: Polish contract

- **WHEN** an invoice for a client with country `PL` and VAT rate 23 is prepared for a net fee of 1000 PLN
- **THEN** every output and the preview use domestic VAT, a VAT amount of 230 PLN, a total of 1230 PLN and no reverse-charge annotation

#### Scenario: Estonian contract

- **WHEN** an invoice for an EU VAT-identified client with country `EE` and VAT rate 0 is prepared for a net fee of 1000 EUR
- **THEN** every output and the preview use EU reverse charge, charge no Polish VAT and show a total of 1000 EUR

#### Scenario: US contract

- **WHEN** an invoice for a client with country `US`, VAT rate 0 and reverse charge disabled is prepared for a net fee of 1000 USD
- **THEN** every output and the preview identify services outside Polish VAT, charge no Polish VAT, show a total of 1000 USD and contain no reverse-charge annotation

#### Scenario: Country changed without correcting rate

- **WHEN** a client is changed from country `PL` to `EE` while its VAT rate remains 23
- **THEN** the preview reports inconsistent tax configuration and every requested output is refused before any file is written

### Requirement: Configure Non-EU Reverse Charge Independently

Non-EU clients SHALL support a persisted boolean `reverse_charge` setting. Enabling it SHALL add reverse-charge wording and set the corresponding XML flag without changing net, Polish VAT, gross or outside-Poland classification. Disabling it SHALL omit the wording and set the negative XML flag.

Polish and other EU treatment SHALL determine reverse charge. An explicit true setting for Poland or false setting for another EU country SHALL be rejected as contradictory; matching settings SHALL be accepted. Non-boolean TOML values SHALL be reported as invalid configuration rather than coerced or treated as absent.

#### Scenario: Non-EU choice survives a round trip

- **WHEN** clients with `reverse_charge = true` and `reverse_charge = false` are saved and reloaded
- **THEN** each explicit boolean value is preserved

#### Scenario: Non-EU reverse charge enabled

- **WHEN** a US client with VAT rate 0 has reverse charge explicitly enabled
- **THEN** every output uses reverse-charge wording, XML uses `P_18=1`, and classification and amounts remain the same as with reverse charge disabled

#### Scenario: EU treatment cannot be disabled

- **WHEN** generation is requested for a client with country `EE`, VAT rate 0 and `reverse_charge = false`
- **THEN** every output is refused before writing, with an error explaining that the supported EU B2B treatment requires reverse charge

#### Scenario: Domestic reverse charge is unsupported

- **WHEN** generation is requested for a client with country `PL`, VAT rate 23 and `reverse_charge = true`
- **THEN** every output is refused before writing, with an error identifying the contradictory setting

### Requirement: Load Missing Reverse-Charge Settings Deliberately

Existing and new US clients without an explicit reverse-charge setting SHALL resolve to no reverse charge, as selected for this contract; their next save SHALL persist `reverse_charge = false`. Explicit existing choices SHALL never be overwritten. Polish and other EU clients without a setting SHALL continue to resolve their domestic and EU treatments respectively.

Other non-EU clients without a choice SHALL remain unresolved: loading, editing and saving SHALL be allowed, but generation SHALL require an explicit Yes or No. Saving SHALL preserve unresolved choices as absent. Missing countries SHALL remain subject to the existing country-migration rules, with no default country substituted.

#### Scenario: Existing US configuration

- **WHEN** a configuration containing a client with country `US`, VAT rate 0 and no reverse-charge key is loaded and used to generate an invoice
- **THEN** reverse charge is disabled and the next save writes `reverse_charge = false`

#### Scenario: Existing US override

- **WHEN** a configuration containing a client with country `US` and `reverse_charge = true` is loaded
- **THEN** the explicit true setting is retained

#### Scenario: Existing EU configuration

- **WHEN** a client with country `EE`, VAT rate 0 and no reverse-charge key is loaded
- **THEN** it resolves to EU reverse charge without requiring a new choice

#### Scenario: Unresolved non-EU configuration

- **WHEN** a client with country `GB`, VAT rate 0 and no reverse-charge key is loaded and saved
- **THEN** loading and saving succeed, the setting stays absent, and generation requires an explicit reverse-charge choice before writing any output

### Requirement: Explain Tax Treatment In The Editor And Preview

The editor and invoice preview SHALL display the resolved treatment in plain language: `Domestic Polish VAT`, `EU B2B reverse charge`, `Outside Polish VAT - no reverse charge`, or `Outside Polish VAT - reverse charge`. Invalid or unresolved settings SHALL display the specific issue rather than a successful-looking tax summary.

For non-EU clients, the editor SHALL offer an explicit Yes/No choice and visibly represent unresolved choices. Its explanation SHALL state that reverse charge concerns whether the customer accounts for tax and whether the wording appears; it does not add Polish VAT to the amount payable. Derived Polish/EU treatment SHALL be shown without offering a contradictory choice. Switching clients or editing country, rate or reverse charge SHALL refresh treatment, amounts and applicable controls from the new data.

#### Scenario: Switch between contracts

- **WHEN** the user switches between valid Polish, Estonian and US clients
- **THEN** the editor and preview show each client's own treatment and amounts, with reverse charge for Estonia and no reverse charge for the US default

#### Scenario: Non-EU choice is explained

- **WHEN** the user edits a non-EU client
- **THEN** the choice explains its effect on customer tax accounting and annotations while making clear that no Polish VAT is added

### Requirement: Render Tax Treatment In Customer Documents

PDF and DOCX SHALL render the same tax label, Polish VAT amount and annotation. Domestic invoices SHALL show the numeric rate and calculated VAT. Foreign-service invoices SHALL show `NP - not subject to Polish VAT` and a zero Polish VAT amount formatted in the invoice currency, including the VAT total. The tax column heading SHALL accommodate a treatment rather than imply that every value is a percentage. The preview SHALL convey the same label and amount.

When reverse charge applies, both documents SHALL contain the exact annotation `Reverse charge / odwrotne obciążenie`; otherwise both SHALL omit it. Existing English/Ukrainian content SHALL be preserved and new tax text SHALL have an appropriate Ukrainian explanation. Foreign-service tax cells SHALL contain neither `N/A`, `0%` nor a VAT-exempt label.

#### Scenario: Estonian customer documents

- **WHEN** valid PDF and DOCX invoices are generated for an Estonian EU B2B client
- **THEN** both show the NP label, zero Polish VAT and reverse-charge annotation, with net equal to gross

#### Scenario: US customer documents

- **WHEN** valid PDF and DOCX invoices are generated for a US client with reverse charge disabled
- **THEN** both show the NP label, zero Polish VAT and net equal to gross, and neither contains reverse-charge wording

#### Scenario: Non-EU reverse-charge documents

- **WHEN** valid PDF and DOCX invoices are generated for a non-EU client with reverse charge explicitly enabled
- **THEN** both include the reverse-charge annotation while retaining the outside-Poland treatment

### Requirement: Validate Party Identifiers And Monetary Consistency

Every output SHALL require a seller NIP, valid client country and the buyer identification required by `client-country`. For EU B2B reverse-charge invoices, the seller EU VAT identifier SHALL be `PL` followed by the same NIP; the buyer identifier SHALL use its country's EU VAT prefix. Customer documents SHALL show complete prefixed EU identifiers even when buyer configuration omits the prefix. Normalization SHALL remove whitespace and normalize letter case. Format validation SHALL NOT claim to verify active registration in VIES.

Domestic VAT SHALL equal net multiplied by the supported rate, rounded to two decimals under the existing rounding rule; gross SHALL equal net plus VAT. For foreign services, Polish VAT SHALL be zero and gross SHALL equal net. Stale or manually inconsistent amounts or tax settings SHALL be rejected before writing rather than silently repaired or emitted.

#### Scenario: Missing seller EU VAT identifier

- **WHEN** an Estonian EU B2B invoice is requested with a seller NIP but no seller EU VAT identifier
- **THEN** every output is refused before writing with an error identifying the missing seller EU VAT identifier

#### Scenario: Seller identifiers disagree

- **WHEN** the seller EU VAT number has prefix `PL` but its numeric part differs from the seller NIP
- **THEN** an EU reverse-charge invoice is refused before writing with an error identifying the disagreement

#### Scenario: Buyer number without prefix

- **WHEN** an Estonian client stores an otherwise valid buyer VAT number without the `EE` prefix
- **THEN** PDF and DOCX show the complete `EE` identifier while XML uses `KodUE=EE` and the unprefixed number

#### Scenario: Inconsistent foreign totals

- **WHEN** a foreign-service invoice carries nonzero Polish VAT or gross different from net
- **THEN** every output is refused before writing with an error identifying the inconsistent amounts

### Requirement: Validate Selected Outputs Before Writing Files

The workflow SHALL validate shared tax data for every requested format before creating output directories, writing or replacing invoice files, advancing numbering or saving changed output preferences. If XML is selected, its additional checks SHALL run before any selected file is written. Validation failure SHALL leave existing invoice files, numbering and saved output preferences unchanged and return actionable errors.

PDF-only and DOCX-only generation SHALL require shared tax validity but SHALL NOT require an exchange rate solely because XML needs one. Format-specific checks SHALL apply only to selected formats. Each standalone generator SHALL enforce shared tax checks before writing its own output so direct invocation cannot bypass validation.

#### Scenario: XML validation fails in a combined run

- **WHEN** PDF, DOCX and XML are selected for a valid EUR foreign-service invoice without an exchange rate
- **THEN** no file is created or replaced, numbering and saved output preferences do not change, and the error identifies the missing exchange rate

#### Scenario: Existing files survive failed validation

- **WHEN** any selected output path already exists and the request fails tax or selected-format validation
- **THEN** every existing file retains its original content

#### Scenario: Documents alone do not require an XML rate

- **WHEN** valid PDF and DOCX are requested for an Estonian EUR invoice without XML or an exchange rate
- **THEN** both documents are generated with the correct EU reverse-charge treatment

#### Scenario: Direct generator validates shared data

- **WHEN** a PDF, DOCX or XML generator is invoked directly with an invalid country/rate combination
- **THEN** it refuses its output before creating or replacing an invoice file
