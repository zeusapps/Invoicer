# Design

## Context

See `proposal.md` for motivation and the three delta specs for the behavior contract. This design is needed because the period crosses the model, creation form, rate lookup and all three generators.

`Invoice.Create` currently calculates `ServiceMonth` through `CalculateServiceMonth`; the stored value identifies a month but retains a day within it. `CreateInvoiceView` shows a read-only month label and recalculates the month independently for its preview and rate date. Both customer generators format `ServiceMonth` directly. `KsefInvoiceBody` carries only `InvoiceDate`, and `WriteInvoiceBody` writes `P_2` immediately before monetary summaries.

`InvoicePreparation.Prepare` already runs shared validation for the orchestration path and direct generator entry points. `InvoiceGeneration.CreateAndGenerate` updates numbering and preferences only after successful generation. These boundaries allow period validation to fail before any output is touched.

`ExchangeRateRules.RelevantDate(DateTime, DateTime)` currently interprets its second argument as a service month, expands it to month end and returns the earlier date. Reusing that signature with a period end would silently change its meaning. The creation form already cancels superseded lookups, but currently resets the rate date after every invoice-date change and does not track manual rate-date ownership.

The pinned Terminal.Gui 2.0.0 package's installed XML documentation exposes `DateField.Date`, `DateField.DateChanged`, `DateField.Culture`, `DatePicker.Date` and `DatePicker.Culture`. No package change is required for date editing or calendar selection.

## Goals / Non-Goals

**Goals:**

- Resolve one validated, date-only period and pass it through the generation request.
- Keep existing monthly requests and direct invoice construction compatible.
- Make period and rate-date state transitions explicit and independently testable.
- Use existing schema validation and customer-document formatting infrastructure.

**Non-Goals:**

- New client month rules, saved custom-date defaults or an invoice-history database.
- Advance-payment (`ZAL`) invoice generation, payment receipt dates, changes to tax treatment or a general accounting decision engine. Billing before a service month does not itself introduce another invoice type.
- Per-line periods, corrections of previously issued invoices or KSeF submission.
- Redesigning invoice number, payment due date, output folder or filename behavior.

## Decisions

### 1. Resolve explicit endpoints with a compatible service-month fallback

Add nullable `ServicePeriodStart` and `ServicePeriodEnd` to `Invoice`. Append optional endpoint parameters to `Invoice.Create` and `InvoiceGeneration.CreateAndGenerate`, preserving existing positional argument order, including the latter's existing clock argument. Both absent means the first and last day of the stored `ServiceMonth`; both present means an explicit override. A single endpoint is invalid. Keep `ServiceMonth` and `CalculateServiceMonth` as the existing automatic default source.

Use a small immutable resolved-period value containing normalized start/end calendar dates, with one pure resolver for both UI and generation. Continue using the project's `DateTime` convention at interfaces and strip time components; do not convert dates through a timezone. Reject uninitialized date values and an uninitialized fallback month. Calendar-month expansion must handle boundaries without throwing uncontrolled `AddMonths` exceptions.

Shared preparation resolves and validates the period once and carries the immutable value alongside the prepared tax inputs. All prepared generators consume it. Standalone generators still enter through shared preparation. XML-specific bounds stay in KSeF preflight so valid documents outside those bounds remain possible without XML.

Alternatives: replacing `ServiceMonth` outright forces existing callers/tests to migrate; deriving endpoints independently in each generator allows drift; permanently storing two automatic dates alongside `ServiceMonth` creates competing default sources. Nullable explicit endpoints distinguish the override from the established month input without a TOML migration.

### 2. Automatic/Custom is form state, with compact date controls

Replace the read-only service-month row with an Automatic/Custom selector and From/To rows. Use `DateField` controls for keyboard entry and a Pick button beside each field that opens a dialog containing `DatePicker` with explicit acceptance and cancellation. Configure the controls' culture to display `dd.MM.yyyy`; do not change global application culture. Automatic dates remain visible but their editing and Pick buttons are disabled.

Keep the selection mode, custom endpoints and last valid invoice date in a small form state helper, or equivalent isolated state. A programmatic-update guard prevents date-control callbacks from treating automatic population as user edits or triggering duplicate lookups. A single refresh path validates current input, resolves the period, synchronizes controls, updates rate dependencies and builds the preview.

Default selection and client changes start in Automatic. Custom starts with the current automatic endpoints and then preserves edits across invoice-date changes. Returning to Automatic clears overrides. Successful generation resets the form's period for the next invoice; failed validation leaves it editable for correction. Period controls do not write client configuration.

Treat custom ranges as inclusive, with no restriction to one month and with equal endpoints allowed. This is the chosen default for manual exceptions. Replace the invoice-date parser's silent `DateTime.Today` fallback with an explicit success/error path when deriving or generating periods. Invalid or incomplete typed text must not be accepted through a date control's stale underlying value: commit/validate current field input before preview, picker acceptance and generation, or prevent invalid entry visibly. Do not silently substitute defaults.

Alternatives: always-editable fields make it unclear whether subsequent invoice-date changes should overwrite them; two inline calendars consume excessive terminal space; free text alone omits the requested calendar selection. The native compact fields plus dialogs fit the existing scrolling form.

