## ADDED Requirements

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

The system SHALL generate deterministic XML for the same invoice input so that repeated runs produce equivalent structure and values.

#### Scenario: Repeated generation with same input

- **WHEN** the same invoice data is generated twice with XML output
- **THEN** both resulting XML documents are equivalent in schema metadata, structure, and mapped values
