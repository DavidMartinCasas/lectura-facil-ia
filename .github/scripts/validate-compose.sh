#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# CI: crea deploy/.env a partir del ejemplo y valida el compose base y el de producción (US-10, US-19).
# Producción no puede publicar ningún puerto en el host. Se ejecuta desde la raíz del repositorio.
set -euo pipefail

readonly base=(-f deploy/docker-compose.yml)
readonly prod=(-f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml)

cp deploy/.env.example deploy/.env
# Contraseñas con separadores y comillas: los roles se crean y la API arranca igual (credenciales escapadas).
sed -i 's/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=ci;con=separadores/' deploy/.env
sed -i "s/^POSTGRES_OWNER_PASSWORD=.*/POSTGRES_OWNER_PASSWORD=ci-owner;o'reilly/" deploy/.env
sed -i 's/^POSTGRES_APP_PASSWORD=.*/POSTGRES_APP_PASSWORD=ci-app;con=separadores/' deploy/.env

docker compose "${base[@]}" config --quiet
docker compose "${prod[@]}" config --quiet

published=$(docker compose "${prod[@]}" config --format json \
  | jq '[.services[] | select((.ports // []) | length > 0)] | length')
if [[ $published != 0 ]]; then
  echo "::error::El compose de producción publica puertos en el host ($published servicios)."
  exit 1
fi
