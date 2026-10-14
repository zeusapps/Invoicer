# Proposal

## Why

KSeF XML currently omits the service period even though the app calculates a service month and displays it in customer documents. Recording the actual period makes monthly services clear when invoicing before or after that month, especially December services invoiced in January, and manual dates accommodate exceptions.

## What Changes

- Derive an inclusive service-period start and end from the existing client month rule, including month lengths, leap years and year transitions.
- Add Automatic and Custom service-period modes to invoice creation, with editable From/To date fields and calendar pickers in Custom mode.
- Use the same resolved period in the preview, KSeF XML, DOCX and PDF. Keep the existing month/year text for one complete calendar month and show explicit dates for other ranges in customer documents.
- Write `Fa/OkresFa/P_6_Od` and `P_6_Do` in FA(3) schema order while keeping the invoice issue date separate.
- Derive the default exchange-rate date from the earlier of the invoice date and the selected period end, preserving manual rate-date overrides.
- Reject invalid or reversed periods before producing any selected output or changing numbering/preferences.

## Capabilities

### New Capabilities

- `invoice-service-period`: Automatic month boundaries, custom date selection, shared period validation and consistent service-period presentation across invoice outputs.

### Modified Capabilities

- `ksef-xml-generation`: Emit the resolved service period through `OkresFa` and validate the FA(3) date bounds before writing XML.
- `exchange-rate`: Use the resolved service-period end for the default rate date and retain manual overrides when date dependencies change.

## Impact

- Invoice model/factory and `InvoiceGeneration.CreateAndGenerate` gain optional period inputs without requiring existing callers or TOML configuration to migrate.
- `CreateInvoiceView` gains period controls and coordinates preview, date validation and rate lookup state.
- Shared preparation and document text, both customer-document generators, the KSeF document mapping and `ExchangeRateRules` consume the same period.
- Extend the existing model, date-rule, UI, document and vendored FA(3) schema tests; document the controls in README.
- Use the installed Terminal.Gui 2.0.0 date controls; no new dependency or schema version is needed. Advance-payment invoice types, payment receipt tracking and historical invoice correction remain outside this change.
