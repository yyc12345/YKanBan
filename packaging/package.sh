#!/usr/bin/env bash
# Packs one published RID into a release artifact.
#
#   packaging/package.sh <rid> <publish-dir> <output-dir> <version>
#
# win-x64   -> YKanBan-<version>-win-x64-setup.exe  (Inno Setup installer)
# linux-x64 -> YKanBan-<version>-linux-x64.tar.gz (folder YKanBan-<version>-linux-x64/)
# osx-*     -> YKanBan-<version>-<rid>.dmg        (YKanBan.app in a drag-to-Applications disk image, ad-hoc signed when possible)
#
# tar.gz/dmg keep the executable bit, which a plain zip artifact upload would lose.
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
    # The Windows runners ship Inno Setup with ISCC on PATH. Git Bash rewrites
    # arguments that start with "/" into POSIX paths, so disable the conversion
    # and hand ISCC native Windows paths via cygpath.
    MSYS_NO_PATHCONV=1 iscc \
      "/DMyAppVersion=${version}" \
      "/DPublishDir=$(cygpath -w "${publish_dir}")" \
      "/DOutputDir=$(cygpath -w "${output_dir}")" \
      "$(cygpath -w "${script_dir}/windows/ykanban.iss")"
    ;;
  linux-*)
    cp -R "$publish_dir" "$staging/$name"
    chmod +x "$staging/$name/YKanBan"
    tar -C "$staging" -czf "$output_dir/$name.tar.gz" "$name"
    ;;
  osx-*)
    app="$staging/YKanBan.app"
    mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
    cp -R "$publish_dir/." "$app/Contents/MacOS/"
    cp "$script_dir/macos/ykanban.icns" "$app/Contents/Resources/"
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
    ln -s /Applications "$staging/Applications"
    hdiutil create -volname "YKanBan" -srcfolder "$staging" -format UDZO -ov "$output_dir/$name.dmg"
    ;;
  *)
    echo "unsupported RID: $rid" >&2
    exit 2
    ;;
esac

echo "packed $rid -> $output_dir"
