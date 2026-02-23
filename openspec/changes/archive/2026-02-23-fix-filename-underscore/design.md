## Context

Terminal.Gui v2 `Label` treats `_` as a hotkey/mnemonic specifier. The preview label in `CreateInvoiceView` displays dynamic text containing filenames and paths that may include underscores. These underscores get consumed by the mnemonic parser instead of displaying literally.

## Goals / Non-Goals

**Goals:**
- Display underscores literally in the preview label

**Non-Goals:**
- Changing the hotkey behavior of other labels/buttons in the app

## Decisions

Disable hotkey/mnemonic processing on the preview label by setting `HotKeySpecifier = (Rune)0xFFFF`. This makes the label render all characters literally, including underscores, without any string escaping needed.

## Risks / Trade-offs

- **[No hotkey on preview label]** → The preview label cannot have a keyboard hotkey. This is acceptable since it's a read-only display label.
