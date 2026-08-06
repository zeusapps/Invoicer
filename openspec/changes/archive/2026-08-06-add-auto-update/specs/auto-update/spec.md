## ADDED Requirements

### Requirement: Application reports its own version
The application SHALL expose the version of the running executable, read from the assembly's informational version with a fallback to the assembly version. The About dialog SHALL display that version rather than a hardcoded string.

#### Scenario: About dialog shows the built version
- **WHEN** the user opens Help → About on a build published from tag `v1.2.0`
- **THEN** the dialog displays version `1.2.0`

#### Scenario: Local development build
- **WHEN** the application is built locally without a version override
- **THEN** the reported version is `0.0.0`, which never compares as newer than a published release

### Requirement: Check GitHub for the latest stable release
The application SHALL query the GitHub REST API endpoint `releases/latest` for the configured repository to discover the newest published release. The request MUST include a `User-Agent` header, an `Accept: application/vnd.github+json` header, and a pinned `X-GitHub-Api-Version` header. Draft and pre-release versions MUST NOT be offered to the user.

#### Scenario: Latest release retrieved
- **WHEN** an update check runs and GitHub returns a release with tag `v1.3.0`
- **THEN** the application parses the tag, release name, notes, release page URL, and the asset matching `Invoicer-<tag>.zip` with its download URL and size

#### Scenario: Pre-release is not offered
- **WHEN** the newest release on the repository is marked as a pre-release
- **THEN** the check reports the newest stable release instead, and never the pre-release

#### Scenario: Request omitting User-Agent is not made
- **WHEN** the application issues an update-check request
- **THEN** the request carries a `User-Agent` header identifying Invoicer and its version

### Requirement: Compare versions to decide whether an update exists
The application SHALL compare the release tag against the running version by stripping a leading `v` and comparing major, minor, and patch components only. An update SHALL be reported only when the release version is strictly greater. A tag that cannot be parsed SHALL be treated as "no update available" and MUST NOT surface an error.

#### Scenario: Newer release available
- **WHEN** the running version is `1.1.1` and the latest tag is `v1.2.0`
- **THEN** an update is reported as available

#### Scenario: Same version
- **WHEN** the running version is `1.2.0` and the latest tag is `v1.2.0`
- **THEN** no update is reported, despite the assembly version carrying a fourth revision component

#### Scenario: Running a newer build than the latest release
- **WHEN** the running version is `1.3.0` and the latest tag is `v1.2.0`
- **THEN** no update is reported

#### Scenario: Unparseable tag
- **WHEN** the latest release is tagged `nightly`
- **THEN** no update is reported and no error is shown

### Requirement: Automatic check on startup is silent unless an update exists
When `check_on_startup` is enabled, the application SHALL run the update check on a background task after the TUI initialises, with a request timeout. The check MUST NOT block startup, and MUST NOT display anything when it finds no update or when it fails for any reason.

#### Scenario: Update found on startup
- **WHEN** the startup check finds a newer release
- **THEN** the update dialog is shown on the UI thread once the interface is ready

#### Scenario: Offline at startup
- **WHEN** the startup check cannot reach GitHub
- **THEN** the application starts normally and displays no error, warning, or dialog

#### Scenario: Startup check disabled
- **WHEN** `check_on_startup` is `false` in config
- **THEN** no network request is made at startup

#### Scenario: Check completes after the user quits
- **WHEN** the startup check finds an update after the user has already exited the application
- **THEN** no dialog is shown and no exception is raised

### Requirement: Manual check reports every outcome
The Help menu SHALL contain a "Check for Updates" item. A manually triggered check SHALL report its result to the user in all cases: an update is available, the application is up to date, or the check failed.

#### Scenario: Manual check finds an update
- **WHEN** the user selects Help → Check for Updates and a newer release exists
- **THEN** the update dialog is shown

#### Scenario: Manual check finds nothing
- **WHEN** the user selects Help → Check for Updates and the running version is current
- **THEN** a message states that the application is up to date

#### Scenario: Manual check fails
- **WHEN** the user selects Help → Check for Updates while offline or rate-limited
- **THEN** a message states that the check could not be completed, and the failure is not reported as "up to date"

### Requirement: User confirms before any update is downloaded or installed
The application SHALL NOT download or install an update without explicit user confirmation. The update dialog SHALL show the new version, the current version, the download size, and the release notes, and SHALL offer to install, to dismiss, or to open the release page in a browser.

#### Scenario: User declines
- **WHEN** the update dialog is shown and the user dismisses it
- **THEN** nothing is downloaded and the application continues normally

#### Scenario: User opens the release page
- **WHEN** the user chooses to open the release page
- **THEN** the GitHub release URL opens in the default browser and no download starts

#### Scenario: Dismissed version is not re-offered
- **WHEN** the user dismisses version `1.2.0` and later restarts the application while `1.2.0` is still the latest
- **THEN** the startup check does not prompt again, until a version newer than `1.2.0` is released

### Requirement: Install replaces the running executable in place
On confirmation, the application SHALL download the release asset, extract `Invoicer.exe`, verify the extracted file is a non-empty Windows executable, rename the running executable by appending `.old`, move the new executable into its place, launch it, and exit. `config.toml` and any generated output MUST NOT be modified.

#### Scenario: Successful update
- **WHEN** the user confirms an update and the download and extraction succeed
- **THEN** the new executable replaces the old one, the application restarts on the new version, and `config.toml` is unchanged

#### Scenario: Download fails
- **WHEN** the download is interrupted or the asset cannot be retrieved
- **THEN** the running executable is untouched, the user is told the update failed, and the application continues running

#### Scenario: Extracted file is not a valid executable
- **WHEN** the extracted file is empty or lacks an `MZ` header
- **THEN** the swap is not attempted and the update is reported as failed

#### Scenario: Swap fails partway
- **WHEN** the running executable has been renamed to `.old` but moving the new executable into place fails
- **THEN** the `.old` file is restored to its original name and the update is reported as failed

### Requirement: Superseded executable is cleaned up on next launch
On startup, before the TUI initialises, the application SHALL delete any `.old` executable left beside itself by a previous update. A failure to delete MUST NOT prevent the application from starting.

#### Scenario: Cleanup after an update
- **WHEN** the application starts and `Invoicer.exe.old` exists in its directory
- **THEN** the file is deleted and the application starts normally

#### Scenario: Cleanup blocked
- **WHEN** the `.old` file cannot be deleted because it is locked
- **THEN** the application starts normally without an error

### Requirement: Update behaviour is configurable
`config.toml` SHALL contain an `[update]` section with `check_on_startup` (boolean, default `true`) and `repository` (string, default `zeusapps/Invoicer`). Missing values SHALL fall back to the defaults, and the section MUST be written back on save.

#### Scenario: Defaults on a fresh config
- **WHEN** a new `config.toml` is created
- **THEN** it contains `[update]` with `check_on_startup = true` and `repository = "zeusapps/Invoicer"`

#### Scenario: Existing config without the section
- **WHEN** a config file written by an earlier version is loaded
- **THEN** the update settings take their default values and are persisted on the next save
