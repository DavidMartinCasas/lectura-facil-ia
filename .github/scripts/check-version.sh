#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Comprueba desde fuera que /health responde 200 con la versión desplegada en la cabecera X-App-Version (US-19).
#
#   .github/scripts/check-version.sh <url-de-health> <sha> [segundos]   (por defecto espera 120 s)
set -euo pipefail

url=${1:?Indica la URL de /health.}
sha=${2:?Indica el SHA que se espera.}
timeout=${3:-120}
deadline=$((SECONDS + timeout))

matches() {
  local headers
  headers=$(curl -fsS --max-time 10 -o /dev/null -D - "$url") || return 1
  printf '%s\n' "$headers" | tr -d '\r' | grep -Eqi "^x-app-version:[[:space:]]*${sha}[[:space:]]*$"
}

until matches; do
  if ((SECONDS >= deadline)); then
    echo "::error::$url no responde 200 con X-App-Version: $sha."
    exit 1
  fi
  sleep 5
done
echo "$url responde con la versión $sha."
