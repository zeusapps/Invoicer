# Tasks

## 1. Shared service-period data and validation

- [x] 1.1 Add optional explicit start/end dates and a shared immutable period resolver to the invoice model, retaining the service-month fallback; verify model tests cover both client rules at the day-20/day-21 cutoff, unknown-rule fallback, month lengths, leap February, January/December transitions, single-day and cross-month custom ranges, and normalization of time components.
- [x] 1.2 Append optional period inputs to `Invoice.Create` and `InvoiceGeneration.CreateAndGenerate` without changing existing positional parameters; verify old callers compile and requests with no override retain existing numbering, issue date, paths and automatic service month while explicit dates reach the generated invoice.
- [x] 1.3 Carry the resolved period through shared preparation and reject missing, partial, uninitialized or reversed periods before any output write; verify direct generator calls and orchestrated document-only/combined requests preserve existing file bytes, numbering and saved preferences on failure.

## 2. Customer documents and KSeF XML

- [x] 2.1 Add shared service-period text to `InvoiceText` and consume it in DOCX/PDF; verify formatter tests and generated DOCX text cover full-month presentation, manually selected full months, partial periods, cross-month periods and single days, and inspect a generated PDF to confirm the same text and existing bilingual label.
- [x] 2.2 Carry the prepared period in the KSeF document mapping and write `OkresFa` before monetary summaries, with `P_6_Od` then `P_6_Do`; extend `KsefXmlGeneratorTests` to assert exact dates, independent `P_1`, absence of `P_6`/`P_6A`, schema order, vendored FA(3) validity and fixed-clock determinism for automatic, January/December and custom periods.
- [x] 2.3 Add XML-only `TDataT` bounds checks for both service dates; verify dates on 2006-01-01 and 2050-01-01 pass, dates outside each bound fail before any selected file is replaced, and otherwise valid PDF/DOCX requests outside those bounds still succeed without XML.

## 3. Period selection and calendar controls

- [x] 3.1 Add testable Automatic/Custom form state and replace the read-only service-month row with mode and From/To controls; verify automatic recalculation, Custom initialization, preserved custom dates on issue-date changes, restoration to Automatic, client-switch reset, next-invoice reset and correction after failed validation.
- [x] 3.2 Add compact native date fields and calendar dialogs for each endpoint, with explicit accept/cancel handling and guarded programmatic updates; verify keyboard editing, `dd.MM.yyyy` presentation, calendar acceptance affecting only one date, cancellation preserving the prior date, and disabled editing/pickers in Automatic mode.
- [x] 3.3 Resolve the same period for the preview and generation request and replace the invoice-date parser's silent-today fallback with explicit validation; verify exact preview endpoints match the submitted dates, invalid current input cannot generate using stale date-control values, and invalid issue/period dates report errors without starting dependent lookups.

## 4. Exchange-rate dependencies

- [x] 4.1 Add an explicitly named period-end calculation, route invoice/UI rate defaults through it and preserve the existing month-based helper as a wrapper; extend `ExchangeRateDateTests` to verify existing monthly cases, partial-month ends, periods crossing years, invoice dates before the period and ignored time components.
- [x] 4.2 Track automatic versus manual rate-date ownership and refresh rate data only for changed effective dependencies (except the existing unconditional client reset); verify manual overrides survive invoice/period edits, typing the derived default restores automatic updates, start-only edits preserve rates, client/next-invoice resets work, and changed lookup dates clear manual or fetched rates and their source metadata.
- [x] 4.3 Preserve cancellation and lookup-identity checks when period edits trigger new lookups; verify with controlled local lookup responses that superseded results cannot overwrite the current rate, invalid dates start no request, PLN clients make no request, and existing unavailable-rate/manual-entry behavior still works.

## 5. Integration and documentation

- [x] 5.1 Verify one generation request uses matching periods across preview, XML, DOCX and PDF for September services billed in October, December services billed in January and a partial/cross-month custom range; assert exact XML/DOCX dates and inspect the corresponding PDFs rather than relying on file existence alone.
- [x] 5.2 Update README to describe Automatic/Custom periods, From/To calendar selection, override/reset behavior and the independent invoice/rate dates; verify the examples match the shipped controls and neither new TOML keys nor changed month rules are documented.
- [x] 5.3 Run `dotnet build` and `dotnet test tests/Invoicer.Tests/Invoicer.Tests.csproj`, then check the TUI with keyboard navigation and both hidden/visible rate blocks in a small terminal; verify the suite passes and the period controls, dialogs, preview and Generate action remain reachable through scrolling.
