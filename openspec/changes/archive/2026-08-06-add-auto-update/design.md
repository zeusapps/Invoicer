## Context

Invoicer is distributed as one self-contained, trimmed, single-file `Invoicer.exe` for win-x64. It keeps `config.toml` beside itself in `AppContext.BaseDirectory` and writes generated invoices to a user-configured output directory. Releases are produced by [.github/workflows/build-release.yml](../../../.github/workflows/build-release.yml): pushing a `v*.*.*` tag runs `dotnet publish`, zips the exe as `Invoicer-<tag>.zip`, and attaches it to a GitHub Release on the public `zeusapps/Invoicer` repository.

Three properties of that setup shape this design:

1. **The published binary has no version.** `Invoicer.csproj` sets no `<Version>`, so every build reports assembly version `1.0.0`. Nothing else in this change works until CI stamps the tag into the binary.
2. **The app is trimmed.** `PublishTrimmed=true` means reflection-heavy APIs are unsafe unless explicitly rooted.
3. **The app is a single file.** There is exactly one thing to replace, and no side-by-side assemblies to keep in sync — which makes self-replacement far simpler than it would be for a multi-file layout.

The UI is Terminal.Gui v2, which is single-threaded: anything touching views from a background thread must be marshalled back through `Application.Invoke`.

## Goals / Non-Goals

**Goals:**
- The running executable can state its own version, and that version corresponds to the git tag it was built from.
- Detect newer stable releases without the user going to GitHub.
- Never interrupt, block, or crash the app because of a failed or slow update check.
- Replace the executable in place, on explicit user confirmation, without losing `config.toml`.
- Keep the update path testable: version comparison, release-payload parsing, and asset selection are pure functions with no network in the test path.

**Non-Goals:**
- Delta or patch updates. The zip is a few tens of MB; a full swap is simpler and adequate.
- Downgrade or rollback UI. The superseded executable is retained until the next launch, which covers a failed swap, not user regret.
- Pre-release, beta, or nightly channels. Stable releases only.
- Cryptographic verification of the download (code signing, published checksums). Deferred; see Risks.
- Updating `config.toml` schema as part of an update, or migrating user data.
- Silent background installation.

## Decisions

### Use the GitHub REST API `releases/latest` endpoint, unauthenticated

`GET https://api.github.com/repos/{owner}/{repo}/releases/latest` returns the most recent published non-prerelease, non-draft release. That is exactly the semantics wanted, so no client-side filtering of a release list is needed.

- Unauthenticated requests are limited to 60/hour per IP. One check per launch plus occasional manual checks is nowhere near that, and no token can be embedded in a public client anyway.
- **The API returns 403 for requests without a `User-Agent` header.** The client sets `User-Agent: Invoicer/<version>`.
- Send `Accept: application/vnd.github+json` and pin `X-GitHub-Api-Version: 2022-11-28` so a future default-version bump cannot change the payload shape underneath a shipped binary.
- The repository is read from `[update].repository` in config (default `zeusapps/Invoicer`) rather than hardcoded, so a fork can retarget it without a code change.

*Alternative considered:* scraping the releases Atom feed. It avoids the rate limit but gives no asset URLs, so a second request would be needed anyway.

### Parse the response with `JsonDocument`, not `JsonSerializer.Deserialize<T>`

Reflection-based deserialization is exactly what `PublishTrimmed=true` breaks, and the failure mode is a runtime exception in the shipped binary that never appears in a debug build. The response is read field by field with `JsonDocument` — `tag_name`, `name`, `body`, `html_url`, and the `assets` array — which is fully trim-safe and needs no source-generated context or `TrimmerRootAssembly` entry. Only five fields are read, so the ceremony of a source-gen context buys nothing.

### Version comparison on `System.Version`, tolerant of the `v` prefix

Tags are `vMAJOR.MINOR.PATCH`; assembly versions are `MAJOR.MINOR.PATCH.REVISION`. The comparison strips a leading `v`, parses with `Version.TryParse`, and compares the first three components only — otherwise the assembly's implicit `.0` revision makes equal versions compare unequal in confusing ways. A tag that does not parse is treated as "no update available" rather than as an error, so a stray non-semver release cannot nag every user.

The running version comes from the assembly's `AssemblyInformationalVersionAttribute` (falling back to `AssemblyName.Version`). Informational version is what `-p:Version=` sets, and it survives as a plain string.

### Stamp the version in CI from the tag

