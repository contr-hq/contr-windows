#!/usr/bin/env bash
# Regenerates gen/Contr.Sdk with contr-sdkgen from one commit of the core repository.
# Usage: scripts/update-sdk.sh <contr-checkout> [ref]    (ref defaults to HEAD)
set -euo pipefail

core=$(cd "${1:?usage: scripts/update-sdk.sh <contr-checkout> [ref]}" && pwd)
ref=${2:-HEAD}
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
commit=$(git -C "$core" rev-parse --verify "$ref^{commit}")

work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# Generating from an exported tree keeps uncommitted changes in the checkout out of the SDK.
mkdir "$work/src"
git -C "$core" archive "$commit" | tar -x -C "$work/src"
(
    cd "$work/src"
    CARGO_TARGET_DIR=${CARGO_TARGET_DIR:-$core/target} \
        cargo run --quiet --locked -p contr-sdkgen -- generate --lang csharp --out "$work/out"
)

rm -rf "$root/gen/Contr.Sdk"
cp -R "$work/out/Contr.Sdk" "$root/gen/Contr.Sdk"
cp "$work/out/Directory.Build.props" "$root/gen/Directory.Build.props"
printf '%s\n' "$commit" >"$root/gen/CORE_COMMIT"

echo "gen/Contr.Sdk now matches contr-hq/contr@$commit"
