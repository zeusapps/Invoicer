## ADDED Requirements

### Requirement: Month offset rule is displayed as a radio button set
The client details form SHALL display the month offset rule as a `RadioGroup` control with two options instead of a free-text TextField. The options SHALL be labeled "Early → Previous Month" and "Early → Current Month".

#### Scenario: Radio group is visible in client details
- **WHEN** the user views the client details form
- **THEN** a "Month Rule:" label and a RadioGroup with two options ("Early → Previous Month", "Early → Current Month") are displayed after the last text field

### Requirement: Radio selection maps to MonthOffsetRule config value
The RadioGroup SHALL map its selected index to the `MonthOffsetRule` string value: index 0 → `early_previous`, index 1 → `early_current`.

#### Scenario: Selecting first option sets early_previous
- **WHEN** the user selects "Early → Previous Month"
- **THEN** the client's `MonthOffsetRule` is set to `early_previous`

#### Scenario: Selecting second option sets early_current
- **WHEN** the user selects "Early → Current Month"
- **THEN** the client's `MonthOffsetRule` is set to `early_current`

### Requirement: Radio selection loads from existing client config
When a client is selected in the list, the RadioGroup SHALL reflect the client's current `MonthOffsetRule` value. If the value is unrecognized, the RadioGroup SHALL default to index 0 (`early_previous`).

#### Scenario: Loading a client with early_current
- **WHEN** a client with `MonthOffsetRule` = `early_current` is selected
- **THEN** the RadioGroup shows "Early → Current Month" as selected

#### Scenario: Loading a client with unrecognized rule
- **WHEN** a client with an unrecognized `MonthOffsetRule` value is selected
- **THEN** the RadioGroup defaults to "Early → Previous Month"

### Requirement: Radio selection is saved with client data
When the user switches clients or clicks "Save All", the current radio selection SHALL be persisted to the client's `MonthOffsetRule` field.

#### Scenario: Saving after changing the radio selection
- **WHEN** the user changes the radio from "Early → Previous Month" to "Early → Current Month" and clicks "Save All"
- **THEN** the client's `MonthOffsetRule` is saved as `early_current` in the config file
