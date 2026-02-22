### Requirement: Build on push to main
The workflow SHALL build the project using `dotnet build` on every push to the main branch. The build MUST fail the workflow if compilation errors occur.

#### Scenario: Successful build on push
- **WHEN** a commit is pushed to the main branch
- **THEN** GitHub Actions runs `dotnet build` and the workflow succeeds

#### Scenario: Build failure on push
- **WHEN** a commit with compilation errors is pushed to main
- **THEN** the workflow fails and reports the build error

### Requirement: Publish and release on version tag
The workflow SHALL publish a self-contained win-x64 single-file executable when a version tag matching `v*.*.*` is pushed. The executable MUST be zipped and attached to a GitHub Release named after the tag.

#### Scenario: Tag triggers release
- **WHEN** a tag matching `v*.*.*` (e.g., `v1.0.0`) is pushed
- **THEN** the workflow publishes with `dotnet publish -c Release`, zips the output, and creates a GitHub Release with the zip attached

#### Scenario: Non-version tag does not trigger release
- **WHEN** a tag not matching `v*.*.*` is pushed
- **THEN** no release is created

### Requirement: Release artifact is a zip archive
The release artifact SHALL be a zip file containing the published single-file executable. The zip MUST be named `Invoicer-<tag>.zip` (e.g., `Invoicer-v1.0.0.zip`).

#### Scenario: Zip contents
- **WHEN** a release is created
- **THEN** the attached zip contains the `Invoicer.exe` single-file executable
