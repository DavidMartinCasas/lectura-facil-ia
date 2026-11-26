#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# CI, criterio "Todo arranca con Docker Compose": levanta el compose base y comprueba /health a través de nginx, la
# página de inicio y sus cabeceras de seguridad. Se ejecuta desde la raíz del repositorio, con deploy/.env creado.
set -euo pipefail

readonly base_url=http://localhost:${WEB_PORT:-8080}
readonly health_attempts=30
readonly health_interval_seconds=2

docker compose -f deploy/docker-compose.yml up -d --wait

health=''
for _ in $(seq 1 "$health_attempts"); do
  health=$(curl -fsS "$base_url/health") && break
  sleep "$health_interval_seconds"
done
[[ $health == '{"status":"Healthy"}' ]] || { echo "::error::/health devuelve '$health'."; exit 1; }

curl -fsS "$base_url/" | grep -q "<title>Lectura Fácil IA</title>"
headers=$(curl -fsSI "$base_url/")
grep -qi "^content-security-policy: default-src 'self'" <<< "$headers"
grep -qi "^x-content-type-options: nosniff" <<< "$headers"
