## Why

There is no CI/CD pipeline. Building and releasing is manual. A GitHub Actions workflow will automate building on every push to main and creating releases when version tags are pushed.

## What Changes

- Add a GitHub Actions workflow that builds the project on push to main
- On version tag push (e.g., `v1.0.0`), publish a self-contained win-x64 single-file executable, zip it, and attach it to a GitHub Release
- Uses the existing .csproj publish configuration (PublishSingleFile, SelfContained, win-x64)

## Capabilities

### New Capabilities
- `ci-build`: GitHub Actions workflow that builds on push to main and creates releases on version tags

### Modified Capabilities
<!-- None -->

## Impact

- New file: `.github/workflows/build-release.yml`
- No code changes — leverages existing .csproj publish settings
- Requires GitHub Actions to be enabled on the repository
