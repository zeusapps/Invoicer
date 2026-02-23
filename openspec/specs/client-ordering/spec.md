## ADDED Requirements

### Requirement: User can move a client up in the list
The system SHALL allow the user to move the currently selected client one position up in the client list via a "Move Up" button in the ClientListView.

#### Scenario: Move client up from non-first position
- **WHEN** the user selects a client that is not the first in the list and clicks "Move Up"
- **THEN** the selected client swaps position with the client immediately above it, and the selection follows the moved client

#### Scenario: Move Up on first client
- **WHEN** the user selects the first client in the list and clicks "Move Up"
- **THEN** nothing happens; the list remains unchanged

### Requirement: User can move a client down in the list
The system SHALL allow the user to move the currently selected client one position down in the client list via a "Move Down" button in the ClientListView.

#### Scenario: Move client down from non-last position
- **WHEN** the user selects a client that is not the last in the list and clicks "Move Down"
- **THEN** the selected client swaps position with the client immediately below it, and the selection follows the moved client

#### Scenario: Move Down on last client
- **WHEN** the user selects the last client in the list and clicks "Move Down"
- **THEN** nothing happens; the list remains unchanged

### Requirement: Client order persists across saves
The system SHALL persist the current client order in `config.toml` when the user clicks "Save All". The TOML `[[clients]]` array order SHALL reflect the displayed list order.

#### Scenario: Reorder and save
- **WHEN** the user reorders clients and clicks "Save All"
- **THEN** the `config.toml` file reflects the new client order in its `[[clients]]` array

#### Scenario: Reload after save preserves order
- **WHEN** the application is restarted after a reorder-and-save
- **THEN** the client list displays in the previously saved order
