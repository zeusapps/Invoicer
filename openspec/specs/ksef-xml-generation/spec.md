## Purpose

Define expected behavior for generating KSeF FA(3) XML invoice output from invoice data while preserving existing DOCX/PDF workflows.

## Requirements

### Requirement: Generate KSeF XML Output

The system SHALL generate an invoice XML file in KSeF FA(3) format from the same invoice data used for DOCX and PDF generation.

#### Scenario: User generates XML invoice

- **WHEN** a user runs invoice generation with XML output enabled
- **THEN** the system produces a KSeF XML file for that invoice

### Requirement: Apply FA(3) Header And Namespace Metadata

The system SHALL emit FA(3) metadata in the XML header using `KodFormularza` with `kodSystemowy="FA (3)"`, `wersjaSchemy="1-0E"`, `WariantFormularza` set to `3`, and the namespace `http://crd.gov.pl/wzor/2025/06/25/13775/`.

#### Scenario: XML contains required schema metadata

- **WHEN** an XML invoice is generated
- **THEN** the XML root namespace and header metadata match the configured FA(3) baseline values

### Requirement: Map Invoice Parties And Totals To KSeF Structure

The system SHALL map supplier, client, line, currency, invoice number/date, and monetary totals into the required KSeF XML structure.

#### Scenario: XML contains mapped business data

- **WHEN** invoice data includes seller, buyer, one or more line items, and totals
- **THEN** the generated XML contains corresponding KSeF party, line, and summary elements with equivalent values

### Requirement: Validate Required KSeF Fields Before Writing XML

The system SHALL validate required KSeF fields before writing the XML file and SHALL fail generation for XML output if required values are missing or invalid.

Client country and client VAT SHALL be validated as follows:

- The client's country SHALL be required and SHALL be a country code accepted by the FA(3) schema.
- A client outside Poland SHALL have a VAT rate of 0. A Polish client SHALL have a VAT rate of 23, 22, 8, 7 or 5.
- A VAT number SHALL be required when the country is Poland or another EU member state, and optional otherwise.
- For a Polish client, the VAT number with any `PL` prefix removed SHALL be a valid NIP.
- For another EU client, a letter prefix on the VAT number, if present, SHALL match the EU VAT code of the client's country.

The client's billing account SHALL resolve to a defined account with a non-empty IBAN. The system SHALL NOT substitute a default country or account for a missing value.

#### Scenario: Required field missing

- **WHEN** XML output is requested and a required KSeF field is missing
- **THEN** the system does not write the XML file and returns a validation error identifying the missing field

#### Scenario: Client country missing

- **WHEN** XML output is requested for a client with an empty country
- **THEN** no XML file is written and the error states that the client country is required

#### Scenario: EU client without VAT

- **WHEN** XML output is requested for a client with country `DE` and an empty VAT
- **THEN** no XML file is written and the error states that VAT is required for that client

#### Scenario: Non-EU client without VAT

- **WHEN** XML output is requested for a client with country `US` and an empty VAT
- **THEN** validation passes

#### Scenario: Foreign client with a VAT rate

- **WHEN** XML output is requested for a client with country `US` and VAT rate 23
- **THEN** no XML file is written and the error states that a client outside Poland must have a VAT rate of 0

#### Scenario: Polish client with an unsupported rate

- **WHEN** XML output is requested for a client with country `PL` and VAT rate 0
- **THEN** no XML file is written and the error states that the rate is not supported for a Polish client

#### Scenario: VAT prefix contradicts country

- **WHEN** XML output is requested for a client with country `DE` and VAT `FR12345678901`
- **THEN** no XML file is written and the error states that the VAT prefix does not match the client country

### Requirement: Support Combined Output Modes

The system SHALL allow XML generation to be selected independently and in combination with DOCX and PDF in the same run.

#### Scenario: XML combined with existing formats

