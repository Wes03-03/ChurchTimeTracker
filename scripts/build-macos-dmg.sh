#!/usr/bin/env bash
set -euo pipefail

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This DMG must be built on macOS with Xcode." >&2
  exit 1
fi

: "${VERSION:?Set VERSION to a semantic version such as 0.4.0.}"
: "${BUILD_NUMBER:?Set BUILD_NUMBER to an increasing integer.}"
: "${SPARKLE_PUBLIC_ED_KEY:?Set SPARKLE_PUBLIC_ED_KEY to the public key printed by Sparkle generate_keys.}"

if [[ -z "${SPARKLE_FEED_URL:-}" ]]; then
  : "${GITHUB_REPOSITORY:?Set GITHUB_REPOSITORY to owner/repository or set SPARKLE_FEED_URL explicitly.}"
  SPARKLE_FEED_URL="https://github.com/${GITHUB_REPOSITORY}/releases/latest/download/appcast.xml"
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
project_root="$(cd "$script_dir/.." && pwd)"
project_path="$project_root/ChurchTimeTracker.csproj"
build_root="$project_root/artifacts/macos/$VERSION"
staging_dir="$build_root/dmg-root"
dmg_path="$build_root/ChurchTimeTracker-$VERSION-mac.dmg"

mkdir -p "$build_root"

dotnet publish "$project_path" \
  -f net9.0-maccatalyst \
  -c Release \
  -p:MtouchLink=SdkOnly \
  -p:CreatePackage=false \
  -p:EnableCodeSigning=false \
  -p:ApplicationDisplayVersion="$VERSION" \
  -p:ApplicationVersion="$BUILD_NUMBER" \
  -p:Version="$VERSION" \
  -p:SparkleFeedUrl="$SPARKLE_FEED_URL" \
  -p:SparklePublicKey="$SPARKLE_PUBLIC_ED_KEY"

app_path="$(find "$project_root/bin/Release/net9.0-maccatalyst" -maxdepth 4 -type d \
  \( -name 'ChurchTimeTracker.app' -o -name 'Church Time Tracker.app' \) -print -quit)"
if [[ -z "$app_path" ]]; then
  echo "Publish completed, but Church Time Tracker.app was not found." >&2
  find "$project_root/bin/Release/net9.0-maccatalyst" -maxdepth 5 -type d -name '*.app' -print >&2
  exit 1
fi

rm -rf "$staging_dir"
mkdir -p "$staging_dir"
ditto "$app_path" "$staging_dir/Church Time Tracker.app"
ln -s /Applications "$staging_dir/Applications"

app_plist="$staging_dir/Church Time Tracker.app/Contents/Info.plist"
/usr/libexec/PlistBuddy -c 'Delete :SUFeedURL' "$app_plist" >/dev/null 2>&1 || true
/usr/libexec/PlistBuddy -c 'Delete :SUPublicEDKey' "$app_plist" >/dev/null 2>&1 || true
/usr/libexec/PlistBuddy -c "Add :SUFeedURL string $SPARKLE_FEED_URL" "$app_plist"
/usr/libexec/PlistBuddy -c "Add :SUPublicEDKey string $SPARKLE_PUBLIC_ED_KEY" "$app_plist"

# Updating Info.plist changes the app bundle seal. Reapply a free ad-hoc signature
# to the outer bundle without recursively replacing Sparkle's helper signatures.
codesign \
  --force \
  --sign - \
  --entitlements "$project_root/Platforms/MacCatalyst/Entitlements.plist" \
  "$staging_dir/Church Time Tracker.app"
codesign --verify --deep --strict --verbose=2 "$staging_dir/Church Time Tracker.app"

embedded_entitlements="$build_root/embedded-entitlements.plist"
codesign --display --entitlements :- "$staging_dir/Church Time Tracker.app" \
  > "$embedded_entitlements" 2>/dev/null
if [[ "$(/usr/libexec/PlistBuddy -c 'Print :com.apple.security.files.user-selected.read-write' "$embedded_entitlements")" != "true" ]]; then
  echo "The packaged app is missing user-selected file read/write access." >&2
  exit 1
fi

app_executable="$staging_dir/Church Time Tracker.app/Contents/MacOS/ChurchTimeTracker"
if [[ ! -f "$app_executable" ]]; then
  app_executable="$(find "$staging_dir/Church Time Tracker.app/Contents/MacOS" -maxdepth 1 -type f -print -quit)"
fi
app_architectures="$(lipo -archs "$app_executable")"
if [[ "$app_architectures" != *"arm64"* || "$app_architectures" != *"x86_64"* ]]; then
  echo "Expected a universal Mac app, but found: $app_architectures" >&2
  exit 1
fi

hdiutil create \
  -volname 'Church Time Tracker' \
  -srcfolder "$staging_dir" \
  -ov \
  -format UDZO \
  "$dmg_path"

echo "Unsigned DMG created: $dmg_path"
echo "Sparkle feed: $SPARKLE_FEED_URL"
