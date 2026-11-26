#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Prueba de deploy/scripts/deploy.sh sin servidor (US-19): un registro local hace de GHCR y se despliega el compose real
# con la capa de producción, sobre una red de prueba y en un proyecto de compose aparte.
#
# Necesita Docker, curl, deploy/.env y las imágenes ya construidas:
#   docker compose -f deploy/docker-compose.yml build
#   tests/deploy/test-deploy.sh
#
# Casos: solo commits de main (en un repositorio git de prueba), primer despliegue, actualización con copia de la base,
# versión rota e imagen que no existe (vuelven a la anterior), despliegue por digest con la etiqueta reescrita en el
# registro, digest que no existe (vuelve a la anterior) y versiones no válidas (no tocan nada).
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
readonly unknown=5555555555555555555555555555555555555555
readonly absent_digest=sha256:0000000000000000000000000000000000000000000000000000000000000000
readonly LOG_LINES_ON_FAILURE=50
readonly MANIFEST_TYPES='application/vnd.oci.image.index.v1+json, application/vnd.oci.image.manifest.v1+json, application/vnd.docker.distribution.manifest.list.v2+json, application/vnd.docker.distribution.manifest.v2+json'

work_dir=$(mktemp -d)
export COMPOSE_PROJECT_NAME=lf-deploy-test
export PUBLIC_NETWORK=lf-deploy-test-public
export IMAGE_REGISTRY=localhost:$registry_port/
export DEPLOY_SYNC_REPO=false
export DEPLOY_STATE_FILE=$work_dir/release
export DEPLOY_HEALTH_TIMEOUT=${DEPLOY_HEALTH_TIMEOUT:-120}
export BACKUP_DIR=$work_dir/backups

deployed_release() { cat "$DEPLOY_STATE_FILE" 2>/dev/null || true; }

# Con el proyecto de la prueba (para exec, logs y down basta el nombre del proyecto; la imagen no importa).
compose() {
  IMAGE_TAG=local docker compose \
    -f "$repo_root/deploy/docker-compose.yml" -f "$repo_root/deploy/docker-compose.prod.yml" "$@"
}

