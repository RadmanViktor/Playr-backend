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

# Read the env file the way systemd's EnvironmentFile= does: treat everything
# after the first '=' as a literal value.
#
# `source` cannot be used here. It runs the file as bash, so an unquoted
# connection string like
#     ConnectionStrings__DefaultConnection=Host=localhost;Port=5432;...
# is cut at the first ';' - the variable silently becomes "Host=localhost" and
# the remaining fields leak out as separate variables. No error is raised, and
# `dotnet ef` then fails with "No password has been provided".
#
# Trailing \r is stripped so a file saved with Windows line endings still works.
set -a
while IFS= read -r line || [ -n "$line" ]; do
  line="${line%$'\r'}"
  case "$line" in ''|'#'*) continue ;; esac
  [ "${line#*=}" = "$line" ] && continue
  export "${line%%=*}=${line#*=}"
done < "$ENV_FILE"
set +a

if [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
  echo "ERROR: ConnectionStrings__DefaultConnection is not set in $ENV_FILE"
  exit 1
fi

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

echo "==> Waiting for health check"
# The app can take a few seconds to bind its port after systemd reports the
# service as active, so retry instead of checking once - a single early
# "Connection refused" would otherwise fail this whole script (set -e) even
# though the restart itself succeeded and the app comes up moments later.
health_ok=false
for attempt in 1 2 3 4 5 6 7 8 9 10; do
  if curl -sS -f http://127.0.0.1:5258/health; then
    echo
    health_ok=true
    break
  fi
  sleep 1
done

if [ "$health_ok" != true ]; then
  echo "ERROR: health check did not succeed within 10 seconds after restart."
  exit 1
fi

echo "==> Done."
