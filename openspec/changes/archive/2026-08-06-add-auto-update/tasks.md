## 1. Version identity

- [x] 1.1 Add `<Version>0.0.0</Version>` to `src/Invoicer/Invoicer.csproj` as the non-release default
- [x] 1.2 Add `.github/workflows/build-release.yml` step that derives the version from `github.ref_name` (strip leading `v`) and fails the job if it does not parse as `MAJOR.MINOR.PATCH`
- [x] 1.3 Pass `-p:Version=<derived>` to `dotnet publish` in the release job
- [x] 1.4 Add `AppVersion` helper that reads `AssemblyInformationalVersionAttribute` with a fallback to `AssemblyName.Version`, trimming any `+<commit>` build-metadata suffix
- [x] 1.5 Replace the hardcoded `"Invoicer v1.0"` string in `InvoicerApp.ShowAbout` with the real version

## 2. Config

- [x] 2.1 Add `src/Invoicer/Models/UpdateConfig.cs` with `CheckOnStartup` (default `true`), `Repository` (default `zeusapps/Invoicer`), and `DismissedVersion`
- [x] 2.2 Add `Update` property to `AppConfig`
- [x] 2.3 Read the `[update]` table in `ConfigManager.FromTomlTable`, defaulting every field when the section or a key is absent
- [x] 2.4 Write the `[update]` section in `ConfigManager.ToTomlString`
- [x] 2.5 Test: config written by an earlier version (no `[update]` section) loads with defaults and round-trips through save/load

## 3. Release lookup

- [x] 3.1 Add `src/Invoicer/Update/ReleaseInfo.cs` — tag, version, name, notes, release page URL, asset download URL, asset size
- [x] 3.2 Add `VersionComparer` with `TryParseTag` (strip leading `v`) and `IsNewer` comparing major/minor/patch only
- [x] 3.3 Test `VersionComparer`: newer, equal (including the assembly's four-component form vs a three-component tag), older, unparseable tag, empty tag
- [x] 3.4 Add `ReleaseParser` that reads a GitHub `releases/latest` JSON payload with `JsonDocument` and selects the asset whose name matches `Invoicer-<tag>.zip`
- [x] 3.5 Test `ReleaseParser` against a captured real payload fixture, plus payloads with no assets, an unexpected asset name, and missing optional fields
- [x] 3.6 Add `UpdateChecker` issuing the HTTP request with `User-Agent`, `Accept: application/vnd.github+json`, `X-GitHub-Api-Version: 2022-11-28`, and a 10s timeout, returning a result of up-to-date / update-available / failed
- [x] 3.7 Ensure non-success status codes (403 rate limit, 404 no releases) map to "failed", never to "up to date"

## 4. Download and self-replace

- [x] 4.1 Add `UpdateInstaller.DownloadAsync` writing the asset to a temp directory
- [x] 4.2 Extract `Invoicer.exe` from the zip with `System.IO.Compression`
- [x] 4.3 Validate the extracted file: non-empty and starts with the `MZ` header
- [x] 4.4 Implement the swap — rename running exe to `.old`, move the new one into place, restore `.old` on failure
- [x] 4.5 Test the swap against temporary files: success path, missing replacement, and the restore-on-failure path
- [x] 4.6 Launch the replaced executable and request the TUI stop
- [x] 4.7 Clean up the temp download directory on both success and failure

## 5. Startup cleanup

- [x] 5.1 Delete any `<exe>.old` beside the executable at the start of `Program.cs`, before `ConfigManager.Load`
- [x] 5.2 Swallow failures so a locked `.old` file cannot prevent startup

## 6. TUI integration

- [x] 6.1 Add "Check for _Updates" to the Help menu
- [x] 6.2 Add the update dialog — current version, new version, download size, scrollable release notes, and Install / Later / Open Release Page buttons
- [x] 6.3 Wire the manual check: report update-available, up-to-date, and failed distinctly
- [x] 6.4 Wire the startup check as a fire-and-forget task gated on `check_on_startup`, silent on failure and on up-to-date
- [x] 6.5 Marshal results with `Application.Invoke` and skip the dialog if the application is no longer running
- [x] 6.6 Skip the startup prompt when the found version equals `DismissedVersion`; record the version on "Later" and save config
- [x] 6.7 Open the release page via `Process.Start` with `UseShellExecute = true`

## 7. Verification

- [x] 7.1 `dotnet test` passes
- [x] 7.2 `dotnet publish -c Release -p:Version=1.2.0` produces a binary that reports `1.2.0`, and the publish output is still a single executable
- [x] 7.3 Manually verify the swap end to end against a real release asset: check, confirm, restart on the new version, `.old` removed on the following launch, `config.toml` intact
- [x] 7.4 Verify the app starts normally with no network access and with `check_on_startup = true`
- [x] 7.5 Update `README.md` with the update behaviour and the `[update]` config keys