The release job derives the number by stripping the leading `v` from `github.ref_name` and passes `-p:Version=$version` to `dotnet publish`. The csproj keeps a `<Version>0.0.0</Version>` default so local and CI-branch builds are unambiguously "not a release" and never claim to be newer than a real release.

*Alternative considered:* a version file in the repo bumped by hand. Rejected — it can drift from the tag, and the tag is already the source of truth for the release name and the asset filename.

### Self-replace by renaming the running executable

Windows refuses to delete or overwrite the image of a running process, but it *does* allow renaming it within the same volume. That gives a swap that needs no helper process, no elevation, and no installer:

1. Download the asset to a temp directory and extract `Invoicer.exe` from it.
2. Sanity-check the extracted file (non-empty, has the `MZ` header) before touching anything.
3. `File.Move(current, current + ".old")`.
4. `File.Move(extracted, current)`.
5. If step 4 throws, move `.old` back and abort with the original binary intact.
6. Start the new executable and request the TUI stop.
7. On the next launch, `Program` deletes any `*.old` beside the executable; the delete now succeeds because that image is no longer running.

This is the same approach used by Chrome and VS Code updaters. It rules out the classic batch-file-that-loops-until-the-exe-unlocks pattern, which is fragile, flashes a console window, and is indistinguishable from malware to most AV heuristics.

*Alternative considered:* a separate `Updater.exe`. It would break the single-file-publish guarantee and doubles what CI must produce.

### The check runs on a background task; the UI is only touched via `Application.Invoke`

The startup check runs as a fire-and-forget `Task` started after `Application.Init` with a short timeout (10s). Every exception is swallowed — an update check is not worth an error dialog, let alone a crash. Only when a newer version is found does it marshal back to show a dialog. The manual Help → Check for Updates path uses the same code but reports all three outcomes, because a user who explicitly asked deserves an answer even when it is "you're up to date" or "couldn't reach GitHub".

### Config gate

A new `[update]` section with `check_on_startup` (default `true`) and `repository` (default `zeusapps/Invoicer`). `ConfigManager` hand-writes TOML, so this is a matched pair of read and write edits. Default-on is the right call for a tool that must track legal schema changes, and the manual menu item still works when it is off.

## Risks / Trade-offs

- **No integrity verification of the download** → Transport is HTTPS to GitHub-controlled hosts with certificate validation, and the asset URL comes from the API response rather than being constructed by hand. A compromised GitHub account would defeat the check regardless. Publishing a `.sha256` alongside the zip and verifying it is a cheap follow-up if this stops feeling acceptable; the workflow change is two lines.
- **Antivirus or SmartScreen blocks a self-modifying, unsigned executable** → Plausible, and unfixable without code signing. Mitigated by leaving the original binary in place on any failure and telling the user the release page URL so they can update manually. The dialog always offers "open the release page" as an escape hatch.
- **The swap succeeds but the new binary is broken** → The `.old` file survives until the next successful launch, so a user can rename it back. Not automated, and the user has to be told; the release notes are the only real defense.
- **Rate limiting on a shared IP** (office NAT, VPN) → 403/429 is treated as "check failed", never as "up to date", and the automatic path stays silent about it. One check per launch keeps normal usage far below the limit.
- **Version stamping is the single point of failure** → If CI stops passing `-p:Version=`, every release reports `0.0.0` and every installed copy sees an update-available loop that never resolves. This is worth a test that asserts the assembly version is non-zero in Release, plus a workflow step that fails if the tag does not parse.
- **`Application.Invoke` after the user has quit** → The startup check can complete after the TUI has stopped. The callback checks that the application is still running before showing anything.
- **Full-binary download on a metered connection** → Accepted. The user confirms before any download starts, and the dialog shows the asset size.

## Migration Plan

1. Land the version stamping first — csproj default plus the workflow flag. It is independently correct and harmless on its own.
2. Land the updater. The first release that contains it can only be *discovered* by the release after it; the version shipped before this change will never self-update, so users on `v1.1.1` and earlier still have to do one manual update. Worth calling out in the release notes.
3. Rollback is deleting the Help menu item and setting `check_on_startup = false` by default; nothing about the update path is load-bearing for invoice generation.

## Open Questions

- Should a dismissed update stay dismissed for that version, or re-prompt on every launch? Re-prompting every launch is the simpler implementation and the more annoying experience. Proposed: remember the dismissed version in config and stay quiet until a newer one appears.
- Should the update dialog render the release body verbatim? GitHub release notes are Markdown, and Terminal.Gui will show the raw text. Proposed: show it as-is in a scrollable read-only view and accept the stray `##`.
