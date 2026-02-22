## Context

The app's menu bar is defined in `InvoicerApp.cs` (lines 41-64). The Invoice menu currently has only "Create New". Terminal.Gui v2 handles Ctrl+Q as a built-in quit shortcut, but there is no visible menu item for it.

## Goals / Non-Goals

**Goals:**
- Provide a discoverable Exit option in the menu

**Non-Goals:**
- Confirmation dialog before exit
- Unsaved-changes detection

## Decisions

**Place Exit in the Invoice menu** rather than adding a new File menu.
- Rationale: The Invoice menu is the leftmost menu and already serves as the primary action menu. Adding a File menu just for Exit would be unnecessary structure. Exit goes at the bottom of the Invoice menu, separated logically as the last item.

**Use `Application.RequestStop()`** to trigger shutdown.
- Rationale: This is the standard Terminal.Gui v2 method for cleanly ending the run loop. The existing finally block in `Run()` already calls `Application.Shutdown()`.

**Show "Ctrl+Q" as the shortcut hint** on the menu item.
- Rationale: Ctrl+Q is Terminal.Gui's built-in quit shortcut. Displaying it teaches users the keyboard alternative.

## Risks / Trade-offs

No meaningful risks — single line addition to an existing menu, using a standard framework API.
