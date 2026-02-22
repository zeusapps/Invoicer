## ADDED Requirements

### Requirement: Exit menu item in Invoice menu
The Invoice menu SHALL contain an "Exit" menu item as its last entry. The item SHALL display "Ctrl+Q" as its shortcut hint. Activating the item SHALL cleanly stop the application.

#### Scenario: User clicks Exit
- **WHEN** the user opens the Invoice menu and selects "Exit"
- **THEN** the application shuts down cleanly

#### Scenario: Shortcut hint is visible
- **WHEN** the user opens the Invoice menu
- **THEN** the "Exit" item displays "Ctrl+Q" as its shortcut
