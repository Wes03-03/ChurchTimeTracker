#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This installer must be built on macOS with Xcode." >&2
  exit 1
fi

: "${MAC_APP_SIGNING_KEY:?Set MAC_APP_SIGNING_KEY to your Developer ID Application identity.}"
: "${MAC_INSTALLER_SIGNING_KEY:?Set MAC_INSTALLER_SIGNING_KEY to your Developer ID Installer identity.}"
: "${MAC_PROVISIONING_PROFILE:?Set MAC_PROVISIONING_PROFILE to the distribution profile name.}"

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_path="$(cd "$script_dir/.." && pwd)/ChurchTimeTracker.csproj"

dotnet publish "$project_path" \
  -f net9.0-maccatalyst \
  -c Release \
  -p:MtouchLink=SdkOnly \
  -p:RuntimeIdentifiers='maccatalyst-x64;maccatalyst-arm64' \
  -p:CreatePackage=true \
  -p:EnableCodeSigning=true \
  -p:EnablePackageSigning=true \
  -p:CodesignKey="$MAC_APP_SIGNING_KEY" \
  -p:CodesignProvision="$MAC_PROVISIONING_PROFILE" \
  -p:CodesignEntitlements=Platforms/MacCatalyst/Entitlements.plist \
  -p:PackageSigningKey="$MAC_INSTALLER_SIGNING_KEY" \
  -p:UseHardenedRuntime=true

echo "PKG created under bin/Release/net9.0-maccatalyst. Notarize and staple it before distribution."
