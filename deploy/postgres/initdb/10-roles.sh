#!/bin/sh
# SPDX-License-Identifier: Apache-2.0
# Roles con mínimo privilegio. La imagen de PostgreSQL ejecuta este script una sola vez, al crear el volumen vacío,
# conectada como POSTGRES_USER (superusuario, solo para esta inicialización y los backups):
#   - POSTGRES_OWNER_USER: propietario de la base y del esquema. Solo lo usa el servicio "migrate" para aplicar las
#     migraciones; no es superusuario.
#   - POSTGRES_APP_USER: el que usa la API. Solo lee y escribe filas (SELECT, INSERT, UPDATE, DELETE): no puede crear,
#     cambiar ni borrar tablas.
# Las contraseñas llegan como variables de psql (:'...'), que las escapa: pueden llevar comillas o punto y coma.
set -eu

: "${POSTGRES_OWNER_USER:?Define POSTGRES_OWNER_USER}"
: "${POSTGRES_OWNER_PASSWORD:?Define POSTGRES_OWNER_PASSWORD}"
: "${POSTGRES_APP_USER:?Define POSTGRES_APP_USER}"
: "${POSTGRES_APP_PASSWORD:?Define POSTGRES_APP_PASSWORD}"

psql --no-psqlrc -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v db="$POSTGRES_DB" \
  -v owner="$POSTGRES_OWNER_USER" -v owner_password="$POSTGRES_OWNER_PASSWORD" \
  -v app="$POSTGRES_APP_USER" -v app_password="$POSTGRES_APP_PASSWORD" <<'SQL'
CREATE ROLE :"owner" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'owner_password';
CREATE ROLE :"app" LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'app_password';

-- El propietario de la base es dueño del esquema public (pg_database_owner) y puede crear las tablas.
ALTER DATABASE :"db" OWNER TO :"owner";
REVOKE ALL ON DATABASE :"db" FROM PUBLIC;
GRANT CONNECT ON DATABASE :"db" TO :"app";
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO :"app";

-- Las migraciones la crean con IF NOT EXISTS; así no hace falta que el propietario pueda crear extensiones.
CREATE EXTENSION IF NOT EXISTS unaccent SCHEMA public;

-- Las tablas y secuencias que cree el propietario (las migraciones) quedan accesibles para la API, solo para datos.
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" IN SCHEMA public
  GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app";
ALTER DEFAULT PRIVILEGES FOR ROLE :"owner" IN SCHEMA public
  GRANT USAGE, SELECT ON SEQUENCES TO :"app";
SQL
