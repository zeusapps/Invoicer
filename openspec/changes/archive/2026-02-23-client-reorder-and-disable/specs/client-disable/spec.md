## ADDED Requirements

### Requirement: User can disable a client
The system SHALL allow the user to disable the currently selected client via a toggle button in the ClientListView. The button SHALL display "Disable" when the selected client is enabled, and "Enable" when the selected client is disabled.

#### Scenario: Disable an enabled client
- **WHEN** the user selects an enabled client and clicks "Disable"
- **THEN** the client's `enabled` flag is set to `false`, the button label changes to "Enable", and the client list refreshes to show the client with strikethrough text

#### Scenario: Enable a disabled client
- **WHEN** the user selects a disabled client and clicks "Enable"
- **THEN** the client's `enabled` flag is set to `true`, the button label changes to "Disable", and the client list refreshes to show the client without strikethrough text

### Requirement: Disabled clients display with strikethrough
The system SHALL render disabled clients in the ClientListView sidebar with Unicode strikethrough styling (combining long stroke overlay U+0336 applied to each character of the client key).

#### Scenario: Disabled client rendering
- **WHEN** the client list is displayed and a client has `enabled = false`
- **THEN** that client's key appears with strikethrough characters in the list

#### Scenario: Enabled client rendering
- **WHEN** the client list is displayed and a client has `enabled = true`
- **THEN** that client's key appears normally without strikethrough

### Requirement: Enabled flag defaults to true
The `ClientConfig` model SHALL include an `Enabled` property that defaults to `true`. Existing config files without an `enabled` field SHALL treat the client as enabled.

#### Scenario: New client defaults to enabled
- **WHEN** a new client is added
- **THEN** the client's `Enabled` property is `true`

#### Scenario: Legacy config without enabled field
- **WHEN** the application loads a `config.toml` that has no `enabled` field on a client
- **THEN** the client is treated as enabled (`Enabled = true`)

### Requirement: Disabled clients are excluded from invoice creation
The CreateInvoiceView client radio group SHALL only display clients where `Enabled` is `true`. Disabled clients MUST NOT appear as selectable options for invoice generation.

#### Scenario: Only enabled clients shown in create invoice
- **WHEN** the Create Invoice view is opened and some clients are disabled
- **THEN** only enabled clients appear in the client radio selection

#### Scenario: All clients disabled
- **WHEN** the Create Invoice view is opened and all clients are disabled
- **THEN** no clients appear in the radio selection and the preview shows a message indicating no clients are available

### Requirement: Enabled state persists in config
The system SHALL serialize the `enabled` field for each client in the `[[clients]]` TOML array when saving configuration.

#### Scenario: Save disabled client
- **WHEN** the user disables a client and clicks "Save All"
- **THEN** the `config.toml` contains `enabled = false` for that client

#### Scenario: Save enabled client
- **WHEN** the user saves with an enabled client
- **THEN** the `config.toml` contains `enabled = true` for that client
