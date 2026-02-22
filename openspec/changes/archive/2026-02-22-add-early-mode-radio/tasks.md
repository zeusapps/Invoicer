## 1. Remove Month Rule from generic field loop

- [x] 1.1 Remove "Month Rule:" from the `_fieldLabels` array (reduce to 12 entries)
- [x] 1.2 Update `_fields` array size to 12 (loop now creates 12 TextFields)
- [x] 1.3 Reorder fields into logical groups: Identity → Address → Financial (Currency, Default Amt, VAT, VAT Rate) → Service → Prefix
- [x] 1.4 Update `SaveFieldsToClient()` and `LoadClientIntoFields()` index mappings to match new field order

## 2. Add RadioGroup control

- [x] 2.1 Add a `RadioGroup` field (`_monthRuleRadio`) and a string array mapping (`_monthRuleValues = ["early_previous", "early_current"]`)
- [x] 2.2 Add a "Month Rule:" label at Y = 24 (after last TextField) and the RadioGroup at X = 16, Y = 24 with labels "Early → Previous Month" and "Early → Current Month"
- [x] 2.3 Add the label and RadioGroup to `detailFrame`

## 3. Update load/save logic

- [x] 3.1 In `LoadClientIntoFields()`, set `_monthRuleRadio.SelectedItem` based on `client.MonthOffsetRule` using `Array.IndexOf`, defaulting to 0
- [x] 3.2 In `SaveFieldsToClient()`, set `client.MonthOffsetRule` from `_monthRuleValues[_monthRuleRadio.SelectedItem]`
- [x] 3.3 Remove the old `_fields[12]` references from both methods

## 4. Verify

- [x] 4.1 Build the project (`dotnet build`) and confirm no errors
