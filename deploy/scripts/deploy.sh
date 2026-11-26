#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Despliega en el servidor una versión (las imágenes publicadas con la etiqueta de un commit) y vuelve a la anterior si
# falla. Lo ejecuta el workflow de despliegue por SSH (docs/despliegue.md, "Despliegue automático"); también sirve a mano:
#
#   deploy/scripts/deploy.sh <sha> [<digest-api> <digest-web>]
#
# <sha> es el commit completo (40 caracteres hexadecimales); los digests (sha256:<64 hexadecimales>), los de las imágenes
# que publicó el workflow. Con ellos se descarga exactamente esa imagen, aunque alguien reescriba la etiqueta en el
# registro. Con una clave limitada a este script (command= en authorized_keys), la versión llega en
# SSH_ORIGINAL_COMMAND y entonces los digests son obligatorios; a mano, sin digests, se descarga por etiqueta.
#
# Pasos: copia de la base → ficheros de deploy/ en ese commit → docker compose pull → up -d (migrate antes que la API)
# → espera a que /health responda 200 con X-App-Version igual al SHA. Si algo falla, vuelve a la versión anterior
# (guardada en DEPLOY_STATE_FILE) y termina con error.
#
# Las migraciones no se deshacen: la versión anterior debe funcionar con el esquema nuevo (migraciones compatibles
# hacia atrás). Si no, volver atrás exige restaurar la copia que se hace al empezar.
#
# En el servidor, la clave de la CI no ejecuta este fichero del repositorio sino una copia instalada fuera de él
# (DEPLOY_REPO_DIR apunta al repositorio): así el checkout de una versión, o la vuelta a una anterior, no cambia el
# script que atiende el siguiente despliegue. La copia se actualiza a mano (docs/despliegue.md, 9.1); si no es la de
# DEPLOY_BRANCH, el script lo avisa en cada despliegue.
#
# Variables opcionales:
#   DEPLOY_REPO_DIR         raíz del repositorio (por defecto, la que contiene este script: ../..).
#   DEPLOY_COMPOSE_FILES    ficheros de compose separados por ':', relativos a la raíz del repositorio
#                           (por defecto deploy/docker-compose.yml:deploy/docker-compose.prod.yml).
#   DEPLOY_STATE_FILE       fichero con la versión desplegada, SHA y digests (por defecto deploy/.release).
#   DEPLOY_HEALTH_TIMEOUT   segundos que se espera a /health (por defecto 180).
#   DEPLOY_SYNC_REPO        true (por defecto): solo despliega commits de DEPLOY_BRANCH y pone el repositorio en el
#                           commit desplegado. false no toca git ni comprueba la rama (solo para pruebas).
#   DEPLOY_GIT_REMOTE       remoto del que se descarga el commit (por defecto origin).
#   DEPLOY_BRANCH           rama de la que tiene que ser el commit (por defecto main).
#   BACKUP_*                se pasan a backup-postgres.sh.
set -euo pipefail

readonly RELEASE_PATTERN='^[0-9a-f]{40}( sha256:[0-9a-f]{64} sha256:[0-9a-f]{64})?$'
readonly RELEASE_WITH_DIGESTS_PATTERN='^[0-9a-f]{40} sha256:[0-9a-f]{64} sha256:[0-9a-f]{64}$'
readonly HEALTH_URL='http://127.0.0.1:8080/health'
readonly HEALTH_INTERVAL_SECONDS=3
readonly HEALTH_REQUEST_TIMEOUT_SECONDS=5

