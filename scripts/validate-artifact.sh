#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-/opt/data/home/.dotnet/dotnet}"
PLUGIN_PROJECT="$ROOT_DIR/Emby.Plugin.Danmu/Emby.Plugin.Danmu.csproj"
TEST_PROJECT="$ROOT_DIR/Emby.Plugin.Danmu.Tests/Emby.Plugin.Danmu.Tests.csproj"
VALIDATOR_PROJECT="$ROOT_DIR/tools/ArtifactValidator/ArtifactValidator.csproj"
BUILD_OUTPUT_DIR="$ROOT_DIR/Emby.Plugin.Danmu/bin/Debug/netstandard2.0"
BUILD_PLUGIN_DLL="$BUILD_OUTPUT_DIR/Emby.Plugin.Danmu.dll"
DOC_PLUGIN_DLL="$ROOT_DIR/doc/Emby.Plugin.Danmu.dll"

if [[ ! -x "$DOTNET_BIN" ]]; then
  echo "dotnet not found or not executable: $DOTNET_BIN" >&2
  exit 2
fi

echo "[1/4] Build plugin"
"$DOTNET_BIN" build "$PLUGIN_PROJECT" -v minimal

echo "[2/4] Run regression tests"
"$DOTNET_BIN" test "$TEST_PROJECT" -v minimal --no-build

echo "[3/4] Validate built artifact via reflection"
"$DOTNET_BIN" run --project "$VALIDATOR_PROJECT" -- "$BUILD_PLUGIN_DLL" "$BUILD_OUTPUT_DIR"

echo "[4/4] Compare packaged doc artifact"
if [[ -f "$DOC_PLUGIN_DLL" ]]; then
  build_sha="$(sha256sum "$BUILD_PLUGIN_DLL" | awk '{print $1}')"
  doc_sha="$(sha256sum "$DOC_PLUGIN_DLL" | awk '{print $1}')"
  echo "build dll sha256: $build_sha"
  echo "doc   dll sha256: $doc_sha"
  if [[ "$build_sha" != "$doc_sha" ]]; then
    echo "WARNING: $DOC_PLUGIN_DLL is stale and does not match the freshly built artifact." >&2
    echo "Use $BUILD_PLUGIN_DLL for Emby installation / isolated verification." >&2
  fi
else
  echo "Skip doc artifact comparison: $DOC_PLUGIN_DLL not found"
fi

echo "Validation completed successfully."
