#!/usr/bin/env bash
# One-command demo reset (docs/05 P18): wipes the local Postgres volume, brings the stack back up,
# applies every migration, then starts the web app once so DbSeeder's Development-only seed step runs
# (four candidates, recruiter/assessor/partner, five opportunities, Anko's pre-analysed demo repository
# and pre-generated interview). Safe to run as many times as you like before a rehearsal or the real
# demo; it is destructive to local data only (never run this against anything but the dev stack).
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."

echo "==> Stopping the dev stack and wiping its volumes..."
docker compose -f docker/docker-compose.yml down -v

# minio/IBlobStore is declared in docker-compose.yml but not used by any implemented feature yet,
# so it's skipped here to avoid an unnecessary image pull.
echo "==> Starting Postgres and Mailpit..."
docker compose -f docker/docker-compose.yml up -d postgres mailpit

echo "==> Waiting for Postgres to be ready..."
until docker compose -f docker/docker-compose.yml exec -T postgres pg_isready -U herproof >/dev/null 2>&1; do
  sleep 1
done

echo "==> Applying migrations..."
dotnet ef database update -p src/HerProof.Infrastructure -s src/HerProof.Web

echo "==> Starting the web app once to run DbSeeder (Development only), then stopping it..."
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/HerProof.Web --urls http://localhost:5280 &
APP_PID=$!
for _ in $(seq 1 30); do
  if curl -s -o /dev/null http://localhost:5280/; then
    break
  fi
  sleep 1
done
kill "$APP_PID" 2>/dev/null || true
wait "$APP_PID" 2>/dev/null || true

echo "==> Demo data reset. Start the app for real with:"
echo "    dotnet run --project src/HerProof.Web"
