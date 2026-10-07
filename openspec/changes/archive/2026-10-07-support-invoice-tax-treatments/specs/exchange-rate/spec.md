# Spec Delta

## MODIFIED Requirements

### Requirement: Tolerate An Unavailable Rate Source

When the NBP rate cannot be retrieved — the service is unreachable, times out, returns an error, or has no data for the requested period — the system SHALL report that plainly, leave the rate fields empty and editable, and SHALL NOT substitute a guessed, cached-as-current or zero rate.

Failure to retrieve a rate SHALL NOT prevent the user from entering a rate by hand or generating DOCX/PDF without XML when shared invoice validation passes. When XML is selected, its exchange-rate validation SHALL complete before any selected file is written. An unresolved or invalid XML rate SHALL refuse the complete selected-output request, preserving existing files and numbering. The user SHALL be able to supply a valid rate or deselect XML and generate otherwise valid customer documents.

#### Scenario: Rate source unreachable

- **WHEN** the NBP service cannot be reached while preparing a foreign-currency invoice
- **THEN** the system reports that the rate could not be retrieved, leaves the rate fields empty and editable, and keeps document-only generation available subject to shared validation

#### Scenario: User supplies the rate after a failed lookup

- **WHEN** the rate could not be retrieved and the user types a valid rate
- **THEN** the invoice is generated using the typed rate when all selected-output validation passes

#### Scenario: Missing rate with mixed outputs

- **WHEN** the rate lookup fails and PDF and XML remain selected without a manually supplied rate
- **THEN** no selected file is created or replaced, numbering stays unchanged, and the error identifies the missing exchange rate

#### Scenario: User chooses documents only after a failed lookup

- **WHEN** the rate lookup fails, the user deselects XML and requests otherwise valid PDF/DOCX
- **THEN** the documents are generated without an exchange rate or rate text
