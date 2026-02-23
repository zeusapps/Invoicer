## Context

The Invoicer app stores clients as a `[[clients]]` TOML array in `config.toml`. The `ClientConfig` model has no concept of ordering (order is implicit from array position) or enabled/disabled state. The `ClientListView` displays clients in a `ListView` with Add/Delete/Save buttons. The `CreateInvoiceView` shows all clients in a `RadioGroup` for selection.

Terminal.Gui v2's `ListView` does not natively support strikethrough text styling, but it does support custom rendering via source collections with formatted strings. Unicode strikethrough combining characters (U+0336) can be applied per-character to simulate strikethrough in terminal output.

## Goals / Non-Goals

**Goals:**
- Allow users to reorder clients with persistent ordering
- Allow users to disable/enable clients without deleting them
- Visually distinguish disabled clients with strikethrough in the client list
- Hide disabled clients from the invoice creation form

**Non-Goals:**
- Drag-and-drop reordering (out of scope for TUI)
- Archiving or soft-delete with undo history
- Per-client visibility settings beyond a single enabled/disabled toggle

## Decisions

### 1. Enabled flag on ClientConfig

Add `public bool Enabled { get; set; } = true;` to `ClientConfig`. Defaults to `true` so existing configs without the field remain fully functional (backward compatible). Serialized as `enabled = true/false` in TOML.

**Alternative considered**: A separate "disabled clients" list — rejected because it splits client data across two locations and complicates reordering.

### 2. Reordering via list swap

Move Up/Down buttons swap the selected client with its neighbor in `_config.Clients` (the `List<ClientConfig>`). The TOML array order is the canonical order, so saving after reorder persists the new order automatically.

**Alternative considered**: An explicit `order` integer field on each client — rejected as unnecessary complexity since TOML array position already encodes order.

### 3. Strikethrough via Unicode combining characters

Apply Unicode combining long stroke overlay (U+0336) after each character of disabled client keys in the ListView source. This works across most modern terminals without requiring Terminal.Gui custom cell rendering.

**Alternative considered**: Prefix with `[X]` or `(disabled)` marker — functional but less visually distinct. The user specifically requested strikethrough styling.

### 4. Button placement in ClientListView

Add "Move Up", "Move Down", and "Enable/Disable" buttons to the bottom button bar alongside existing Add, Delete, Save buttons. The Enable/Disable button label toggles based on the selected client's current state.

### 5. Filtering in CreateInvoiceView

Filter `_config.Clients.Where(c => c.Enabled)` when building the radio group labels and when resolving the selected index back to a client. Store the filtered list to maintain correct index mapping.

## Risks / Trade-offs

- **[Unicode strikethrough rendering]** → Some terminal emulators may not render combining characters correctly. Mitigation: the feature degrades gracefully — worst case the text displays without strikethrough but the client still shows in the list.
- **[Reorder persistence]** → Reordering modifies the `config.toml` array order on save. Mitigation: this is the expected behavior, and Save is already explicit (not auto-save).
- **[Filtered radio index mismatch]** → The `CreateInvoiceView` radio index no longer maps 1:1 to `_config.Clients` index. Mitigation: maintain a separate filtered client list and index through it.
