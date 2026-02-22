## Why

There is no explicit way to exit the application from the menu. Users must know the Ctrl+Q shortcut or close the terminal. An Exit menu item makes the app more discoverable and user-friendly.

## What Changes

- Add an "Exit" menu item to the Invoice menu with a Ctrl+Q shortcut hint
- The item calls `Application.RequestStop()` to cleanly shut down the app

## Capabilities

### New Capabilities
- `exit-menu-item`: Adds an Exit menu item to the Invoice menu for closing the application

### Modified Capabilities
<!-- None — no existing spec-level behavior is changing -->

## Impact

- `src/Invoicer/Tui/InvoicerApp.cs` — menu bar definition gains one new MenuItem
- No new dependencies, no API changes