- **WHEN** a user selects XML and PDF outputs for one invoice generation run
- **THEN** the system generates both files without changing the behavior of existing DOCX/PDF generation paths

### Requirement: Preserve Output Naming And Location Rules

The system SHALL apply existing output path and filename conventions to XML output, including configured destination and year-based invoice folder structure.

#### Scenario: XML file placement follows output configuration

- **WHEN** an XML invoice is generated
- **THEN** the XML file is written to the configured output location using the established naming and folder rules

### Requirement: Maintain Deterministic XML Generation

The system SHALL generate deterministic XML for the same invoice input so that repeated runs produce equivalent structure and values. Generation time is the only permitted source of variation: two runs over identical input SHALL differ at most in `Naglowek/DataWytworzeniaFa`, and SHALL be byte-identical when the same fixed time source is supplied to both.

#### Scenario: Repeated generation with same input

- **WHEN** the same invoice data is generated twice with XML output
- **THEN** both resulting XML documents are equivalent in schema metadata, structure, and mapped values, differing at most in `DataWytworzeniaFa`

#### Scenario: Repeated generation with a fixed clock

- **WHEN** the same invoice data is generated twice with XML output and the same fixed time source
- **THEN** both resulting XML documents are byte-identical

### Requirement: Conform To The Official FA(3) XML Schema

Generated KSeF XML SHALL be valid against the official FA(3) XML Schema for namespace `http://crd.gov.pl/wzor/2025/06/25/13775/`. The test suite SHALL verify this by validating generated output against a vendored copy of that schema and every schema it references, resolved from local files without network access. Validation warnings, including "no schema information found", SHALL be treated as failures so that a document matching no schema cannot pass vacuously.

#### Scenario: Generated document is schema-valid

- **WHEN** an XML invoice is generated from valid invoice data
- **THEN** validating the document against the vendored FA(3) schema produces no errors and no warnings

#### Scenario: Structural defect fails the build

- **WHEN** generation emits an element that is unexpected, missing, or out of order for its FA(3) type
- **THEN** schema validation fails and reports each violation with its element name and position

#### Scenario: Default configuration produces a valid document

- **WHEN** an invoice is generated from the configuration a first-run user receives
- **THEN** the resulting document validates against the FA(3) schema, including the schema's value constraints on placeholder identifiers

#### Scenario: Schema resolution never reaches the network

- **WHEN** the schema set is loaded during validation
- **THEN** every referenced schema resolves to a local file, and a reference that cannot be resolved locally fails the test rather than being fetched remotely

### Requirement: Identify The Seller By NIP And Name Only

The seller identification block `Podmiot1/DaneIdentyfikacyjne` SHALL contain exactly `NIP` followed by `Nazwa`, as required by the schema type `TPodmiot1`. The system SHALL NOT emit a `KodKraju` element in that block. The seller's country code SHALL continue to be emitted in `Podmiot1/Adres`, where the schema defines it. Buyer identification in `Podmiot2/DaneIdentyfikacyjne` is governed by the requirement "Identify The Buyer According To Client Country".

#### Scenario: Seller identification contains no country code

- **WHEN** an XML invoice is generated
- **THEN** `Podmiot1/DaneIdentyfikacyjne` contains only `NIP` and `Nazwa`, in that order

#### Scenario: Seller address retains its country code

- **WHEN** an XML invoice is generated
- **THEN** `Podmiot1/Adres` contains `KodKraju` followed by `AdresL1`

#### Scenario: Buyer identification is unchanged

- **WHEN** an XML invoice is generated
- **THEN** the seller identification rules do not constrain `Podmiot2/DaneIdentyfikacyjne`, whose content follows the client's country as defined in "Identify The Buyer According To Client Country"

### Requirement: Record Actual Generation Time In The Header

`Naglowek/DataWytworzeniaFa` SHALL carry the UTC instant at which the document was generated, not a value derived from the invoice date. The value SHALL fall within the range the schema permits, `2025-09-01T00:00:00Z` through `2050-01-01T23:59:59Z`. The clock SHALL be injectable so that generation can be exercised against a fixed instant.

