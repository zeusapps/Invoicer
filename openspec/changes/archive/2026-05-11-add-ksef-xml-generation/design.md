## Context

Invoicer is a .NET 9 TUI application that currently generates invoices as DOCX and PDF from shared invoice domain models (`Invoice`, `SupplierConfig`, `ClientConfig`, output settings). The new capability requires generating KSeF-compatible XML from the same source data without breaking existing flows.

Constraints:

- Existing generation pipeline should remain stable for DOCX/PDF.
- XML output must be deterministic and reproducible for validation and comparison.
- KSeF requires stricter mandatory field presence and canonical structure than current visual document outputs.
- The user provided two manually prepared XML examples that should be used as structural references during implementation and verification.

Stakeholders:

- End users issuing Polish invoices that require KSeF-structured data.
- Maintainers of generation, configuration, and TUI modules.

## Goals / Non-Goals

**Goals:**

- Add a new XML generation path for KSeF while preserving DOCX/PDF behavior.
- Introduce a dedicated mapping layer from domain invoice models to KSeF XML document model.
- Validate required KSeF fields before writing XML and surface actionable errors in TUI flow.
- Reuse existing output configuration semantics (directory/year layout and filename policy).

**Non-Goals:**

- Implementing direct API submission to KSeF services.
- Replacing existing invoice domain model with a KSeF-native model.
- Building a full XSD validator pipeline in the first iteration if required shape/required-field validation already provides sufficient reliability.

## Decisions

1. Add a dedicated `KsefXmlGenerator` in `src/Invoicer/Generation/`.

- Rationale: Keeps responsibilities aligned with existing generator structure (`DocxGenerator`, `PdfGenerator`), minimizing cross-cutting churn.
- Alternatives considered:
  - Expand existing generators with XML branches: rejected due to mixed responsibilities and higher regression risk.
  - Single generic generator abstraction: deferred to avoid broad refactor unrelated to immediate capability.

2. Introduce an intermediate KSeF DTO/document model for serialization.

- Rationale: Separates business/domain naming from schema-specific XML naming and nesting; simplifies evolution if KSeF format changes.
- Alternatives considered:
  - Serialize directly from domain models: rejected because domain objects do not reflect required KSeF nesting and naming.
  - Build XML via string templates: rejected due to fragility and escaping/formatting risks.

3. Use .NET XML APIs (`System.Xml.Linq`/`XmlWriter`) with stable ordering and explicit namespace handling.

- Rationale: No extra dependency required, full control of element order/attributes, straightforward testability.
- Alternatives considered:
  - `XmlSerializer`: possible but less explicit for fine-grained control over namespaces/ordering in complex structures.
  - Third-party XML builders: rejected to avoid new dependency for core output path.

4. Add pre-generation validation for KSeF-required fields.

- Rationale: Fail-fast with clear user feedback in TUI before file emission; avoids producing partially valid XML.
- Alternatives considered:
  - Best-effort generation with warnings: rejected because compliance-oriented output should not silently degrade.

5. Integrate XML format selection in current output flow with backward-compatible defaults.

- Rationale: Preserve existing user experience and config behavior while allowing explicit XML output selection.
- Alternatives considered:
  - Force XML generation always: rejected because it changes existing output expectations and workflow.

## Risks / Trade-offs

- [KSeF structure mismatch against required schema nuance] -> Mitigation: compare generated output against provided reference XML samples and add focused fixture tests for critical sections.
- [Missing required source data in current models/config] -> Mitigation: define explicit required-field checks and guide users via TUI validation messages.
- [Output option complexity in TUI/config] -> Mitigation: keep format selection simple and aligned with existing patterns; defer advanced mode matrix to future change.
- [Future KSeF format changes] -> Mitigation: isolate schema mapping in dedicated mapper/DTO layer for easier updates.

## Migration Plan

1. Add generator and mapper components without changing existing DOCX/PDF code paths.
2. Extend configuration and TUI format selection to include XML option.
3. Add validation rules and error surface in invoice creation/generation flow.
4. Add tests/fixtures using representative sample XML shape.
5. Release with XML generation opt-in; monitor for validation gaps.

Rollback strategy:

- Disable XML option in selection/config and exclude generator invocation while leaving existing DOCX/PDF paths untouched.

## Open Questions

All initial open questions are resolved for this change iteration:

- Baseline schema version: use FA(3), namespace `http://crd.gov.pl/wzor/2025/06/25/13775/`, `kodSystemowy="FA (3)"`, `wersjaSchemy="1-0E"`, and `WariantFormularza=3`, matching both provided reference XML files.
- Validation strategy in v1: enforce required-field + structure validation in application code; do not block this iteration on full XSD validation.
- Domain data coverage: treat current models as the primary source and add targeted model/config extensions only when a required KSeF field cannot be derived from existing data.
- Output selection behavior: XML is independently selectable in configuration/UI and can be generated alongside DOCX/PDF in the same run.

Follow-up assumptions to verify during implementation:

- If future schema updates are needed, keep version/namespace metadata centralized in one mapping configuration point.
- If strict XSD validation becomes required by users or regulators, add it as a follow-up change without redesigning the generator pipeline.
