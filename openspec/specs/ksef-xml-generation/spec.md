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

#### Scenario: Required field missing

- **WHEN** XML output is requested and a required KSeF field is missing
- **THEN** the system does not write the XML file and returns a validation error identifying the missing field

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

The seller identification block `Podmiot1/DaneIdentyfikacyjne` SHALL contain exactly `NIP` followed by `Nazwa`, as required by the schema type `TPodmiot1`. The system SHALL NOT emit a `KodKraju` element in that block. The seller's country code SHALL continue to be emitted in `Podmiot1/Adres`, where the schema defines it, and buyer identification in `Podmiot2/DaneIdentyfikacyjne` SHALL be unaffected.

#### Scenario: Seller identification contains no country code

- **WHEN** an XML invoice is generated
- **THEN** `Podmiot1/DaneIdentyfikacyjne` contains only `NIP` and `Nazwa`, in that order

#### Scenario: Seller address retains its country code

- **WHEN** an XML invoice is generated
- **THEN** `Podmiot1/Adres` contains `KodKraju` followed by `AdresL1`

#### Scenario: Buyer identification is unchanged

- **WHEN** an XML invoice is generated for a buyer identified by `NrID`
- **THEN** `Podmiot2/DaneIdentyfikacyjne` continues to emit `KodKraju` before `NrID`

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

