#!/bin/bash
# Deploy script for Playr backend (.NET API)
# Run this on the server (viktor@87.106.19.210) inside ~/apps/playr-api-src
set -euo pipefail

export PATH="$PATH:$HOME/.dotnet:$HOME/.dotnet/tools"
export DOTNET_ROOT="$HOME/.dotnet"

REPO_DIR="$HOME/apps/playr-api-src"
PUBLISH_DIR="$HOME/apps/playr-api-publish"
ENV_FILE="$HOME/apps/playr-api.env"

if [ ! -f "$ENV_FILE" ]; then
  echo "ERROR: $ENV_FILE not found. Copy deploy/playr-api.env.example there and fill in real values."
  exit 1
fi
set -a
source "$ENV_FILE"
set +a

echo "==> Pulling latest changes"
cd "$REPO_DIR"
git pull

echo "==> Publishing (Release)"
dotnet publish src/Playr.Api/Playr.Api.csproj -c Release -o "$PUBLISH_DIR"

echo "==> Applying database migrations"
(
  cd src/Playr.Api
  dotnet ef database update --project ../Playr.Infrastructure/Playr.Infrastructure.csproj --startup-project .
)

echo "==> Restarting service"
sudo systemctl restart playr-api
sleep 2
sudo systemctl status playr-api --no-pager | head -8

echo "==> Done. Health check:"
curl -s http://127.0.0.1:5258/health
echo
