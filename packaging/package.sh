#!/usr/bin/env bash
# Packs one published RID into a release archive.
#
#   packaging/package.sh <rid> <publish-dir> <output-dir> <version>
#
# win-x64   -> YKanBan-<version>-win-x64.zip     (folder YKanBan-<version>-win-x64/)
# linux-x64 -> YKanBan-<version>-linux-x64.tar.gz (folder YKanBan-<version>-linux-x64/)
# osx-*     -> YKanBan-<version>-<rid>.tar.gz     (YKanBan.app bundle, ad-hoc signed when possible)
#
# tar.gz keeps the executable bit, which a plain zip artifact upload would lose.
set -euo pipefail

if [ "$#" -ne 4 ]; then
  echo "usage: $0 <rid> <publish-dir> <output-dir> <version>" >&2
  exit 2
fi

rid="$1"
publish_dir="$(cd "$2" && pwd)"
mkdir -p "$3"
output_dir="$(cd "$3" && pwd)"
version="$4"
script_dir="$(cd "$(dirname "$0")" && pwd)"
name="YKanBan-${version}-${rid}"
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT

case "$rid" in
  win-*)
    cp -R "$publish_dir" "$staging/$name"
    rm -f "$output_dir/$name.zip"
    # 7-Zip ships on the Windows runners and writes '/' entry separators;
    # Windows PowerShell 5.1's Compress-Archive writes '\', which breaks
    # extraction outside Windows.
    if ! command -v 7z > /dev/null 2>&1; then
      echo "7z not found" >&2
      exit 1
    fi
    (cd "$staging" && 7z a -tzip -bso0 -bsp0 "$output_dir/$name.zip" "$name")
    ;;
  linux-*)
    cp -R "$publish_dir" "$staging/$name"
    chmod +x "$staging/$name/YKanBan"
    tar -C "$staging" -czf "$output_dir/$name.tar.gz" "$name"
    ;;
  osx-*)
    app="$staging/YKanBan.app"
    mkdir -p "$app/Contents/MacOS"
    cp -R "$publish_dir/." "$app/Contents/MacOS/"
    chmod +x "$app/Contents/MacOS/YKanBan"
    sed "s/@VERSION@/${version}/g" "$script_dir/macos/Info.plist" > "$app/Contents/Info.plist"
    if command -v codesign > /dev/null 2>&1; then
      # Ad-hoc signature for the bundle. Not fatal: the SDK already ad-hoc
      # signs the apphost, which is all arm64 needs to run, and --deep may
      # trip over the non-Mach-O files in Contents/MacOS.
      if ! codesign --force --deep --sign - "$app"; then
        echo "warning: ad-hoc signing of YKanBan.app failed; packing it unsigned" >&2
      fi
    fi
    tar -C "$staging" -czf "$output_dir/$name.tar.gz" YKanBan.app
    ;;
  *)
    echo "unsupported RID: $rid" >&2
    exit 2
    ;;
esac

echo "packed $rid -> $output_dir"
