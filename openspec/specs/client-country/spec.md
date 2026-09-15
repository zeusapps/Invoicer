# client-country Specification

## Purpose
Records the country of each client explicitly, so that country-dependent invoice content is based on real client data and not guessed from the VAT number. That content is whether VAT applies, how the buyer is identified in KSeF, and the buyer's address country.

## Requirements

### Requirement: Client Has A Country

Each client SHALL have a `country` holding an ISO 3166-1 alpha-2 code. The value SHALL be persisted in the configuration file, shown and editable in the client editor, and normalized to upper case when saved. A newly added client SHALL start with an empty country.

#### Scenario: Country is persisted

- **WHEN** a user sets a client's country to `us` in the client editor and saves
- **THEN** the configuration file stores `country = "US"` for that client, and reloading the configuration yields `US`

#### Scenario: Country is shown in the editor

- **WHEN** a user selects a client in the client editor
- **THEN** the editor shows that client's country in an editable field

### Requirement: Client VAT Is Optional Outside The EU

A client's VAT number SHALL be optional. The system SHALL allow a client to be saved and invoiced with an empty VAT number. A VAT number SHALL be required only where the client's country makes it applicable, which is when the country is Poland or another EU member state. That requirement is enforced when KSeF XML is generated (see `ksef-xml-generation`). DOCX and PDF generation SHALL NOT require a VAT number.

#### Scenario: US client without VAT is saved

- **WHEN** a user saves a client with country `US` and an empty VAT
- **THEN** the configuration is saved without error and the client's VAT remains empty

#### Scenario: DOCX and PDF generated without VAT

- **WHEN** DOCX and PDF output is generated for a client with an empty VAT
- **THEN** both files are produced without a validation error

### Requirement: Omit Empty Client VAT From Documents

DOCX and PDF invoices SHALL show the customer's VAT line only when the client has a non-empty VAT number. When the VAT number is empty, the customer block SHALL contain no VAT line and no placeholder such as `N/A`.

#### Scenario: Client with VAT

- **WHEN** a DOCX or PDF invoice is generated for a client with VAT `DE123456789`
- **THEN** the customer block contains the line `VAT: DE123456789`

#### Scenario: Client without VAT

- **WHEN** a DOCX or PDF invoice is generated for a client with an empty VAT
- **THEN** the customer block contains the client's names and addresses and no VAT line

### Requirement: Infer Missing Country From EU VAT Prefix On Load

When a configuration is loaded and a client has no `country`, the system SHALL fill it in from the client's VAT number only if that number starts with an EU VAT country prefix. The prefix SHALL be mapped to its ISO code: `EL` becomes `GR`, and every other EU member-state prefix maps to itself. The Northern Ireland prefix `XI` SHALL NOT be mapped. If the VAT number is empty or has no EU prefix, the country SHALL stay empty. The system SHALL NOT fill in a default country such as `PL`. A country already present in the configuration SHALL never be overwritten.

#### Scenario: Polish VAT prefix

- **WHEN** a configuration is loaded containing a client with no country and VAT `PL9999999999`
- **THEN** the client's country is `PL`

#### Scenario: Greek VAT prefix

- **WHEN** a configuration is loaded containing a client with no country and VAT `EL123456789`
- **THEN** the client's country is `GR`

#### Scenario: No usable prefix

- **WHEN** a configuration is loaded containing a client with no country and VAT `N/A`
- **THEN** the client's country remains empty

#### Scenario: Explicit country is kept

- **WHEN** a configuration is loaded containing a client with country `US` and VAT `PL9999999999`
- **THEN** the client's country remains `US`
