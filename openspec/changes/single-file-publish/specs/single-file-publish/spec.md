## ADDED Requirements

### Requirement: Publish produces a single executable
Running `dotnet publish -c Release` SHALL produce a single self-contained executable for win-x64 that bundles all managed assemblies, runtime components, and native libraries.

#### Scenario: Single-file output
- **WHEN** `dotnet publish -c Release` is run
- **THEN** the publish output directory contains exactly one executable file (plus any user-managed files like config.toml)

#### Scenario: Native libraries are bundled
- **WHEN** the single-file executable is published
- **THEN** QuestPDF native dependencies (qpdf.dll, QuestPdfSkia.dll, etc.) are embedded inside the executable

### Requirement: Application runs correctly from single file
The single-file executable SHALL start and function identically to the multi-file build, including PDF generation (QuestPDF), DOCX generation (OpenXml), TUI rendering (Terminal.Gui), and config loading (Tomlyn).

#### Scenario: PDF generation works from single-file build
- **WHEN** the user generates an invoice as PDF from the single-file executable
- **THEN** the PDF is created successfully with correct formatting

#### Scenario: TUI renders correctly from single-file build
- **WHEN** the user launches the single-file executable
- **THEN** the Terminal.Gui interface renders and functions normally

### Requirement: Unused satellite assemblies are excluded
The publish output SHALL NOT include satellite assemblies for unused locales (fr-FR, ja-JP, pt-PT, zh-Hans from Terminal.Gui).

#### Scenario: No satellite assembly directories in output
- **WHEN** `dotnet publish -c Release` is run
- **THEN** no locale-specific subdirectories (e.g., fr-FR/, ja-JP/) exist in the publish output
