#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Construye y publica en GHCR las imágenes de la API y de la web con la etiqueta del commit (US-19) y escribe sus
# digests en las salidas del job (api_digest y web_digest): el servidor descarga por digest, no por etiqueta, así que
# aunque alguien reescribiera la etiqueta en el registro desplegaría exactamente lo que se construyó aquí.
#
# Variables de GitHub Actions: GITHUB_SHA, GITHUB_REPOSITORY_OWNER, GITHUB_REPOSITORY, GITHUB_SERVER_URL, GITHUB_ACTOR,
# GITHUB_OUTPUT y GITHUB_TOKEN (con packages: write).
set -euo pipefail

readonly DIGEST_PATTERN='^sha256:[0-9a-f]{64}$'
readonly registry="ghcr.io/${GITHUB_REPOSITORY_OWNER,,}"

# Nombre, contexto y Dockerfile: los mismos que deploy/docker-compose.yml (la imagen que prueba la CI).
readonly images=(
  "lectura-facil-api . src/LecturaFacil.Api/Dockerfile"
  "lectura-facil-web src/LecturaFacil.Web src/LecturaFacil.Web/Dockerfile"
)

build() {
  local name=$1 context=$2 dockerfile=$3
  docker build --pull --file "$dockerfile" --tag "$registry/$name:$GITHUB_SHA" \
    --label "org.opencontainers.image.source=$GITHUB_SERVER_URL/$GITHUB_REPOSITORY" \
    --label "org.opencontainers.image.revision=$GITHUB_SHA" \
    --label "org.opencontainers.image.licenses=Apache-2.0" \
    "$context"
}

# Digest con el que el registro guardó la imagen recién publicada.
pushed_digest() {
  local repository=$1 digest
  digest=$(docker inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$repository:$GITHUB_SHA" \
    | sed -n "s|^$repository@||p" | head -n 1)
  [[ $digest =~ $DIGEST_PATTERN ]] || { echo "::error::No se ha podido leer el digest de $repository." >&2; return 1; }
  printf '%s' "$digest"
}

for image in "${images[@]}"; do
  read -r name context dockerfile <<< "$image"
  build "$name" "$context" "$dockerfile"
done

# La sesión se abre después de construir: mientras corren npm ci y dotnet restore, el runner no tiene credenciales.
trap 'docker logout ghcr.io >/dev/null 2>&1 || true' EXIT
printf '%s' "$GITHUB_TOKEN" | docker login ghcr.io --username "$GITHUB_ACTOR" --password-stdin

for image in "${images[@]}"; do
  read -r name _ <<< "$image"
  docker push "$registry/$name:$GITHUB_SHA"
  digest=$(pushed_digest "$registry/$name")
  output=${name#lectura-facil-}_digest
  echo "$registry/$name:$GITHUB_SHA@$digest"
  echo "$output=$digest" >> "$GITHUB_OUTPUT"
done
