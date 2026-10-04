# Distribution

The project is configured for desktop-only Windows and macOS releases. A smooth one-click install requires trusted code signing on both platforms; unsigned packages are suitable only for local testing.

## Windows MSIX

Run on Windows from the repository root:

```powershell
.\scripts\Build-WindowsInstaller.ps1 -CertificateThumbprint "YOUR_CERTIFICATE_THUMBPRINT"
```

The certificate must be available in the current user's certificate store and its subject must match the publisher identity used for the package. For public distribution, use a trusted code-signing certificate or publish the MSIX through the Microsoft Store. The script also accepts `-Architecture arm64` for Windows on Arm.

Without `-CertificateThumbprint`, the script emits an unsigned development MSIX and prints a warning. Windows will not provide a one-click experience for that artifact until it is signed by a certificate trusted on the destination device.

## macOS PKG

Apple requires the release to be built on macOS with Xcode, an Apple Developer Program membership, Developer ID Application and Installer certificates, and a matching provisioning profile.

Set these environment variables on the Mac:

```bash
export MAC_APP_SIGNING_KEY='Developer ID Application: Organization (TEAMID)'
export MAC_INSTALLER_SIGNING_KEY='Developer ID Installer: Organization (TEAMID)'
export MAC_PROVISIONING_PROFILE='Church Time Tracker Distribution'
./scripts/build-macos-installer.sh
```

After the `.pkg` is created, submit it to Apple's notary service and staple the accepted ticket before distribution:

```bash
xcrun notarytool submit path/to/ChurchTimeTracker.pkg --keychain-profile YOUR_NOTARY_PROFILE --wait
xcrun stapler staple path/to/ChurchTimeTracker.pkg
```

Ship only the signed and notarized PKG. The release target is universal (`x64` and `arm64`) so the same installer can be used on Intel and Apple Silicon Macs.
