## Context

The client details form in `ClientListView.cs` renders all 13 fields as `TextField` controls in a generic loop. The last field ("Month Rule:", index 12) accepts free-text input for `MonthOffsetRule`, which only has two valid values: `early_previous` and `early_current`. There is no input validation — users can type arbitrary strings, which fall through to the default branch in `Invoice.CalculateServiceMonth()`.

Terminal.Gui v2 provides a `RadioGroup` control that accepts a list of string labels and exposes `SelectedItem` as an integer index, which fits this two-option scenario.

## Goals / Non-Goals

**Goals:**
- Replace the Month Rule TextField with a RadioGroup showing two human-readable options
- Map between radio index and the `MonthOffsetRule` string value in load/save logic
- Keep the form layout consistent — the radio group occupies the same vertical position

**Non-Goals:**
- Changing the `MonthOffsetRule` field type on `ClientConfig` (remains `string`)
- Adding validation or migration for existing config files (both values already valid)
- Refactoring the rest of the form fields away from the generic loop

## Decisions

**1. Use `RadioGroup` rather than a `ComboBox` or two `CheckBox` controls**

RadioGroup is the idiomatic Terminal.Gui control for mutually exclusive options. ComboBox adds unnecessary dropdown complexity. Two CheckBoxes would require manual mutual-exclusion logic.

**2. Reduce the `_fieldLabels` / `_fields` array to 12 entries and add RadioGroup separately**

The generic loop creates TextFields. Rather than special-casing index 12 inside the loop, remove "Month Rule:" from the array and add the RadioGroup as a standalone field after the loop. This is simpler and avoids type-mixing in the `_fields` array.

**3. Reorder fields for logical grouping**

While removing the Month Rule TextField, reorder the remaining fields into logical groups: Identity (Key, Name, Name UA) → Address → Financial (Currency, Default Amt, VAT, VAT Rate) → Service (Service Desc, Service UA) → Invoice (Prefix, Month Rule radio). This places Currency and Default Amt adjacent to each other.

**4. Map by index: 0 → `early_previous`, 1 → `early_current`**

A simple string array `["early_previous", "early_current"]` maps radio index to config value. Reverse lookup via `Array.IndexOf` for loading. Default to index 0 if the value is unrecognized.

## Risks / Trade-offs

**[Risk] Existing config has an unrecognized MonthOffsetRule value** → Defaults to index 0 (`early_previous`), which matches the current fallback behavior in `CalculateServiceMonth()`. No data loss.

**[Trade-off] Label text vs raw value** → Radio labels will be human-readable (e.g., "Early → Previous Month") rather than raw config strings. The mapping array handles translation. Slightly more code but much better UX.
