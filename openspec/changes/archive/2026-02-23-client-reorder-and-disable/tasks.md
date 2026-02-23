## 1. Model & Config

- [x] 1.1 Add `Enabled` property (default `true`) to `ClientConfig` model
- [x] 1.2 Update `ConfigManager.FromTomlTable` to read `enabled` field from TOML (default `true` if missing)
- [x] 1.3 Update `ConfigManager.ToTomlString` to write `enabled` field for each client

## 2. Client Reordering

- [x] 2.1 Add "Move Up" button to `ClientListView` that swaps the selected client with the one above it
- [x] 2.2 Add "Move Down" button to `ClientListView` that swaps the selected client with the one below it
- [x] 2.3 After a swap, refresh the ListView source and keep selection on the moved client

## 3. Client Disable/Enable

- [x] 3.1 Add "Disable"/"Enable" toggle button to `ClientListView` that flips the selected client's `Enabled` flag
- [x] 3.2 Update the toggle button label dynamically based on the selected client's current `Enabled` state
- [x] 3.3 Render disabled clients with Unicode strikethrough (U+0336 combining overlay per character) in the ListView source
- [x] 3.4 Ensure new clients added via "Add" default to `Enabled = true`

## 4. Create Invoice Filtering

- [x] 4.1 Filter `_config.Clients` to only enabled clients when building the radio group in `CreateInvoiceView`
- [x] 4.2 Maintain a filtered client list so radio index maps correctly back to the actual `ClientConfig` instance
- [x] 4.3 Handle edge case where all clients are disabled (show message, no radio options)

## 5. Verification

- [x] 5.1 Build the project and verify no compilation errors
- [x] 5.2 Manual smoke test: reorder clients, save, restart — order persists
- [x] 5.3 Manual smoke test: disable a client, verify strikethrough in list and absence from Create Invoice view
