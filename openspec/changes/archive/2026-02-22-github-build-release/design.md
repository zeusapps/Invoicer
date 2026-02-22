## Context

The project has no CI/CD. The .csproj is already configured for single-file self-contained win-x64 publishing with trimming, embedded fonts, and native library bundling. Publishing is done manually via `dotnet publish`.

## Goals / Non-Goals

**Goals:**
- Automated build validation on every push to main
- Automated GitHub Release with zipped executable on version tag push

**Non-Goals:**
- Multi-platform builds (only win-x64)
- NuGet package publishing
- Automated version bumping or changelog generation

## Decisions

**Single workflow file with two jobs** (build + release) rather than separate workflow files.
- Rationale: Both jobs share the same trigger context (push to main). The release job adds a condition on tag pattern. One file is simpler to maintain.

**Trigger on push to main and version tags** (`v*.*.*`).
- Build job runs on all pushes to main.
- Release job runs only when a version tag matches `v*.*.*`.
- Alternative considered: separate `workflow_dispatch` — rejected because tag-based is the standard convention and the user's preference.

**Use `dotnet publish` with Release configuration** and zip the output.
- Rationale: The .csproj already has all publish settings (PublishSingleFile, SelfContained, RuntimeIdentifier). Just need `-c Release` and an output path.

**Use `actions/upload-artifact` for build and `softprops/action-gh-release` for releases.**
- Rationale: These are the most widely used, well-maintained GitHub Actions for these tasks.

## Risks / Trade-offs

- [Windows-only build] → Acceptable since the app targets win-x64 only. If cross-platform is needed later, add matrix builds.
- [Large artifact size ~40MB] → Expected for self-contained .NET apps. Zip compression will help.
