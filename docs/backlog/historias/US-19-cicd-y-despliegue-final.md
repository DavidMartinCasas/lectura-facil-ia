# US-19 · CI/CD y despliegue final

| Campo | Valor |
|---|---|
| **Épica** | EP-08 · Calidad y operación |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Lunes 23 |
| **Depende de** | US-10 |
| **Requiere humano** | Sí: secretos en GitHub (clave OAuth o authkey efímera de Tailscale, usuario SSH del VPS) |

## Historia
**Como** autor del TFM
**quiero** que cada fusión en `main` pase todos los controles de calidad y seguridad y se despliegue sola en el VPS
**para** no desplegar a mano en los últimos días y demostrar un flujo de DevOps completo.

## Contexto para el agente
- Pipeline: sección 17, "Pipeline de CI/CD", de `docs/TFM-Lectura-Facil.md`.
- El runner de GitHub entra en el tailnet con una **clave efímera y etiquetada** (acción oficial de Tailscale) y despliega por SSH al VPS.
- Curso, módulo 10: DevOps y CI/CD.

## Criterios de aceptación
```gherkin
Escenario: PR
  Dado una pull request
  Cuando se ejecuta CI
  Entonces pasan build, tests con cobertura, lint, dotnet format, gitleaks, análisis de dependencias,
  hadolint, validación de docker compose y la evaluación en --dry-run
  Y si algo falla, la PR no se puede fusionar

Escenario: Despliegue
  Dado un merge en main con CI en verde
  Cuando se ejecuta el workflow de despliegue
  Entonces se construyen y publican las imágenes con la etiqueta del commit
  Y el VPS descarga esas imágenes y reinicia los servicios
  Y el workflow comprueba que /health responde 200 con la nueva versión

Escenario: Despliegue fallido
  Cuando /health no responde tras el despliegue
  Entonces el workflow vuelve a la versión anterior y falla
```

## Alcance técnico
- `.github/workflows/ci.yml` (completo) y `.github/workflows/deploy.yml`.
- Imágenes en **GitHub Container Registry** con etiqueta del commit (no solo `latest`).
- **Permisos mínimos** del `GITHUB_TOKEN` por job (`contents: read`, `packages: write` solo donde haga falta).
- Acciones de terceros **fijadas por SHA**, no por etiqueta.
- `deploy/scripts/deploy.sh` en el VPS: `docker compose pull` + `up -d` + comprobación de salud + vuelta atrás.
- Dependabot para NuGet, npm, Docker y GitHub Actions.
- Protección de rama `main` documentada (la configura la persona): PR obligatoria y CI en verde.

## Seguridad
- **Cadena de suministro:** acciones fijadas por SHA, permisos mínimos, Dependabot, imágenes con versión.
- **Acceso al VPS:** solo por Tailscale con clave efímera etiquetada (`tag:ci`), con ACL que solo permita `tag:ci → tag:lf-web:22`.
- Secretos solo en GitHub Secrets; nunca impresos en logs (`::add-mask::` si hiciera falta).
- El workflow de despliegue **solo** se ejecuta en `main` y no en PRs de forks.

## Calidad y clean code
- Jobs separados y reutilizables; nada de scripts largos en línea dentro del YAML.
- Cobertura publicada como artefacto y resumen en la PR.

## Tests requeridos
- [ ] `actionlint` sobre los workflows.
- [ ] Prueba del script de despliegue en local contra un compose de pruebas (sin VPS).

## Fuera de alcance
- Entornos de *staging* separados.

## Verificación
```bash
actionlint
bash -n deploy/scripts/deploy.sh
```

## Definition of Done específica
- [ ] Un despliegue real completado desde `main` (lo comprueba la persona) y la URL muestra la nueva versión.
- [ ] `docs/despliegue.md` actualizado con el flujo automático y los secretos necesarios.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-19-cicd-y-despliegue-final.md.
Crea la rama feat/US-19-cicd, preséntame un plan breve y espera mi confirmación.
Fija todas las acciones por SHA y usa permisos mínimos. No te conectes al VPS: prepara workflows y scripts
y dime qué secretos y ACLs tengo que configurar yo. Ejecuta las verificaciones locales y termina con un
resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
