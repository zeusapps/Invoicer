## Why

Invoicer ships as a single self-contained `Invoicer.exe` that users copy somewhere and run. Once a new version is tagged and released on GitHub, there is no mechanism to tell the user about it — they must notice the release themselves, download the zip, and swap the executable by hand. In practice that means installed copies drift arbitrarily far behind, which matters for a tool whose output (KSeF XML in particular) tracks external legal schemas that change on a fixed timetable.

The release pipeline already publishes everything an in-app updater needs: a `v*.*.*` tag, a GitHub Release, and a predictably named `Invoicer-<tag>.zip` asset containing exactly one file.

## What Changes

- Add an **update check** that queries the GitHub Releases API for the latest stable release of `zeusapps/Invoicer` and compares its tag against the running executable's version.
- Add a **"Check for Updates"** item under the Help menu that runs the check on demand and reports the result either way (up to date / update available / check failed).
- Add an **automatic check on startup**, off the UI thread, that stays silent unless a newer version exists. Failures (offline, rate-limited, API change) never block or interrupt the user.
- When an update is available, show the new version and release notes and let the user choose to install or dismiss. **Installation is never silent** — the user always confirms.
- Add **self-replacement**: download the release zip, extract `Invoicer.exe`, rename the running executable aside, move the new one into place, and restart. The stale file is cleaned up on the next launch. `config.toml` and generated output are untouched.
- Add an `[update]` section to `config.toml` (`check_on_startup`, `repository`) so the automatic check can be disabled.
- **Stamp the assembly version from the git tag in CI.** Today `Invoicer.csproj` declares no `<Version>`, so every build reports `1.0.0` regardless of which tag produced it and no comparison is meaningful. The release workflow must pass `-p:Version=<tag without leading v>`.
- Replace the hardcoded `"Invoicer v1.0"` string in the About dialog with the real assembly version.

Not in scope: delta/patch updates, rollback to a previous version, update channels beyond stable, signature or checksum verification of the downloaded asset (transport integrity relies on TLS to `api.github.com` and `objects.githubusercontent.com`), and updating anything other than the executable itself.

## Capabilities

### New Capabilities
- `auto-update`: the application knowing its own version, discovering newer stable GitHub releases, presenting them to the user, and replacing its own executable on confirmation.

### Modified Capabilities
- `ci-build`: the release workflow gains a requirement to stamp the published executable's version from the git tag, so a built artifact can identify itself at runtime.

## Impact

**New code**
- `src/Invoicer/Update/` — release lookup against the GitHub API, version comparison, download and self-replace, startup cleanup of the superseded executable.
- `src/Invoicer/Models/UpdateConfig.cs` — new config section.

**Modified code**
- `src/Invoicer/Invoicer.csproj` — default `<Version>` for local builds.
- `.github/workflows/build-release.yml` — pass `-p:Version=` derived from `github.ref_name` to `dotnet publish`.
- `src/Invoicer/Program.cs` — clean up a leftover `.old` executable before the TUI starts.
- `src/Invoicer/Tui/InvoicerApp.cs` — Help menu item, startup check, update dialog, real version in About.
- `src/Invoicer/Config/ConfigManager.cs` — read and write `[update]`.

**Dependencies**
- No new NuGet packages. `HttpClient`, `System.IO.Compression`, and `System.Text.Json` are in the framework.
- Trimming constraint: `PublishTrimmed=true` makes reflection-based JSON deserialization unreliable, so the API response must be read with `JsonDocument` rather than `JsonSerializer.Deserialize<T>`.

**External**
- Outbound HTTPS to `api.github.com` and `objects.githubusercontent.com`. Unauthenticated requests are rate-limited to 60/hour per IP, which is ample for one check per launch, and the GitHub API rejects requests without a `User-Agent` header.