Implementation inspection of the pinned package found that `DateField` cancels invalid text changes, clamps some numeric keyboard edits, and uses the thread's current culture rather than keeping a per-control parsing culture. Keep native fields as confirmed during implementation: a small subclass observes cancelled edits, rejects invalid prospective numeric/delete edits with a visible error, and scopes native parsing operations to the required date culture with immediate restoration. Accepted keyboard edits, paste and calendar dates retain native behavior. The calendar's internal date display is disabled so selection occurs through the calendar; its navigation buttons cancel dialog acceptance to prevent unintended submission. Native terminal interactions are checked in a standalone review harness because this installed test platform has incompatible attribute metadata when the toolkit scans loaded assemblies at application initialization.

### 3. Use a shared customer-document formatter and explicit preview endpoints

Add service-period formatting to `InvoiceText` using the resolved value, and call it from both `DocxGenerator` and `PdfGenerator`. A period from the first to the actual last day of a single month retains the current `MMMM yyyy` rendering, regardless of selection mode. Every other period uses invariant `dd.MM.yyyy - dd.MM.yyyy` text with the existing bilingual label. Preview always shows the exact endpoints separately from the issue date, using the same resolution logic.

Alternatives: always printing endpoints would change every existing monthly document; keeping month-only text for partial periods would misrepresent the selected dates. The formatter preserves established monthly presentation and exposes exceptions accurately.

### 4. Serialize OkresFa at the existing invoice-body boundary

Carry the prepared period in `KsefInvoiceBody`. In `WriteInvoiceBody`, write `OkresFa` after `P_2` and before `P_13_*`, with `P_6_Od` followed by `P_6_Do`, both formatted invariantly as `yyyy-MM-dd`. Do not write `P_6` or line-level `P_6A`. Retain `P_1` as the independent issue date.

The vendored `tests/Invoicer.Tests/TestData/Schema/schemat.xsd` defines `P_6` and `OkresFa` in an optional choice and both endpoints as `TDataT`, whose inclusive bounds are 2006-01-01 and 2050-01-01. Add named preflight errors for dates outside those bounds; shared ordering/missing-date validation applies to every output. Retain the existing tax, currency, metadata and generation-clock behavior.

References: [official FA(3) schema](https://crd.gov.pl/wzor/2025/06/25/13775/) and [Ministry of Finance FA(3) guide, page 45](https://ksef.podatki.gov.pl/media/jknpcymf/broszura-informacyjna-dotyczaca-struktury-logicznej-fa-3-04032026.pdf#page=45). The schema describes this period for the periodic-settlement cases listed in its annotation; this feature represents the user's agreed service periods rather than deciding their accounting treatment.

Alternatives: a description-only date range loses the structured field requested; using `P_6` loses the start; emitting both `P_6` and `OkresFa` violates the schema choice.

### 5. Derive rates from the resolved end without repurposing a month argument

Add an explicitly named period-end rule/helper that returns `min(invoiceDate.Date, servicePeriodEnd.Date)`. Route invoice-based and UI calculations through it. Preserve the existing month-based `RelevantDate(DateTime, DateTime)` helper as a compatibility wrapper that expands its service-month argument before delegating. This keeps existing callers and regression tests meaningful.

Track whether the rate-date field is automatic or manually overridden. Programmatic defaults must not mark it manual. A changed user-entered date is manual; entering the current derived default again restores automatic ownership without adding another form control. Preserve a manual rate date when invoice or period dates change, and reset ownership on client change or successful preparation of the next invoice.

Coordinate lookups by effective currency and rate date. If that key changes, cancel the old request, clear rate/source provenance and start a new lookup only when inputs are valid. If it stays the same, retain the existing rate, including a manually entered rate. Client changes always follow the existing discard-and-refresh rule even with a matching key. Keep the current cancellation/reference check on UI-thread result application. Invalid period input cancels outstanding dependent lookups and prevents generation without launching a lookup from substituted dates.

Alternatives: blindly resetting the rate date loses the user's explicit override; retaining a rate after its effective date changes carries obsolete provenance; passing the period end to the existing month API would round a partial period back to month end.

## Risks / Trade-offs

- [Duplicated month and endpoint representations] -> Treat explicit endpoints as overrides and make all consumers use the shared resolver/prepared value.
- [Period edits overwrite user choices or trigger callbacks recursively] -> Explicit mode/ownership state and guarded programmatic control updates; cover transitions in tests.
- [Late NBP results overwrite rates for new dates] -> Cancel obsolete requests and retain the existing identity check before applying results.
- [Date controls retain a prior valid value after invalid text] -> Validate committed input rather than silently trusting stale control state; verify keyboard behavior during the TUI check.
- [Extra rows exceed a small terminal] -> Retain `Scrolling.EnableVertical` and test layout/focus with the rate block both visible and hidden.
- [Schema-valid structure with incorrect dates] -> Assert exact endpoint values as well as schema validity, including January/December and partial periods.
- [Date bounds prevent old/future documents unnecessarily] -> Apply FA(3) bounds only when XML is requested; shared validation enforces actual dates and ordering for all formats.

## Migration Plan

No persisted configuration changes are required. Existing requests without an override keep their month rules and customer-document month/year presentation. New XML contains the additional structured period.

Implement and validate in the repository, then distribute through the normal release process. Rolling back restores prior generation behavior without migrating configuration or rewriting already generated invoices. Existing invoice files are not modified as part of deployment; replacement still occurs only through a deliberate generation request.
