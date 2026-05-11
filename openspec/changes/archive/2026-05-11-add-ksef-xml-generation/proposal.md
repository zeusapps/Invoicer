## Why

The application currently supports DOCX and PDF invoice outputs but cannot generate KSeF XML, which is required for compliance in Polish e-invoicing workflows. Adding KSeF XML export now enables users to produce legally required structured invoice files from the same invoice data.

## What Changes

- Add a new invoice output mode that generates KSeF-compatible XML files.
- Extend invoice generation flow so XML can be produced alongside or instead of existing formats, based on user configuration.
- Map existing invoice, supplier, and client data fields to KSeF XML structure and validate required fields before export.
- Define output naming/location behavior for XML files in line with existing output configuration.
- Use provided manually prepared XML examples as reference fixtures for expected document shape during implementation and verification.

## Capabilities

### New Capabilities

- `ksef-xml-generation`: Generate KSeF-compliant XML invoices from application invoice data, including required structure, field mapping, and file output behavior.

### Modified Capabilities

- None.

## Impact

- Affected code:
  - `src/Invoicer/Generation/` (new XML generator and wiring into generation pipeline)
  - `src/Invoicer/Models/` (possible model/validation updates for required KSeF fields)
  - `src/Invoicer/Tui/` (format selection and validation messaging in UI)
  - `src/Invoicer/Config/` (persist output format selection/options if needed)
- Dependencies/systems:
  - Potential addition of XML serialization/validation utility if current .NET APIs are insufficient
  - Output and tests should be cross-checked against provided sample files:
    - `tests/Invoicer.Tests/TestData/Wersja_robocza_20260511113726.xml`
    - `tests/Invoicer.Tests/TestData/Wersja_robocza_20260429065615.xml`