#### Scenario: Header reflects generation time

- **WHEN** an XML invoice is generated
- **THEN** `DataWytworzeniaFa` equals the current UTC instant, independent of the invoice date

#### Scenario: Backdated invoice still produces a valid header

- **WHEN** an invoice is generated with an invoice date earlier than 2025-09-01
- **THEN** `DataWytworzeniaFa` remains within the schema's permitted range and the document validates

#### Scenario: Clock can be pinned for testing

- **WHEN** generation is invoked with a fixed time source
- **THEN** `DataWytworzeniaFa` equals that fixed instant

### Requirement: Identify The Buyer According To Client Country

`Podmiot2/DaneIdentyfikacyjne` SHALL use exactly one of the identification forms defined by the FA(3) type `TPodmiot2`. The form SHALL be selected from the client's country and VAT number, and `Nazwa` SHALL follow it:

- **Poland:** `NIP` holding the VAT number with any `PL` prefix and any whitespace removed.
- **Another EU member state:**
  - `KodUE` holding the country's EU VAT code, which is the country code except `GR` becomes `EL`;
  - then `NrVatUE` holding the VAT number with that prefix and any whitespace removed.
- **Outside the EU, with a VAT number:** `KodKraju` holding the client's country, then `NrID` holding the VAT number as entered.
- **Outside the EU, without a VAT number:** `BrakID` with value `1`.

The selection SHALL NOT depend on the letters at the start of the VAT number.

#### Scenario: Polish buyer

- **WHEN** an XML invoice is generated for a client with country `PL` and VAT `PL9999999999`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `NIP` = `9999999999` followed by `Nazwa`, and the document validates against the FA(3) schema

#### Scenario: EU buyer

- **WHEN** an XML invoice is generated for a client with country `DE` and VAT `DE123456789`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `KodUE` = `DE`, `NrVatUE` = `123456789`, then `Nazwa`, and the document validates

#### Scenario: Greek buyer uses EL prefix

- **WHEN** an XML invoice is generated for a client with country `GR` and VAT `EL123456789`
- **THEN** `KodUE` is `EL` and `NrVatUE` is `123456789`

#### Scenario: Non-EU buyer with tax ID

- **WHEN** an XML invoice is generated for a client with country `US` and VAT `12-3456789`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `KodKraju` = `US`, `NrID` = `12-3456789`, then `Nazwa`, and the document validates

#### Scenario: Non-EU buyer without tax ID

- **WHEN** an XML invoice is generated for a client with country `US` and an empty VAT
- **THEN** `Podmiot2/DaneIdentyfikacyjne` contains `BrakID` = `1` followed by `Nazwa`, contains no `KodKraju` or `NrID`, and the document validates

### Requirement: Take Buyer Address Country From Client

`Podmiot2/Adres/KodKraju` SHALL equal the client's country for every buyer, regardless of the identification form used.

#### Scenario: US buyer address

- **WHEN** an XML invoice is generated for a client with country `US`
- **THEN** `Podmiot2/Adres/KodKraju` is `US`

#### Scenario: Country independent of VAT prefix

- **WHEN** an XML invoice is generated for a client with country `US` and a VAT number that starts with `PL`
- **THEN** `Podmiot2/Adres/KodKraju` is `US`

### Requirement: Code The Tax Rate According To Client Country

The line tax rate `FaWiersz/P_12`, the invoice summary fields and the reverse-charge annotation `Adnotacje/P_18` SHALL be derived from the client's country and VAT rate. The mapping follows the FA(3) definitions of `TStawkaPodatku`:

- **Client outside the EU:** services outside Poland not covered by art. 100 ust. 1 pkt 4 of the VAT act.
  - `P_12` SHALL be `np I`;
  - the net amount SHALL be written to `P_13_8`;
  - no `P_14_x` element SHALL be written;
  - `P_18` SHALL be `1`.