script_dir=$(CDPATH='' cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
runner=$script_dir/$(basename -- "${BASH_SOURCE[0]}")
repo_root=$(CDPATH='' cd -- "${DEPLOY_REPO_DIR:-$script_dir/../..}" && pwd)
state_file=${DEPLOY_STATE_FILE:-$repo_root/deploy/.release}
health_timeout=${DEPLOY_HEALTH_TIMEOUT:-180}
sync_repo=${DEPLOY_SYNC_REPO:-true}
git_remote=${DEPLOY_GIT_REMOTE:-origin}
deploy_branch=${DEPLOY_BRANCH:-main}
compose_args=()

log() { printf '[deploy] %s\n' "$*"; }
warn() { printf '[deploy] AVISO: %s\n' "$*" >&2; }
error() { printf '[deploy] ERROR: %s\n' "$*" >&2; }

usage() {
  error "uso: deploy.sh <sha> [<digest-api> <digest-web>] (SHA completo del commit y digests sha256:<64 hexadecimales>;"
  error "     por SSH, los digests son obligatorios)."
  exit 2
}

# Una versión es "<sha>" o "<sha> <digest-api> <digest-web>". Llega como argumentos o, con una clave limitada a este
# script, en SSH_ORIGINAL_COMMAND (y entonces con digests). Nada más se acepta.
requested_release() {
  local release
  if (($# > 0)); then
    release="$*"
    [[ $release =~ $RELEASE_PATTERN ]] || return 1
  else
    release=${SSH_ORIGINAL_COMMAND:-}
    [[ $release =~ $RELEASE_WITH_DIGESTS_PATTERN ]] || return 1
  fi
  printf '%s' "$release"
}

sha_of() { printf '%s' "${1%% *}"; }

check_settings() {
  if [[ ! -d $repo_root/deploy ]]; then
    error "$repo_root no es el repositorio de Lectura Fácil: revisa DEPLOY_REPO_DIR."
    exit 2
  fi
  case $health_timeout in
    '' | *[!0-9]*) error "DEPLOY_HEALTH_TIMEOUT debe ser un número de segundos."; exit 2 ;;
  esac
  case $sync_repo in
    true | false) ;;
    *) error "DEPLOY_SYNC_REPO debe ser true o false."; exit 2 ;;
  esac
  # Con una versión fija en deploy/.env, /health nunca devolvería el SHA y cada despliegue volvería atrás.
  # (Vacía también es OBSERVABILITY__RELEASE="" o ''.)
  if grep -Eq "^[[:space:]]*OBSERVABILITY__RELEASE=[\"']?[^[:space:]#\"']" "$repo_root/deploy/.env" 2>/dev/null; then
    error "Deja vacía OBSERVABILITY__RELEASE en deploy/.env: en el despliegue automático la versión es el SHA."
    exit 2
  fi
}

load_compose_files() {
  local files=${DEPLOY_COMPOSE_FILES:-deploy/docker-compose.yml:deploy/docker-compose.prod.yml}
  local file
  local -a files_list
  IFS=':' read -r -a files_list <<< "$files"
  for file in "${files_list[@]}"; do
    compose_args+=(-f "$repo_root/$file")
  done
}

# docker compose con las imágenes de una versión: la etiqueta del commit y, si los hay, los digests.
compose() {
  local sha api_digest web_digest
  read -r sha api_digest web_digest <<< "$1"
  shift
  IMAGE_TAG=$sha API_IMAGE_DIGEST=${api_digest:-} WEB_IMAGE_DIGEST=${web_digest:-} \
    docker compose "${compose_args[@]}" "$@"
}

previous_release() {
  local previous=''
  [[ -f $state_file ]] && previous=$(head -n 1 "$state_file")
  if [[ -n $previous && ! $previous =~ $RELEASE_PATTERN ]]; then
    error "$state_file no contiene una versión válida: no habrá versión a la que volver."
    previous=''
  fi
  printf '%s' "$previous"
}

save_release() {
  printf '%s\n' "$1" > "$state_file.tmp"
  mv "$state_file.tmp" "$state_file"
}

backup_database() {
  local release=$1
  if ! compose "$release" ps --status running --services 2>/dev/null | grep -qx postgres; then
    log "PostgreSQL no está en marcha: no se hace copia (primer despliegue)."
    return 0
  fi
  log "Copia de seguridad de la base de datos."
  "$repo_root/deploy/scripts/backup-postgres.sh"
}

# Solo commits de la rama de despliegue: aunque alguien tuviera la clave del CI, no podría desplegar una rama ni un
# commit suelto (con su docker-compose). Termina antes de tocar nada.
check_on_branch() {
  local sha=$1
  [[ $sync_repo == true ]] || return 0
  if ! git -C "$repo_root" fetch --quiet "$git_remote" \
    "+refs/heads/$deploy_branch:refs/remotes/$git_remote/$deploy_branch"; then
    error "No se ha podido descargar $git_remote/$deploy_branch."
    exit 1
  fi
  check_runner
  if ! git -C "$repo_root" merge-base --is-ancestor "$sha" "refs/remotes/$git_remote/$deploy_branch" 2>/dev/null; then
    error "$sha no está en $deploy_branch: no se despliega."
    exit 2
  fi
}

