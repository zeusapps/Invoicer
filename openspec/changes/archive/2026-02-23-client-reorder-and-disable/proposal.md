## Why

Users with multiple clients need to control which clients appear in the invoice creation form and in what order. Currently, clients are shown in the fixed order they were added, and the only way to remove a client from the list is to delete it entirely — losing all configuration. Users need a non-destructive way to hide inactive clients and reorder active ones to match their workflow priority.

## What Changes

- Add an `enabled` boolean flag to client configuration (defaults to `true` for backward compatibility)
- Add "Move Up" / "Move Down" buttons to the client list view so users can reorder clients
- Display disabled clients with strikethrough text (e.g., `~~CLIENT~~`) in the client list sidebar
- Add a "Disable" / "Enable" toggle button in the client list view
- Filter out disabled clients from the client radio selection on the Create Invoice view
- Persist client order and enabled state in `config.toml`

## Capabilities

### New Capabilities
- `client-ordering`: Ability to reorder clients via Move Up/Down controls, with order persisted in config
- `client-disable`: Ability to enable/disable clients with visual strikethrough for disabled entries, filtering disabled clients from invoice creation

### Modified Capabilities
<!-- No existing specs are being modified -->

## Impact

- **Models**: `ClientConfig` — add `Enabled` boolean property
- **Config**: `ConfigManager` — read/write `enabled` field in TOML serialization/deserialization
- **TUI**: `ClientListView` — add Move Up, Move Down, and Disable/Enable buttons; render disabled clients with strikethrough
- **TUI**: `CreateInvoiceView` — filter `_config.Clients` to only show enabled clients in the radio group
