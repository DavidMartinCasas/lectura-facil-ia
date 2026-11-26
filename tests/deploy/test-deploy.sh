#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Prueba de deploy/scripts/deploy.sh sin servidor (US-19): un registro local hace de GHCR y se despliega el compose real
# con la capa de producción, sobre una red de prueba y en un proyecto de compose aparte.
#
# Necesita Docker, deploy/.env y las imágenes ya construidas:
#   docker compose -f deploy/docker-compose.yml build
#   tests/deploy/test-deploy.sh
#
# Casos: primer despliegue, actualización con copia de la base, versión rota (vuelve a la anterior), imagen que no
# existe (vuelve a la anterior) y SHA no válido (no toca nada).
set -euo pipefail

repo_root=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)
deploy_script=$repo_root/deploy/scripts/deploy.sh
registry_image='registry:3.1.2@sha256:82f6ff4a7f982960c8aa51617fea3aabc485df6e55cce80d22deb1de36f1bc3c'
registry_port=${TEST_REGISTRY_PORT:-5555}
registry_container=lf-deploy-test-registry

readonly first=1111111111111111111111111111111111111111
readonly second=2222222222222222222222222222222222222222
readonly broken=3333333333333333333333333333333333333333
readonly missing=4444444444444444444444444444444444444444

work_dir=$(mktemp -d)
export COMPOSE_PROJECT_NAME=lf-deploy-test
export PUBLIC_NETWORK=lf-deploy-test-public
export IMAGE_REGISTRY=localhost:$registry_port/
export DEPLOY_SYNC_REPO=false
export DEPLOY_STATE_FILE=$work_dir/release
export DEPLOY_HEALTH_TIMEOUT=${DEPLOY_HEALTH_TIMEOUT:-120}
export BACKUP_DIR=$work_dir/backups

compose() {
  IMAGE_TAG=$(cat "$DEPLOY_STATE_FILE") docker compose \
    -f "$repo_root/deploy/docker-compose.yml" -f "$repo_root/deploy/docker-compose.prod.yml" "$@"
}

clean_up() {
  if [[ -f $DEPLOY_STATE_FILE ]]; then
    compose down -v --remove-orphans >/dev/null 2>&1 || true
  fi
  docker rm -f "$registry_container" >/dev/null 2>&1 || true
  docker network rm "$PUBLIC_NETWORK" >/dev/null 2>&1 || true
  rm -rf "$work_dir"
}
trap clean_up EXIT

fail() {
  printf 'FALLO: %s\n' "$*" >&2
  exit 1
}

pass() { printf 'OK: %s\n' "$*"; }

publish() {
  local source=$1 name=$2 tag=$3
  docker tag "$source" "${IMAGE_REGISTRY}$name:$tag"
  docker push --quiet "${IMAGE_REGISTRY}$name:$tag" >/dev/null
}

deployed_release() { cat "$DEPLOY_STATE_FILE"; }

# Versión que responde /health ahora mismo, a través del nginx de la web.
served_version() {
  compose exec -T web wget -q -S -T 5 -O - http://127.0.0.1:8080/health 2>&1 >/dev/null \
    | tr -d '\r' | sed -n 's/^[[:space:]]*[Xx]-[Aa]pp-[Vv]ersion:[[:space:]]*//p'
}

expect_deploy() {
  local expected_status=$1 sha=$2 status=0
  "$deploy_script" "$sha" || status=$?
  [[ $status == "$expected_status" ]] || fail "deploy.sh $sha terminó con $status y se esperaba $expected_status."
}

expect_running() {
  local sha=$1
  [[ $(deployed_release) == "$sha" ]] || fail "La versión guardada es $(deployed_release) y se esperaba $sha."
  [[ $(served_version) == "$sha" ]] || fail "/health devuelve la versión '$(served_version)' y se esperaba $sha."
}

[[ -f $repo_root/deploy/.env ]] || fail "Falta deploy/.env (cópialo de deploy/.env.example)."

docker run -d --rm --name "$registry_container" -p "127.0.0.1:$registry_port:5000" "$registry_image" >/dev/null
docker network create "$PUBLIC_NETWORK" >/dev/null

# Dos versiones buenas y una rota: la "API" rota es la imagen de la web, así que migrate falla al arrancar.
for tag in "$first" "$second"; do
  publish lectura-facil-api:local lectura-facil-api "$tag"
  publish lectura-facil-web:local lectura-facil-web "$tag"
done
publish lectura-facil-web:local lectura-facil-api "$broken"
publish lectura-facil-web:local lectura-facil-web "$broken"

expect_deploy 0 "$first"
expect_running "$first"
pass "primer despliegue"

expect_deploy 0 "$second"
expect_running "$second"
ls "$BACKUP_DIR"/lectura-facil-*.dump >/dev/null 2>&1 || fail "No se hizo la copia de la base antes de actualizar."
pass "actualización con copia de la base"

expect_deploy 1 "$broken"
expect_running "$second"
pass "versión rota: vuelve a la anterior y falla"

expect_deploy 1 "$missing"
expect_running "$second"
pass "imagen que no existe: vuelve a la anterior y falla"

expect_deploy 2 "not-a-sha"
SSH_ORIGINAL_COMMAND="$first; rm -rf /" expect_deploy 2 ""
expect_running "$second"
pass "SHA no válido: no toca nada"