- **Client in another EU member state:** services under art. 100 ust. 1 pkt 4.
  - `P_12` SHALL be `np II`;
  - the net amount SHALL be written to `P_13_9`;
  - no `P_14_x` element SHALL be written;
  - `P_18` SHALL be `1`;
  - `Podmiot1/PrefiksPodatnika` SHALL be `PL`.
- **Polish client:** `P_12` SHALL be the numeric rate, and `P_18` SHALL be `2`. The net and VAT amounts SHALL be written to the pair matching the rate:
  - 23 or 22: `P_13_1` and `P_14_1`;
  - 8 or 7: `P_13_2` and `P_14_2`;
  - 5: `P_13_3` and `P_14_3`.

In every case `P_15` SHALL equal the gross amount, which is the net amount when no VAT applies. The code `oo` SHALL NOT be used, because it is limited to domestic reverse charge.

#### Scenario: US client

- **WHEN** an XML invoice is generated for a client with country `US`, VAT rate 0 and net amount 2500
- **THEN** `P_12` is `np I`, `P_13_8` is `2500`, no `P_13_1` or `P_14_x` exists, `P_15` is `2500`, `P_18` is `1`, and the document validates against the FA(3) schema

#### Scenario: EU client

- **WHEN** an XML invoice is generated for a client with country `DE`, VAT `DE123456789`, VAT rate 0 and net amount 1000
- **THEN** `P_12` is `np II`, `P_13_9` is `1000`, no `P_14_x` exists, `P_15` is `1000`, `P_18` is `1`, `Podmiot1/PrefiksPodatnika` is `PL`, and the document validates

#### Scenario: Polish client at 23%

- **WHEN** an XML invoice is generated for a client with country `PL` and VAT rate 23
- **THEN** `P_12` is `23`, net and VAT are in `P_13_1` and `P_14_1`, `P_15` is net plus VAT, `P_18` is `2`, there is no `PrefiksPodatnika`, and the document validates

#### Scenario: Polish client at 8%

- **WHEN** an XML invoice is generated for a client with country `PL` and VAT rate 8
- **THEN** `P_12` is `8` and net and VAT are in `P_13_2` and `P_14_2`

### Requirement: Take Payment Details From The Client's Billing Account

`Fa/Platnosc/RachunekBankowy` SHALL be populated from the billing account assigned to the invoice's client:

- `NrRB` SHALL hold the account's IBAN with whitespace removed.
- `SWIFT` SHALL hold the account's SWIFT when it is non-empty.
- `NazwaBanku` SHALL hold the account's bank name when it is non-empty.

The elements SHALL appear in the order the schema defines.

#### Scenario: Account with bank name

- **WHEN** an XML invoice is generated for a client whose account has IBAN `PL42 1090 1320 0000 0001 5470 1995`, SWIFT `WBKPPLPP` and bank `Santander`
- **THEN** `RachunekBankowy` contains `NrRB` = `PL42109013200000000154701995`, `SWIFT` = `WBKPPLPP` and `NazwaBanku` = `Santander`, in that order, and the document validates

#### Scenario: Account without SWIFT or bank name

- **WHEN** the client's account has an IBAN but empty SWIFT and bank
- **THEN** `RachunekBankowy` contains only `NrRB`

### Requirement: Write The Exchange Rate For Foreign-Currency Invoices

When the invoice currency is not `PLN`, the generated XML SHALL carry the invoice's exchange rate in `Fa/FaWiersz/KursWaluty`, positioned where the FA(3) schema defines it: after `Procedura` and before `StanPrzed`.

The value SHALL be written at the invoice's full recorded precision, with no rounding, padding or trailing zeros added, and SHALL satisfy the schema's numeric form — no leading zeros on the integer part and no trailing decimal point.

