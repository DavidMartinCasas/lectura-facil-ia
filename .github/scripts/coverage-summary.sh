#!/usr/bin/env bash
# SPDX-License-Identifier: Apache-2.0
# Resume en una tabla Markdown los informes Cobertura de los tests (Domain y Application, US-19).
# Escribe en el resumen del job de GitHub Actions o, fuera de él, en la salida estándar.
#
#   .github/scripts/coverage-summary.sh [carpeta-de-informes]   (por defecto TestResults)
set -euo pipefail

reports_dir=${1:-TestResults}
summary=${GITHUB_STEP_SUMMARY:-/dev/stdout}

shopt -s nullglob
reports=("$reports_dir"/coverage.cobertura*.xml)
if ((${#reports[@]} == 0)); then
  echo "No hay informes de cobertura en $reports_dir." >&2
  exit 1
fi

# Valor de un atributo de la etiqueta <coverage> (la primera línea que la contiene).
attribute() {
  local name=$1 report=$2
  grep -m 1 -o "<coverage [^>]*" "$report" | grep -o " $name=\"[^\"]*\"" | cut -d '"' -f 2
}

percentage() {
  awk -v covered="$1" -v valid="$2" 'BEGIN { if (valid == 0) print "—"; else printf "%.1f %%", 100 * covered / valid }'
}

{
  echo "### Cobertura de los tests"
  echo
  echo "| Ensamblado | Líneas | Ramas |"
  echo "|---|---|---|"
  for report in "${reports[@]}"; do
    assembly=$(grep -m 1 -o '<package name="[^"]*"' "$report" | cut -d '"' -f 2)
    lines=$(percentage "$(attribute lines-covered "$report")" "$(attribute lines-valid "$report")")
    branches=$(percentage "$(attribute branches-covered "$report")" "$(attribute branches-valid "$report")")
    echo "| ${assembly:-desconocido} | $lines | $branches |"
  done
  echo
  echo "Umbral: 80 % de líneas y de ramas en Domain y Application (AGENTS.md, sección 7)."
} >> "$summary"
