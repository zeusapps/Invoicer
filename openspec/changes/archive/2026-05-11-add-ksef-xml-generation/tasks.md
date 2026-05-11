## 1. KSeF XML Core Generation

- [x] 1.1 Add `KsefXmlGenerator` in `src/Invoicer/Generation/` and wire it into the invoice generation pipeline without modifying existing DOCX/PDF behavior.
- [x] 1.2 Define an internal KSeF document/DTO model and mapper from `Invoice`, `SupplierConfig`, and `ClientConfig` to required FA(3) structures.
- [x] 1.3 Implement XML writer logic with deterministic element ordering and namespace `http://crd.gov.pl/wzor/2025/06/25/13775/`.
- [x] 1.4 Emit required FA(3) header values (`kodSystemowy="FA (3)"`, `wersjaSchemy="1-0E"`, `WariantFormularza=3`).

## 2. Validation And Output Flow

- [x] 2.1 Implement pre-generation validation for required KSeF fields and return actionable errors when data is missing or invalid.
- [x] 2.2 Extend output format selection to support XML independently and combined with DOCX/PDF in the same run.
- [x] 2.3 Ensure XML output uses existing output directory and naming conventions, including year-based folder layout.
- [x] 2.4 Add centralized configuration/constants for schema version and namespace metadata to simplify future updates.

## 3. TUI And Configuration Integration

- [x] 3.1 Update TUI views to expose XML output selection and preserve current user flow.
- [x] 3.2 Surface validation failures in TUI generation flow with clear, field-level error messaging.
- [x] 3.3 Update config load/save behavior if needed to persist XML output selection options.

## 4. Testing And Verification

- [x] 4.1 Add unit tests for mapping domain data to KSeF structures, including supplier/buyer data and monetary totals.
- [x] 4.2 Add tests for required-field validation failure paths.
- [x] 4.3 Add fixture-based tests comparing generated XML structure and key values against the two provided reference XML samples.
- [x] 4.4 Add deterministic-output test ensuring repeated generation with identical input produces equivalent XML content.
