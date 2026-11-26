#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Copia de seguridad de PostgreSQL: un pg_dump comprimido (formato custom de pg_restore) por ejecución, y borra los de
# más de BACKUP_RETENTION_DAYS días. Pensado para ejecutarse una vez al día en el servidor (docs/despliegue.md).
#
#   deploy/scripts/backup-postgres.sh
#
# Variables opcionales:
#   BACKUP_DIR              carpeta de las copias (por defecto /var/backups/lectura-facil). Solo la lee su dueño.
#   BACKUP_RETENTION_DAYS   días que se guardan (por defecto 7).
#   BACKUP_COMPOSE_FILE     compose del proyecto (por defecto deploy/docker-compose.yml, junto a este script).
#
# Restaurar: docs/despliegue.md, sección "Restaurar una copia".
set -eu

script_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
compose_file=${BACKUP_COMPOSE_FILE:-$script_dir/../docker-compose.yml}
backup_dir=${BACKUP_DIR:-/var/backups/lectura-facil}
retention_days=${BACKUP_RETENTION_DAYS:-7}

case $retention_days in
  '' | *[!0-9]*) echo "BACKUP_RETENTION_DAYS debe ser un número de días." >&2; exit 1 ;;
esac

umask 077
mkdir -p "$backup_dir"

stamp=$(date -u +%Y%m%dT%H%M%SZ)
target="$backup_dir/lectura-facil-$stamp.dump"
partial="$target.partial"
trap 'rm -f "$partial"' EXIT

# El volcado se hace dentro del contenedor con el superusuario, por el socket local: la contraseña no sale de él.
docker compose -f "$compose_file" exec -T postgres \
  sh -c 'pg_dump --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" --format=custom --compress=9' > "$partial"

if [ ! -s "$partial" ]; then
  echo "El volcado está vacío; no se guarda." >&2
  exit 1
fi

mv "$partial" "$target"
echo "Copia guardada: $target ($(du -h "$target" | cut -f1))"

find "$backup_dir" -maxdepth 1 -type f -name 'lectura-facil-*.dump' -mtime +"$retention_days" -print -delete