When the invoice currency is `PLN`, no `KursWaluty` element SHALL be written.

The system SHALL NOT write `Fa/KursWalutyZ` or `Fa/WarunkiTransakcji/KursUmowny`. Those fields cover advance invoices and contractual rates respectively, neither of which this system issues.

#### Scenario: USD invoice carries the rate

- **WHEN** an XML invoice is generated for a `USD` invoice with an exchange rate of 3.7998
- **THEN** `Fa/FaWiersz/KursWaluty` is `3.7998`, it appears as the last element of `FaWiersz`, and the document validates against the FA(3) schema

#### Scenario: Rate with six decimal places

- **WHEN** an XML invoice is generated with an exchange rate of 0.012007
- **THEN** `KursWaluty` is `0.012007` and the document validates

#### Scenario: PLN invoice omits the rate

- **WHEN** an XML invoice is generated for a `PLN` invoice
- **THEN** no `KursWaluty` element is present anywhere in the document, and the document validates

#### Scenario: Other rate fields are never written

- **WHEN** any XML invoice is generated
- **THEN** the document contains no `KursWalutyZ`, `KursUmowny` or `WalutaUmowna` element

### Requirement: Take The Exchange Rate From Invoice Data

XML generation SHALL read the exchange rate from the invoice data supplied to it and SHALL NOT retrieve a rate while generating.

This keeps generation free of network access, so that the existing determinism guarantee continues to hold: the same invoice data generated twice produces documents differing at most in `Naglowek/DataWytworzeniaFa`.

#### Scenario: Generation performs no rate lookup

- **WHEN** an XML invoice is generated for a foreign-currency invoice with no network available
- **THEN** generation succeeds using the rate recorded on the invoice

#### Scenario: Determinism with a rate present

- **WHEN** the same foreign-currency invoice data is generated twice with the same fixed time source
- **THEN** both documents are byte-identical

### Requirement: Validate The Exchange Rate Before Writing XML

For an invoice whose currency is not `PLN`, the system SHALL validate before writing the XML file that:

- an exchange rate is present and greater than zero; and
- the rate has at most six decimal places, which is the maximum the FA(3) schema can represent.

When either check fails the system SHALL NOT write the XML file and SHALL return a validation error naming the exchange rate as the cause. A rate too precise to represent SHALL be reported as such rather than silently rounded to fit, because a silently rounded rate misstates the PLN taxable base.

#### Scenario: Foreign-currency invoice without a rate

- **WHEN** XML output is requested for a `USD` invoice with no exchange rate
- **THEN** no XML file is written and the error states that an exchange rate is required for a non-PLN invoice

#### Scenario: Rate too precise for the schema

- **WHEN** XML output is requested with an exchange rate of 0.00021425
- **THEN** no XML file is written and the error states that the rate exceeds six decimal places

#### Scenario: PLN invoice without a rate

- **WHEN** XML output is requested for a `PLN` invoice with no exchange rate
- **THEN** validation passes and the XML file is written

### Requirement: Reject A Polish Client Invoiced In A Foreign Currency

The system SHALL NOT generate KSeF XML for an invoice whose client country is Poland and whose currency is not `PLN`. No XML file SHALL be written, and the error SHALL state that the combination is unsupported.

FA(3) requires the tax amounts of such an invoice to be expressed in PLN while the sales values stay in the invoice currency. The system does not perform that conversion, so emitting the amounts unconverted would produce an invoice that is schema-valid and factually wrong. DOCX and PDF generation SHALL be unaffected.

#### Scenario: Polish client billed in USD

- **WHEN** XML output is requested for a client with country `PL`, VAT rate 23 and currency `USD`
- **THEN** no XML file is written and the error states that a Polish client cannot be invoiced in a foreign currency

#### Scenario: Foreign client billed in a foreign currency

- **WHEN** XML output is requested for a client with country `US` and currency `USD`
- **THEN** validation passes
