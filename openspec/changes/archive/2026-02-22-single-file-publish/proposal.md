## Why

Publishing the app produces dozens of files (runtime DLLs, dependency assemblies, satellite resources), making distribution cumbersome. A single-file publish reduces the release to one executable (plus config.toml), which is simpler to distribute and deploy.

## What Changes

- Add single-file publish properties to `Invoicer.csproj`: `PublishSingleFile`, `SelfContained`, `RuntimeIdentifier`, `IncludeNativeLibrariesForSelfExtract`
- Enable trimming to reduce output size
- Ensure QuestPDF native dependencies are bundled (self-extracting)
- Trim unused Terminal.Gui satellite assemblies (localization resources)

## Capabilities

### New Capabilities

- `single-file-publish`: .csproj configuration for producing a single-file self-contained executable

### Modified Capabilities

_(none — no existing spec behavior changes)_

## Impact

- **Code**: `src/Invoicer/Invoicer.csproj` — publish properties only
- **Build**: `dotnet publish` output changes from many files to one executable + config.toml
- **Distribution**: Users receive a single .exe instead of a directory of files
- **Risk**: Trimming may remove types used via reflection (QuestPDF, Terminal.Gui) — may need trim annotations or rooting
