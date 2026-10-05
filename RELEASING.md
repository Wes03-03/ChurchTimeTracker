# Releasing Church Time Tracker

The release workflow creates these files in one public GitHub Release:

- A Velopack `Setup.exe`, portable package, update feed, full package, and delta package for Windows.
- An unsigned universal DMG and stable `ChurchTimeTracker-mac.dmg` alias for macOS.

The repository that hosts Releases must be public. The installed apps intentionally do not contain a GitHub access token, so they cannot read update assets from a private repository.

## One-time GitHub setup

The public repository is `https://github.com/Wes03-03/ChurchTimeTracker`. Initial setup uses:

```powershell
git add .
git commit -m "Initial Church Time Tracker release setup"
git remote add origin https://github.com/Wes03-03/ChurchTimeTracker.git
git push -u origin main
```

If you want the source to remain private, create a separate public release repository and adjust the workflow and release URLs before publishing. Do not place a personal GitHub token inside the app.

## Publish an update

Choose a new semantic version, such as `0.4.0`. Commit and push the finished code, then create and push a version tag:

```powershell
git add .
git commit -m "Release 0.4.0"
git push
git tag v0.4.0
git push origin v0.4.0
```

Pushing the tag starts `.github/workflows/release.yml`. GitHub builds both operating systems and creates the Release automatically. Watch it under the repository's **Actions** tab.

Users install once from the Release page:

- Windows: download the Velopack `Setup.exe` asset.
- macOS: download `ChurchTimeTracker-mac.dmg`, open it, drag **Church Time Tracker** to Applications, eject the DMG, then use Control-click → Open from Applications for the first launch if Gatekeeper blocks it. Do not run the app from inside the mounted DMG.

Afterward, the app checks the same GitHub Releases feed automatically. Users can also open **Categories → Check for updates**. Windows downloads and restarts through Velopack. macOS opens the newest GitHub Release; the user downloads the new DMG and drags the app to Applications, choosing **Replace**. The app's database stays in the user's Library and is not removed when the application bundle is replaced.

Never reuse a version tag. Each update must have a greater version than the previous release: `0.4.0`, then `0.4.1`, then `0.5.0`, and so on.

## Local packaging

Build a Windows Velopack release without publishing it:

```powershell
.\scripts\build-windows-velopack.ps1 `
  -Version 0.4.0 `
  -RepositoryUrl https://github.com/Wes03-03/ChurchTimeTracker
```

On macOS, build an unsigned DMG without publishing it:

```bash
VERSION=0.4.0 \
BUILD_NUMBER=4 \
GITHUB_REPOSITORY=Wes03-03/ChurchTimeTracker \
./scripts/build-macos-dmg.sh
```

Local Windows output is written below `artifacts/windows/<version>/releases`. Local Mac output is written below `artifacts/macos/<version>`.
