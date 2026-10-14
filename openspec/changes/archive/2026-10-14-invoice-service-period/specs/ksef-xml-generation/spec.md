# Spec Delta

## ADDED Requirements

### Requirement: Write The Resolved Service Period In OkresFa

Generated FA(3) XML SHALL contain exactly one `Fa/OkresFa` with `P_6_Od` holding the resolved service-period start followed by `P_6_Do` holding its end. Both dates SHALL use invariant `yyyy-MM-dd` format. The element SHALL appear after `P_2` (and after any `WZ` elements when present) and before the monetary summary elements, as required by the FA(3) schema.

The XML SHALL keep the issue date in `Fa/P_1` and SHALL NOT emit `Fa/P_6` alongside `OkresFa` or substitute line-level `P_6A` dates for this period. It SHALL use the supplied invoice's resolved dates without deriving them from the issue date at serialization time. XML for automatic and custom periods SHALL remain valid against the vendored official schema and deterministic with a fixed generation clock.

#### Scenario: September service period

- **WHEN** an invoice dated 5 October 2026 is generated for 1 through 30 September 2026
- **THEN** `P_1` is `2026-10-05`, `OkresFa/P_6_Od` is `2026-09-01`, `OkresFa/P_6_Do` is `2026-09-30`, the elements appear in schema order, no `P_6` or `P_6A` is emitted and the document validates

#### Scenario: December services billed in January

- **WHEN** an invoice dated 5 January 2027 is generated for 1 through 31 December 2026
- **THEN** XML retains the January issue date and December service-period endpoints without changing the invoice number or output naming rules

#### Scenario: Custom period overrides automatic dates

- **WHEN** supplied invoice data selects 15 September through 14 October 2026 as the service period
- **THEN** XML writes `2026-09-15` and `2026-10-14` without replacing them with full-month boundaries, and the document validates

#### Scenario: Fixed-clock generation with service dates

- **WHEN** identical invoice data with a service period is generated twice with the same fixed time source
- **THEN** both XML documents are byte-identical

### Requirement: Validate FA(3) Service Date Bounds Before Writing

When XML is selected, both resolved service dates SHALL satisfy the FA(3) `TDataT` range, inclusive from `2006-01-01` through `2050-01-01`, in addition to shared service-period validation. Out-of-range dates SHALL produce a validation error naming the offending service date before any selected output file is created or replaced. Numbering and saved output preferences SHALL remain unchanged. These schema-specific bounds SHALL NOT restrict otherwise valid DOCX/PDF generation when XML is deselected.

#### Scenario: Period outside the FA(3) range

- **WHEN** a service period ends on 2 January 2050 and XML and PDF are requested
- **THEN** neither file is written, the error identifies the service-period end as outside the FA(3) range and numbering/preferences stay unchanged

#### Scenario: Schema date boundary accepted

- **WHEN** an otherwise valid invoice has a single-day service period on 1 January 2050 and XML is requested
- **THEN** the service dates pass the FA(3) bounds check

#### Scenario: Documents outside schema date bounds

- **WHEN** a valid service period outside the FA(3) date range is supplied and only PDF and DOCX are requested
- **THEN** the service period is accepted without imposing the XML date bounds
