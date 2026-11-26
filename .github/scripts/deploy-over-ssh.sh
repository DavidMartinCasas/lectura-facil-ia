#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Pide al servidor que despliegue <sha> (US-19). En el servidor, la clave solo puede ejecutar deploy/scripts/deploy.sh
# (command= en authorized_keys), que recibe el SHA y nada más. Guía: docs/despliegue.md, "Despliegue automático".
#
#   .github/scripts/deploy-over-ssh.sh <sha>
#
# Variables obligatorias (GitHub Secrets y variables del environment "production"):
#   DEPLOY_HOST              nombre o IP del servidor en la red privada.
#   DEPLOY_USER              usuario de despliegue del servidor.
#   DEPLOY_SSH_KEY           clave privada SSH de ese usuario.
#   DEPLOY_SSH_KNOWN_HOSTS   línea de known_hosts del servidor: sin ella no se conecta (nada de confiar a ciegas).
set -euo pipefail

sha=${1:?Indica el SHA que se despliega.}
: "${DEPLOY_HOST:?Falta la variable DEPLOY_HOST.}"
: "${DEPLOY_USER:?Falta la variable DEPLOY_USER.}"
: "${DEPLOY_SSH_KEY:?Falta el secreto DEPLOY_SSH_KEY.}"
: "${DEPLOY_SSH_KNOWN_HOSTS:?Falta el secreto DEPLOY_SSH_KNOWN_HOSTS.}"

umask 077
ssh_dir=$(mktemp -d "${RUNNER_TEMP:-/tmp}/deploy-ssh.XXXXXX")
trap 'rm -rf "$ssh_dir"' EXIT
printf '%s\n' "$DEPLOY_SSH_KEY" > "$ssh_dir/key"
printf '%s\n' "$DEPLOY_SSH_KNOWN_HOSTS" > "$ssh_dir/known_hosts"

ssh -i "$ssh_dir/key" \
  -o IdentitiesOnly=yes \
  -o BatchMode=yes \
  -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile="$ssh_dir/known_hosts" \
  -o ConnectTimeout=30 \
  -o ServerAliveInterval=30 \
  "$DEPLOY_USER@$DEPLOY_HOST" "$sha"
