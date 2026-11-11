#!/usr/bin/env bash
# Builds SkynettMiniMap.exe on Linux (Arch/CachyOS) with the Roslyn compiler from
# the .NET SDK against the .NET Framework 4.8 reference assemblies (NuGet).
# Mirrors scripts/Invoke-MiniMapBuild.ps1, minus the Windows-only self-tests.
#
#   linux/build.sh [Release|Test] [output-dir]
#
# Environment:
#   NETFX_REFS   directory with the net48 reference assemblies (otherwise downloaded)
#   CSC          path to csc.dll (otherwise searched in the dotnet SDK)
set -euo pipefail

CONFIG="${1:-Release}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RES="$ROOT/resources"
PACK="$ROOT/packaging"
NETFX_PKG_VERSION="1.0.3"
NETFX_PKG_SHA256="8a7e348538e7eb91351696911689f49e3d4f63f8bab517432bbe159b8b1104a2"

case "$CONFIG" in Release|Test) ;; *) echo "Configuration must be Release or Test" >&2; exit 2;; esac

version="$(grep -oP 'public const string VersionString\s*=\s*"\K[0-9]+\.[0-9]+\.[0-9]+(?=";)' "$ROOT/src/MiniMap.cs")"
[[ -n "$version" ]] || { echo "Invalid VersionString in src/MiniMap.cs" >&2; exit 1; }
OUT="${2:-$ROOT/release/linux/v$version}"
OUT="$(realpath -m "$OUT")"
[[ -e "$OUT" ]] && { echo "Output already exists: $OUT" >&2; exit 1; }

[[ "$(head -c4 "$RES/map-tiles.bin" 2>/dev/null)" == "MTL2" ]] || { echo "Missing or outdated resources/map-tiles.bin" >&2; exit 1; }

# --- compiler ---------------------------------------------------------------
if [[ -z "${CSC:-}" ]]; then
    for d in /usr/share/dotnet /usr/lib/dotnet "$HOME/.dotnet" /opt/dotnet; do
        c="$(ls -1d "$d"/sdk/*/Roslyn/bincore/csc.dll 2>/dev/null | sort -V | tail -n1 || true)"
        [[ -n "$c" ]] && { CSC="$c"; break; }
    done
fi
[[ -f "${CSC:-}" ]] || { echo "csc.dll not found. Install dotnet-sdk (pacman -S dotnet-sdk) or set CSC." >&2; exit 1; }

# --- reference assemblies ----------------------------------------------------
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
if [[ -z "${NETFX_REFS:-}" ]]; then
    cache="${XDG_CACHE_HOME:-$HOME/.cache}/scum-minimap"
    nupkg="$cache/microsoft.netframework.referenceassemblies.net48.$NETFX_PKG_VERSION.nupkg"
    if [[ ! -f "$nupkg" ]]; then
        mkdir -p "$cache"
        curl -fsSL -o "$nupkg.part" "https://api.nuget.org/v3-flatcontainer/microsoft.netframework.referenceassemblies.net48/$NETFX_PKG_VERSION/microsoft.netframework.referenceassemblies.net48.$NETFX_PKG_VERSION.nupkg"
        mv "$nupkg.part" "$nupkg"
    fi
    echo "$NETFX_PKG_SHA256  $nupkg" | sha256sum -c --quiet -
    mkdir -p "$WORK/refs"
    bsdtar -xf "$nupkg" -C "$WORK/refs" 'build/.NETFramework/v4.8/*.dll' 2>/dev/null || unzip -q "$nupkg" 'build/.NETFramework/v4.8/*.dll' -d "$WORK/refs"
    NETFX_REFS="$WORK/refs/build/.NETFramework/v4.8"
fi

# Same default references as csc.exe's csc.rsp plus the explicit build references.
refs=()
for a in mscorlib Accessibility Microsoft.CSharp System System.Configuration System.Core System.Data \
         System.Data.DataSetExtensions System.Deployment System.Design System.Drawing System.Management \
         System.Net.Http System.Numerics System.Runtime.Serialization System.Security System.ServiceProcess \
         System.Transactions System.Web System.Web.Extensions System.Windows.Forms System.Xml System.Xml.Linq \
         System.Xaml System.IO.Compression System.IO.Compression.FileSystem PresentationCore PresentationFramework WindowsBase; do
    [[ -f "$NETFX_REFS/$a.dll" ]] && refs+=("/reference:$NETFX_REFS/$a.dll")
done

# --- sources (order from scripts/Get-MiniMapSources.ps1) ---------------------
mapfile -t srcnames < <(grep -oP "'[A-Za-z.]+\.cs'" "$ROOT/scripts/Get-MiniMapSources.ps1" | tr -d "'")
STAGE="$WORK/stage"; mkdir -p "$STAGE/assets"
sources=()
for s in "${srcnames[@]}"; do
    if [[ "$s" == "MiniMap.cs" ]]; then
        product="$version"; [[ "$CONFIG" == Test ]] && product="$version-test"
        sed -E \
            -e "s/\[assembly:\s*AssemblyVersion\(\"[^\"]+\"\)\]/[assembly: AssemblyVersion(\"$version.0\")]/" \
            -e "s/\[assembly:\s*AssemblyFileVersion\(\"[^\"]+\"\)\]/[assembly: AssemblyFileVersion(\"$version.0\")]/" \
            -e "s/\[assembly:\s*AssemblyInformationalVersion\(\"[^\"]+\"\)\]/[assembly: AssemblyInformationalVersion(\"$product\")]/" \
            "$ROOT/src/MiniMap.cs" > "$WORK/MiniMap.build.cs"
        sources+=("$WORK/MiniMap.build.cs")
    else
        sources+=("$ROOT/src/$s")
    fi
done
[[ ${#sources[@]} -ge 50 ]] || { echo "Source list looks incomplete (${#sources[@]})" >&2; exit 1; }

# --- resources ------------------------------------------------------------------
manifest="$WORK/installation-assets.txt"; : > "$manifest"
for a in map-tiles.bin roads.bin; do
    cp "$RES/$a" "$STAGE/assets/"
    echo "$a=$(sha256sum "$STAGE/assets/$a" | cut -d' ' -f1)" >> "$manifest"
done
embedded=()
for r in zones.tsv water-mask.bin scummap.bin spawn-items.tsv spawn-compatibility.tsv spawn-item-names-pl.tsv keyboard-setup.png steam-logo.png; do
    embedded+=("/resource:$RES/$r,$r")
done
for f in BarlowSemiCondensed-Regular.ttf BarlowSemiCondensed-SemiBold.ttf; do embedded+=("/resource:$RES/fonts/$f,$f"); done
embedded+=("/resource:$manifest,installation-assets.txt")
for lang in es-AR fr de nl ru zh-CN tr ar pt-BR pt-PT ko th zh-TW ja cs it uk fi lt sw; do
    for p in spawn-item-names spawn-category-names; do embedded+=("/resource:$RES/$p-$lang.tsv,$p-$lang.tsv"); done
done
embedded+=("/resource:$PACK/detect_zones.py,detect_zones.py")
if [[ -d "$RES/voice-navigation" ]]; then
    while IFS= read -r -d '' mp3; do
        voice="$(basename "$(dirname "$mp3")")"
        embedded+=("/resource:$mp3,voice-navigation/$voice/$(basename "$mp3")")
    done < <(find "$RES/voice-navigation" -mindepth 2 -maxdepth 2 -name '*.mp3' -print0 | sort -z)
fi

defines=(); exe="SkynettMiniMap.exe"
[[ "$CONFIG" == Test ]] && { defines=(/define:MINIMAP_TEST); exe="SkynettMiniMap-Test.exe"; }

echo "Compiling SCUM MiniMap v$version ($CONFIG) with $(basename "$(dirname "$(dirname "$(dirname "$CSC")")")") Roslyn ..."
dotnet "$CSC" /nologo /nostdlib+ /noconfig /codepage:65001 /optimize+ /deterministic+ /target:winexe /platform:x64 /langversion:7.3 \
    /nowarn:1701,1702,8032 "${defines[@]}" "${refs[@]}" "${embedded[@]}" \
    "/out:$STAGE/$exe" "/win32icon:$RES/App-Icon.ico" "/win32manifest:$PACK/MiniMap.manifest" "${sources[@]}"

cp "$ROOT/ATTRIBUTION.md" "$ROOT/README.md" "$ROOT/CHANGELOG.md" "$RES/keyboard-setup.png" "$STAGE/"
{
    echo "version=$version"
    echo "$exe=$(sha256sum "$STAGE/$exe" | cut -d' ' -f1)"
    sed 's#^#assets/#' "$manifest"
} > "$STAGE/package.txt"

mkdir -p "$(dirname "$OUT")"
mv "$STAGE" "$OUT"
echo "$CONFIG package ready: $OUT"
