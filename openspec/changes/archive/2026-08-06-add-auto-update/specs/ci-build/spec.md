## MODIFIED Requirements

### Requirement: Publish and release on version tag
The workflow SHALL publish a self-contained win-x64 single-file executable when a version tag matching `v*.*.*` is pushed. The publish step MUST stamp the executable's version from the tag by passing `-p:Version=<tag without the leading v>` to `dotnet publish`, so the built binary can identify its own version at runtime. The executable MUST be zipped and attached to a GitHub Release named after the tag.

#### Scenario: Tag triggers release
- **WHEN** a tag matching `v*.*.*` (e.g., `v1.0.0`) is pushed
- **THEN** the workflow publishes with `dotnet publish -c Release`, zips the output, and creates a GitHub Release with the zip attached

#### Scenario: Published executable reports the tag version
- **WHEN** the release job publishes from tag `v1.2.0`
- **THEN** the resulting `Invoicer.exe` reports version `1.2.0` at runtime

#### Scenario: Non-version tag does not trigger release
- **WHEN** a tag not matching `v*.*.*` is pushed
- **THEN** no release is created

## ADDED Requirements

### Requirement: Non-release builds carry a sentinel version
Builds not produced from a version tag SHALL report version `0.0.0`, so that a development or branch build is never mistaken for a release and never compares as newer than a published one.

#### Scenario: Local build
- **WHEN** the project is built locally with `dotnet build`
- **THEN** the resulting binary reports version `0.0.0`

#### Scenario: Push-to-main build
- **WHEN** the build job runs for a push to `main` without a version tag
- **THEN** no version is stamped and the binary reports `0.0.0`
