## 1. Setup

- [x] 1.1 Create `.github/workflows/` directory structure

## 2. Workflow Implementation

- [x] 2.1 Create `build-release.yml` with build job: trigger on push to main, run `dotnet build` on `windows-latest`
- [x] 2.2 Add release job: trigger on `v*.*.*` tags, run `dotnet publish -c Release`, zip output as `Invoicer-<tag>.zip`, create GitHub Release with `softprops/action-gh-release`

## 3. Verification

- [x] 3.1 Validate workflow YAML syntax