clean_up() {
  local status=$?
  if ((status != 0)); then
    compose ps -a || true
    compose logs --no-color --tail "$LOG_LINES_ON_FAILURE" || true
  fi
  compose down -v --remove-orphans >/dev/null 2>&1 || true
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

# Digest con el que el registro sirve ahora mismo una etiqueta.
registry_digest() {
  local name=$1 tag=$2
  curl -fsSI -H "Accept: $MANIFEST_TYPES" "http://localhost:$registry_port/v2/$name/manifests/$tag" \
    | tr -d '\r' | sed -n 's/^[Dd]ocker-[Cc]ontent-[Dd]igest:[[:space:]]*//p'
}

# Versión que responde /health ahora mismo, a través del nginx de la web.
served_version() {
  compose exec -T web wget -q -S -T 5 -O - http://127.0.0.1:8080/health 2>&1 >/dev/null \
    | tr -d '\r' | sed -n 's/^[[:space:]]*[Xx]-[Aa]pp-[Vv]ersion:[[:space:]]*//p'
}

# expect_deploy <código> [argumentos de deploy.sh…]. Sin argumentos, la versión llega en SSH_ORIGINAL_COMMAND.
expect_deploy() {
  local expected_status=$1 status=0
  shift
  "$deploy_script" "$@" || status=$?
  [[ $status == "$expected_status" ]] \
    || fail "deploy.sh ${*:-(por SSH)} terminó con $status y se esperaba $expected_status."
}

# expect_running <sha> [<versión guardada>]: /health devuelve el SHA y deploy/.release guarda esa versión.
expect_running() {
  local sha=$1 release=${2:-$1}
  [[ $(deployed_release) == "$release" ]] || fail "La versión guardada es '$(deployed_release)' y se esperaba '$release'."
  [[ $(served_version) == "$sha" ]] || fail "/health devuelve la versión '$(served_version)' y se esperaba $sha."
}

git_test() { git -c user.name=Prueba -c user.email=prueba@example.invalid -c commit.gpgsign=false "$@"; }

# Un remoto con main y otra rama; el script copiado en un clon. Solo se acepta lo que está en main.
check_only_main_is_deployed() {
  local origin=$work_dir/origin.git clone=$work_dir/clone other status sha
  git init --quiet --bare "$origin"
  git clone --quiet "$origin" "$clone" 2>/dev/null
  mkdir -p "$clone/deploy/scripts"
  cp "$deploy_script" "$clone/deploy/scripts/deploy.sh"
  git_test -C "$clone" add deploy
  git_test -C "$clone" commit --quiet -m main
  git -C "$clone" push --quiet origin HEAD:refs/heads/main
  git_test -C "$clone" commit --quiet --allow-empty -m "otra rama"
  git -C "$clone" push --quiet origin HEAD:refs/heads/otra
  other=$(git -C "$clone" rev-parse HEAD)

  for sha in "$other" "$unknown"; do
    status=0
    DEPLOY_SYNC_REPO=true DEPLOY_STATE_FILE=$work_dir/clone-release "$clone/deploy/scripts/deploy.sh" "$sha" || status=$?
    [[ $status == 2 ]] || fail "deploy.sh $sha (fuera de main) terminó con $status y se esperaba 2."
  done
  [[ $(git -C "$clone" rev-parse HEAD) == "$other" ]] || fail "El repositorio cambió de commit con un SHA rechazado."
}

[[ -f $repo_root/deploy/.env ]] || fail "Falta deploy/.env (cópialo de deploy/.env.example)."

check_only_main_is_deployed
pass "solo se despliegan commits de main"

docker run -d --rm --name "$registry_container" -p "127.0.0.1:$registry_port:5000" "$registry_image" >/dev/null
docker network create "$PUBLIC_NETWORK" >/dev/null

# Dos versiones buenas y una rota: la "API" rota es la imagen de la web, así que migrate falla al arrancar.
for tag in "$first" "$second"; do
  publish lectura-facil-api:local lectura-facil-api "$tag"
  publish lectura-facil-web:local lectura-facil-web "$tag"
done
publish lectura-facil-web:local lectura-facil-api "$broken"
publish lectura-facil-web:local lectura-facil-web "$broken"

# A mano, sin digests: por etiqueta.
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

# Como el workflow: por SSH, con los digests de lo publicado. Después alguien reescribe la etiqueta de la API en el
# registro con otra imagen: por digest se sigue desplegando la buena.
api_digest=$(registry_digest lectura-facil-api "$second")
web_digest=$(registry_digest lectura-facil-web "$second")
[[ -n $api_digest && -n $web_digest ]] || fail "No se han podido leer los digests del registro."
publish lectura-facil-web:local lectura-facil-api "$second"
[[ $(registry_digest lectura-facil-api "$second") != "$api_digest" ]] || fail "La etiqueta no se ha reescrito."
with_digests="$second $api_digest $web_digest"
SSH_ORIGINAL_COMMAND=$with_digests expect_deploy 0
expect_running "$second" "$with_digests"
pass "por digest: una etiqueta reescrita en el registro no cambia la imagen"

SSH_ORIGINAL_COMMAND="$first $absent_digest $web_digest" expect_deploy 1
expect_running "$second" "$with_digests"
pass "digest que no existe: vuelve a la anterior (por digest) y falla"

expect_deploy 2 "not-a-sha"
expect_deploy 2 "$first" "sha256:abc" "$web_digest"
expect_deploy 2 "$first" "$api_digest"
SSH_ORIGINAL_COMMAND=$first expect_deploy 2
SSH_ORIGINAL_COMMAND="$first; rm -rf /" expect_deploy 2
SSH_ORIGINAL_COMMAND="$with_digests; id" expect_deploy 2
SSH_ORIGINAL_COMMAND=$'\n'"$with_digests"$'\nid' expect_deploy 2
SSH_ORIGINAL_COMMAND="$with_digests"$'\nid' expect_deploy 2
expect_running "$second" "$with_digests"
pass "versión no válida o sin digests por SSH: no toca nada"
