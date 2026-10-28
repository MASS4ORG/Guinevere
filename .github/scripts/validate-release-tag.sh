#!/usr/bin/env bash
set -euo pipefail

release_ref="${GITHUB_REF:-}"
if [[ ! "$release_ref" =~ ^refs/tags/v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]]; then
    echo "::error::Publishing requires a stable release tag in the form vX.Y.Z." >&2
    exit 1
fi

release_commit="$(git rev-parse --verify "${release_ref}^{commit}")"
checkout_commit="$(git rev-parse --verify HEAD)"
if [[ "$release_commit" != "$checkout_commit" ]]; then
    echo "::error::The checked-out commit does not match the requested release tag." >&2
    exit 1
fi

echo "Validated release tag ${release_ref#refs/tags/} at ${checkout_commit}."
