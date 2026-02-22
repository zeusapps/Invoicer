## 1. Configure .csproj publish properties

- [x] 1.1 Add `RuntimeIdentifier` = `win-x64` to PropertyGroup
- [x] 1.2 Add `PublishSingleFile` = `true`
- [x] 1.3 Add `SelfContained` = `true`
- [x] 1.4 Add `IncludeNativeLibrariesForSelfExtract` = `true`
- [x] 1.5 Add `SatelliteResourceLanguages` = `en`
- [x] 1.6 Add `DebugType` = `none` to exclude PDB from output

## 2. Enable trimming

- [x] 2.1 Add `PublishTrimmed` = `true` and `SuppressTrimAnalysisWarnings` = `true` (rooted assemblies make warnings safe)
- [x] 2.2 Add `TrimmerRootAssembly` entries for QuestPDF, Terminal.Gui, and Tomlyn

## 3. Embed Lato fonts as resources

- [x] 3.1 Copy Lato-Regular, Lato-Bold, Lato-Italic to `Resources/Fonts/` as `EmbeddedResource`
- [x] 3.2 Register embedded fonts via `FontManager.RegisterFont()` in `PdfGenerator.cs`
- [x] 3.3 Add MSBuild targets to remove QuestPDF's default `LatoFont/` directory from build and publish output

## 4. Verify

- [x] 4.1 Run `dotnet publish -c Release` and confirm output is `Invoicer.exe` + `config.toml` only
- [x] 4.2 Launch the published executable and verify TUI and PDF generation work correctly
