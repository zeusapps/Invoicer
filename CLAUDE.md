# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Invoicer — a .NET 9 TUI application for generating bilingual (English/Ukrainian) invoices as DOCX and PDF files.

## Tech Stack

- **Runtime**: .NET 9, C#
- **TUI**: Terminal.Gui v2
- **PDF**: QuestPDF (Community license)
- **DOCX**: DocumentFormat.OpenXml
- **Config**: Tomlyn (TOML)

## Build & Run

```bash
dotnet build
dotnet run --project src/Invoicer
```

## Project Structure

- `src/Invoicer/Program.cs` — Entry point, loads config, launches TUI
- `src/Invoicer/Models/` — Data models (AppConfig, ClientConfig, SupplierConfig, Invoice, OutputConfig)
- `src/Invoicer/Config/ConfigManager.cs` — TOML config load/save
- `src/Invoicer/Generation/` — DocxGenerator and PdfGenerator
- `src/Invoicer/Tui/` — Terminal.Gui views (InvoicerApp, CreateInvoiceView, ClientListView, SettingsView)

## Config

Config is stored in `config.toml` next to the binary. Created with defaults on first run.
