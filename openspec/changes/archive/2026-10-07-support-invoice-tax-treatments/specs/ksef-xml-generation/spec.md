# Spec Delta

## MODIFIED Requirements

### Requirement: Validate Required KSeF Fields Before Writing XML

The system SHALL validate required KSeF fields before writing XML and SHALL fail XML generation if required values are missing or invalid. When XML is selected with PDF or DOCX, these checks SHALL run before any selected file is created or replaced.

Client country and client VAT SHALL be validated as follows:

- The client's country SHALL be required and accepted by the FA(3) schema.
- A client outside Poland SHALL have a VAT rate of 0. A Polish client SHALL have a rate of 23, 22, 8, 7 or 5.
- A VAT number SHALL be required for Poland or another EU member state, and optional otherwise.
- For a Polish client, the VAT number with any `PL` prefix removed SHALL be a valid NIP.
- For another EU client, a letter prefix, if present, SHALL match the country's EU VAT code.
- Reverse charge, seller EU VAT identity and monetary consistency SHALL satisfy the shared `invoice-tax-treatment` requirements.

The client's billing account SHALL resolve to a defined account with a non-empty IBAN. The system SHALL NOT substitute a default country or account for a missing value. Existing required invoice, supplier, payment and currency checks SHALL continue to apply. Failure SHALL leave all selected output files and numbering unchanged.

#### Scenario: Required field missing

- **WHEN** XML is requested and a required KSeF field is missing
- **THEN** no selected file is written and a validation error identifies the missing field

#### Scenario: Client country missing

- **WHEN** XML is requested for a client with an empty country
- **THEN** no selected file is written and the error states that client country is required

#### Scenario: EU client without VAT

- **WHEN** XML is requested for country `DE` and empty VAT
- **THEN** no selected file is written and the error states that VAT is required for that client

#### Scenario: Non-EU client without VAT

- **WHEN** XML is requested for an otherwise valid US client with reverse charge disabled and empty VAT
- **THEN** validation passes

#### Scenario: Foreign client with a VAT rate

- **WHEN** XML is requested for country `US` and VAT rate 23
- **THEN** no selected file is written and the error states that a client outside Poland must have VAT rate 0

#### Scenario: Polish client with an unsupported rate

- **WHEN** XML is requested for country `PL` and VAT rate 0
- **THEN** no selected file is written and the error states that the rate is unsupported for a Polish client

#### Scenario: VAT prefix contradicts country

- **WHEN** XML is requested for country `DE` and VAT `FR12345678901`
- **THEN** no selected file is written and the error states that the VAT prefix does not match client country

### Requirement: Support Combined Output Modes

The system SHALL allow XML selection independently and in combination with DOCX and PDF. All selected formats SHALL represent the same validated tax treatment and amounts. Shared and selected-format validation SHALL finish before writing any selected file. Validation failure SHALL produce no partial output, advance no invoice number and save no changed output preferences.

#### Scenario: XML combined with existing formats

- **WHEN** XML and PDF are selected for one valid generation run
- **THEN** both files are generated with the same tax treatment and equivalent amounts

#### Scenario: Combined output rejected before writing

- **WHEN** XML and PDF are selected and XML validation fails
- **THEN** neither file is created or replaced, numbering and saved output preferences stay unchanged, and the error identifies the failed validation

### Requirement: Code The Tax Rate According To Client Country

The line rate `FaWiersz/P_12`, summary fields and `Adnotacje/P_18` SHALL reflect the shared treatment derived from client country, VAT rate and applicable non-EU reverse-charge choice. The mapping follows FA(3) `TStawkaPodatku`:

- **Client outside the EU:** services outside Poland not covered by art. 100 ust. 1 pkt 4.
  - `P_12` SHALL be `np I`;
  - net SHALL be written to `P_13_8`;
  - no `P_14_x` SHALL be written;
  - `P_18` SHALL be `1` when reverse charge is enabled and `2` when disabled;
  - no seller `PrefiksPodatnika` SHALL be written for this treatment.
