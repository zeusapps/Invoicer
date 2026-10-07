# Spec Delta

## MODIFIED Requirements

### Requirement: Client VAT Is Optional Outside The EU

A client's VAT number SHALL be optional when the client is outside the EU. The system SHALL allow such a client to be saved and invoiced with an empty VAT number. For supported Polish and EU B2B service treatments, a buyer VAT identifier SHALL be required and validated before generating any PDF, DOCX or KSeF XML. This requirement SHALL NOT depend on whether XML is selected.

Draft or incomplete client configurations SHALL remain editable and saveable; generation SHALL explain missing or invalid required identifiers. Polish buyer identifiers SHALL follow existing NIP format rules after removing any `PL` prefix and whitespace. Other EU identifiers SHALL follow existing EU VAT format rules, and any supplied prefix SHALL match the client's EU VAT country code. Non-EU identification SHALL remain optional. Validation failure SHALL occur before any invoice file is created or replaced.

#### Scenario: US client without VAT is saved

- **WHEN** a user saves a client with country `US` and an empty VAT
- **THEN** configuration is saved without error and the client's VAT remains empty

#### Scenario: DOCX and PDF generated without VAT

- **WHEN** DOCX and PDF are generated for an otherwise valid US client with an empty VAT and reverse charge disabled
- **THEN** both files are produced without a validation error and the customer block has no VAT line

#### Scenario: Estonian buyer missing VAT

- **WHEN** any invoice output is requested for a client with country `EE` and empty VAT
- **THEN** every selected output is refused before writing and the error identifies the required buyer EU VAT number

#### Scenario: Contradictory EU prefix

- **WHEN** PDF-only output is requested for country `EE` and VAT `DE123456789`
- **THEN** no PDF is written and the error identifies the expected `EE` prefix

#### Scenario: Polish buyer missing VAT

- **WHEN** DOCX-only output is requested for country `PL` and empty VAT
- **THEN** no DOCX is written and the error identifies the required Polish buyer NIP
