## Why

The invoice preview in `CreateInvoiceView` loses underscore characters from the filename pattern. A pattern like `{date}_{client}_PL` renders as `20260223SAMPLE_PL` instead of `20260223_SAMPLE_PL`. This is because Terminal.Gui v2's `Label` interprets `_` as a hotkey/mnemonic specifier, consuming it from the displayed text.

## What Changes

- Escape underscores in the preview label text so they display literally instead of being interpreted as mnemonic markers by Terminal.Gui

## Capabilities

### New Capabilities
<!-- None -->

### Modified Capabilities
<!-- None — this is a pure implementation bug fix, no spec-level behavior change -->

## Impact

- **TUI**: `CreateInvoiceView` — the preview label text needs underscore escaping (Terminal.Gui uses `__` to display a literal `_`)
