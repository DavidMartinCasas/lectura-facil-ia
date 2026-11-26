#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# CI, mínimo privilegio (US-10): las migraciones terminaron bien y la cuenta de la API solo puede tocar filas.
# Se ejecuta desde la raíz del repositorio, con el compose base en marcha (smoke-test.sh).
set -euo pipefail

compose() { docker compose -f deploy/docker-compose.yml "$@"; }

# Ejecuta una consulta con la cuenta de la API, dentro del contenedor de PostgreSQL. Las variables se expanden allí
# dentro (comillas simples a propósito): la contraseña no sale del contenedor.
# shellcheck disable=SC2016
as_app() {
  compose exec -T postgres sh -c \
    'PGPASSWORD="$POSTGRES_APP_PASSWORD" psql -h 127.0.0.1 -U "$POSTGRES_APP_USER" -d "$POSTGRES_DB" -v ON_ERROR_STOP=1 -tAc "$1"' \
    sh "$1"
}

fail() {
  echo "::error::$*"
  exit 1
}

[[ $(compose ps -a --format '{{.ExitCode}}' migrate) == 0 ]] || fail "El servicio migrate no terminó bien."
[[ $(as_app "select rolsuper or rolcreatedb or rolcreaterole from pg_roles where rolname = current_user") == f ]] \
  || fail "La cuenta de la API tiene privilegios de administración."
[[ $(as_app "select count(*) > 0 from \"__EFMigrationsHistory\"") == t ]] || fail "No hay migraciones aplicadas."

if as_app "create table intruso (id int)"; then
  fail "La cuenta de la API puede crear tablas."
fi
if as_app "drop table \"__EFMigrationsHistory\""; then
  fail "La cuenta de la API puede borrar tablas."
fi
