#!/usr/bin/env bash
# Uploads the API or the assistant to a Hugging Face Space (Docker SDK); the Space builds and runs the Dockerfile.
#   scripts/deploy-space.sh api   <hf-user>/<space-name>
#   scripts/deploy-space.sh agent <hf-user>/<space-name>
# Needs: pip install huggingface_hub, then `hf auth login` with a write token. Settings and secrets are set on the
# Space's Settings page (see ".md files/DEPLOYMENT.md"); nothing secret is uploaded.
set -euo pipefail

kind=${1:?api or agent}
space=${2:?<hf-user>/<space-name>}
root=$(cd "$(dirname "$0")/.." && pwd)
case $kind in
  api) source_dir=$root/QadoosFlourMill/Kratix.ERP; title="Kratix ERP API" ;;
  agent) source_dir=$root/kratix-agent; title="Kratix ERP Assistant" ;;
  *) echo "First argument must be api or agent" >&2; exit 1 ;;
esac

stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
# Only what the Docker build needs; never local settings, keys, build output or chat data.
tar -C "$source_dir" -cf - \
  --exclude=bin --exclude=obj --exclude=.venv --exclude=__pycache__ --exclude=.pytest_cache --exclude=data \
  --exclude=tests --exclude=.env --exclude='appsettings.Development.json' --exclude='appsettings.*.local.json' \
  --exclude='*.md' --exclude='*.http' . | tar -C "$stage" -xf -
# The Space's settings live in its README front matter.
cat > "$stage/README.md" <<EOF
---
title: $title
emoji: 🌾
colorFrom: yellow
colorTo: gray
sdk: docker
app_port: 10000
pinned: false
---
Backend for the Kratix ERP portfolio demo.
EOF

hf upload "$space" "$stage" . --repo-type space --commit-message "Deploy $kind"
echo "Uploaded. The Space rebuilds now: https://huggingface.co/spaces/$space"
