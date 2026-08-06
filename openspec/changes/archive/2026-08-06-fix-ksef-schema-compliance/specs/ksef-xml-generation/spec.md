## ADDED Requirements

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

## MODIFIED Requirements

### Requirement: Maintain Deterministic XML Generation

The system SHALL generate deterministic XML for the same invoice input so that repeated runs produce equivalent structure and values. Generation time is the only permitted source of variation: two runs over identical input SHALL differ at most in `Naglowek/DataWytworzeniaFa`, and SHALL be byte-identical when the same fixed time source is supplied to both.

#### Scenario: Repeated generation with same input

- **WHEN** the same invoice data is generated twice with XML output
- **THEN** both resulting XML documents are equivalent in schema metadata, structure, and mapped values, differing at most in `DataWytworzeniaFa`

#### Scenario: Repeated generation with a fixed clock

- **WHEN** the same invoice data is generated twice with XML output and the same fixed time source
- **THEN** both resulting XML documents are byte-identical