# El script que corre no cambia con el checkout (es la copia instalada): si no es el de la rama de despliegue, lo avisa
# para que se actualice a mano. Por ejemplo, cuando una PR cambia el formato que envía el workflow.
check_runner() {
  if ! git -C "$repo_root" show "refs/remotes/$git_remote/$deploy_branch:deploy/scripts/deploy.sh" 2>/dev/null \
    | cmp -s - "$runner"; then
    warn "$runner no es el deploy/scripts/deploy.sh de $deploy_branch: actualízalo (docs/despliegue.md, 9.1)."
  fi
}

sync_repository() {
  local sha=$1
  [[ $sync_repo == true ]] || return 0
  log "Ficheros de despliegue en el commit $sha."
  git -C "$repo_root" checkout --quiet --detach "$sha"
}

# Responde 200 a través del nginx de la web (el mismo camino que el proxy) y con la versión que se espera.
healthy() {
  local release=$1 sha headers
  sha=$(sha_of "$release")
  headers=$(compose "$release" exec -T web wget -q -S -T "$HEALTH_REQUEST_TIMEOUT_SECONDS" -O - "$HEALTH_URL" 2>&1 >/dev/null) || return 1
  printf '%s\n' "$headers" | tr -d '\r' | grep -Eqi "^[[:space:]]*x-app-version:[[:space:]]*${sha}[[:space:]]*$"
}

wait_for_health() {
  local release=$1 sha
  local deadline=$((SECONDS + health_timeout))
  sha=$(sha_of "$release")
  log "Esperando a que /health responda con la versión $sha (como mucho ${health_timeout} s)."
  until healthy "$release"; do
    if ((SECONDS >= deadline)); then
      error "/health no responde 200 con la versión $sha."
      return 1
    fi
    sleep "$HEALTH_INTERVAL_SECONDS"
  done
  log "/health responde con la versión $sha."
}

# Pone en marcha una versión. Cada paso devuelve error en lugar de terminar, para poder volver atrás.
release() {
  local release=$1 sha
  sha=$(sha_of "$release")
  sync_repository "$sha" || return 1
  if [[ $release == "$sha" ]]; then
    log "Descargando las imágenes $sha (por etiqueta)."
  else
    log "Descargando las imágenes $sha por digest."
  fi
  compose "$release" pull --quiet || return 1
  log "Arrancando los servicios (migrate aplica las migraciones antes que la API)."
  compose "$release" up -d --no-build --remove-orphans || return 1
  wait_for_health "$release"
}

roll_back() {
  local previous=$1
  if [[ -z $previous ]]; then
    error "No hay versión anterior a la que volver: revisa el servidor."
    return 0
  fi
  log "Volviendo a la versión anterior, $(sha_of "$previous")."
  if release "$previous"; then
    log "La versión anterior, $(sha_of "$previous"), vuelve a estar en marcha."
  else
    error "La vuelta atrás también ha fallado: revisa el servidor (docker compose ps y logs)."
  fi
}

# Solo un despliegue a la vez (si el sistema tiene flock).
take_lock() {
  command -v flock >/dev/null 2>&1 || return 0
  exec 9> "$state_file.lock"
  if ! flock -n 9; then
    error "Hay otro despliegue en curso."
    exit 1
  fi
}

main() {
  local requested sha previous
  requested=$(requested_release "$@") || usage
  sha=$(sha_of "$requested")
  check_settings
  load_compose_files
  take_lock
  previous=$(previous_release)
  check_on_branch "$sha"

  log "Desplegando $sha (versión actual: $(sha_of "${previous:-ninguna}"))."
  backup_database "$requested"

  if ! release "$requested"; then
    error "El despliegue de $sha ha fallado."
    roll_back "$previous"
    exit 1
  fi

  save_release "$requested"
  log "Versión $sha desplegada."
}

# En una sola línea: bash la lee entera antes de ejecutarla, así que el checkout puede cambiar este fichero sin
# afectar a la ejecución en curso.
main "$@"; exit $?