- **Client in another EU member state:** services under art. 100 ust. 1 pkt 4.
  - `P_12` SHALL be `np II`;
  - net SHALL be written to `P_13_9`;
  - no `P_14_x` SHALL be written;
  - `P_18` SHALL be `1`;
  - `Podmiot1/PrefiksPodatnika` SHALL be `PL`.
- **Polish client:** `P_12` SHALL be the numeric rate and `P_18` SHALL be `2`. Net and VAT SHALL use the matching pair:
  - 23 or 22: `P_13_1` and `P_14_1`;
  - 8 or 7: `P_13_2` and `P_14_2`;
  - 5: `P_13_3` and `P_14_3`.

`P_15` SHALL equal gross in every case, equal to net for foreign services. Foreign-service invoices SHALL NOT use `oo`, `0 KR`, `0 WDT`, `0 EX` or `zw` in place of their NP code. The code `oo` is limited to domestic reverse charge.

#### Scenario: US client

- **WHEN** XML is generated for country `US`, VAT rate 0, reverse charge disabled and net 2500
- **THEN** `P_12=np I`, `P_13_8=2500`, no `P_13_1` or `P_14_x` exists, `P_15=2500`, `P_18=2`, no seller prefix exists, and the document validates against FA(3)

#### Scenario: Non-EU reverse-charge override

- **WHEN** XML is generated for country `US`, VAT rate 0, reverse charge explicitly enabled and net 2500
- **THEN** NP code, summary, amounts and absence of seller prefix are unchanged, `P_18=1`, and the document validates against FA(3)

#### Scenario: EU client

- **WHEN** XML is generated for country `DE`, VAT `DE123456789`, VAT rate 0, consistent seller EU VAT identifiers and net 1000
- **THEN** `P_12=np II`, `P_13_9=1000`, no `P_14_x` exists, `P_15=1000`, `P_18=1`, `Podmiot1/PrefiksPodatnika=PL`, and the document validates

#### Scenario: Estonian client

- **WHEN** XML is generated for country `EE`, VAT `EE123456789`, VAT rate 0, consistent seller EU VAT identifiers and net 1000
- **THEN** `P_12=np II`, `P_13_9=1000`, no `P_13_8` or `P_14_x` exists, `P_15=1000`, `P_18=1`, seller prefix is `PL`, buyer identification has `KodUE=EE` and `NrVatUE=123456789`, buyer address country is `EE`, and the document validates

#### Scenario: Polish client at 23%

- **WHEN** XML is generated for country `PL` and VAT rate 23
- **THEN** `P_12=23`, net and VAT are in `P_13_1` and `P_14_1`, `P_15` is net plus VAT, `P_18=2`, no seller prefix exists, and the document validates

#### Scenario: Polish client at 8%

- **WHEN** XML is generated for country `PL` and VAT rate 8
- **THEN** `P_12=8` and net and VAT are in `P_13_2` and `P_14_2`

### Requirement: Reject A Polish Client Invoiced In A Foreign Currency

The system SHALL NOT generate XML for a Polish client invoiced outside `PLN`. When XML is selected, no selected file SHALL be created or replaced, and the error SHALL identify the unsupported combination.

FA(3) requires tax amounts of such an invoice in PLN while sales values remain in invoice currency. The system does not perform that conversion, so unconverted amounts would be schema-valid and factually wrong. PDF/DOCX SHALL remain available when XML is not selected and shared validation passes.

#### Scenario: Polish client billed in USD

- **WHEN** XML is requested for country `PL`, VAT rate 23 and currency `USD`
- **THEN** no selected file is written and the error states that a Polish client cannot be invoiced in a foreign currency for KSeF XML

#### Scenario: Foreign client billed in a foreign currency

- **WHEN** XML is requested for an otherwise valid US client with reverse charge disabled, currency `USD` and a valid exchange rate
- **THEN** validation passes

#### Scenario: Polish foreign-currency documents without XML

- **WHEN** PDF and DOCX are requested without XML for a Polish client in a foreign currency and shared validation passes
- **THEN** both documents are generated without applying the XML-only currency restriction
