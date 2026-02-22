## Why

The `MonthOffsetRule` setting (`early_previous` / `early_current`) controls which month an invoice targets based on the invoice date, but it's exposed in the client details UI as a free-text TextField. Users must type the exact string value, with no discoverability or validation. A radio button set makes the two options visible and prevents invalid input.

## What Changes

- Replace the "Month Rule:" TextField (field index 12) in `ClientListView` with a `RadioGroup` control offering two labeled options: "Early → Previous Month" and "Early → Current Month"
- Map radio selection to/from the `MonthOffsetRule` string values (`early_previous`, `early_current`) in the load/save logic
- Remove "Month Rule:" from the `_fieldLabels` array and the generic `_fields` TextField loop (reduce loop to 12 fields)
- Add the RadioGroup as a standalone control positioned after the last TextField

## Capabilities

### New Capabilities

- `month-rule-radio`: RadioGroup UI control in client details for selecting the invoice month offset rule

### Modified Capabilities

_(none — no existing specs)_

## Impact

- **Code**: `src/Invoicer/Tui/Views/ClientListView.cs` — form layout, field load/save logic
- **No model changes**: `ClientConfig.MonthOffsetRule` remains a `string`; config serialization unchanged
- **No breaking changes**: the two valid values and their semantics are unchanged
