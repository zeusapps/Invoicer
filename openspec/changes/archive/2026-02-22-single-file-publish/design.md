## Context

The current `dotnet publish` output is a self-contained win-x64 build with dozens of files: runtime DLLs, managed dependencies, satellite assemblies, and native libraries. The .csproj has no publish configuration. QuestPDF ships native DLLs (`qpdf.dll`, `QuestPdfSkia.dll`, `libgcc_s_seh-1.dll`, `libstdc++-6.dll`, `libwinpthread-1.dll`, `zlib1.dll`) under `runtimes/win-x64/native/`. Terminal.Gui includes satellite assemblies for fr-FR, ja-JP, pt-PT, zh-Hans.

## Goals / Non-Goals

**Goals:**
- `dotnet publish` produces a single executable + config.toml
- Native libraries and satellite assemblies are bundled inside the executable
- Output size is reasonable (trimming where safe)

**Non-Goals:**
- Cross-platform publish targets (win-x64 only for now)
- NativeAOT compilation (incompatible with QuestPDF's reflection usage)
- Automated CI/CD pipeline changes

## Decisions

**1. Self-contained single-file with `IncludeNativeLibrariesForSelfExtract`**

QuestPDF's native DLLs would normally remain as separate files next to the executable. Setting `IncludeNativeLibrariesForSelfExtract = true` bundles them inside the single file and extracts them to a temp directory at startup. This gives a true single-file distribution.

Alternative: leaving native libs external — simpler but defeats the purpose of single-file.

**2. Enable trimming with `SuppressTrimAnalysisWarnings = false`**

Trimming removes unused managed code, significantly reducing output size. Setting `SuppressTrimAnalysisWarnings = false` surfaces any issues during build. If QuestPDF or Terminal.Gui break under trimming, specific assemblies can be rooted via `TrimmerRootAssembly`.

Alternative: skip trimming — larger but safer. We'll try trimming first and fall back if needed.

**3. Set `SatelliteResourceLanguages` to `en` only**

The app is bilingual (English/Ukrainian) but uses its own translations, not .NET resource files. Terminal.Gui's fr-FR, ja-JP, pt-PT, zh-Hans satellite assemblies are unused. Setting `SatelliteResourceLanguages` to `en` excludes them.

**4. Configure via .csproj properties, not a publish profile**

Publish profiles add a separate file and indirection. Since there's only one target (win-x64 single-file), putting properties directly in the .csproj is simpler. A `dotnet publish -c Release` is all that's needed.

## Risks / Trade-offs

**[Risk] Trimming breaks QuestPDF or Terminal.Gui at runtime** → Start with trimming enabled. If runtime errors occur, add `TrimmerRootAssembly` entries for affected packages. Worst case: disable trimming (`PublishTrimmed = false`) and accept larger output.

**[Risk] Self-extracting native libs add startup latency on first run** → Native libs are extracted once to a cache directory. Subsequent launches reuse the cache. Acceptable trade-off for single-file distribution.

**[Trade-off] Larger single file vs many small files** → The single executable will be larger than any individual file, but the total size is comparable. Distribution simplicity outweighs size concerns.
