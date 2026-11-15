# US-10 · Despliegue del esqueleto en el VPS

| Campo | Valor |
|---|---|
| **Épica** | EP-08 · Calidad y operación |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Sábado 21 |
| **Depende de** | US-01 |
| **Requiere humano** | **Sí:** VPS contratado, acceso SSH por Tailscale, dominio en Cloudflare y token del túnel |

## Historia
**Como** autor del TFM
**quiero** tener la aplicación desplegada en su URL pública desde mitad de semana
**para** descubrir pronto los problemas de despliegue y que el tribunal pueda probarla.

## Contexto para el agente
- Arquitectura de despliegue: sección 17 de `docs/TFM-Lectura-Facil.md`.
- Este es el **despliegue de referencia de la instancia del TFM**. El proyecto es open source: `docs/despliegue.md` lo presenta como **un ejemplo** (VPS + Cloudflare Tunnel + Tailscale), junto a una opción mínima (`docker compose up` en cualquier servidor). Nada de esta infraestructura puede quedar fijado en el código.
- **Reparto del trabajo:** el agente **prepara** ficheros, scripts y documentación; **la persona ejecuta** todo lo que toca servidores, cuentas o DNS. El agente **no se conecta** al VPS ni a Cloudflare.

## Criterios de aceptación
```gherkin
Escenario: Compose de producción
  Dado el fichero deploy/docker-compose.prod.yml
  Cuando se valida con "docker compose config"
  Entonces no expone ningún puerto al host salvo los que use cloudflared internamente
  Y todos los servicios corren con usuario no root
  Y PostgreSQL guarda los datos en un volumen con nombre

Escenario: Acceso por Cloudflare Tunnel
  Dado el servicio cloudflared con el token en .env
  Cuando la persona levanta el compose en el VPS
  Entonces https://<subdominio>/health responde 200
  Y ningún puerto del VPS está abierto a internet

Escenario: Guía de despliegue
  Dado docs/despliegue.md
  Cuando una persona la sigue desde un VPS recién instalado
  Entonces llega a tener la aplicación funcionando sin pasos implícitos
```

## Alcance técnico (lo hace el agente)
- `deploy/docker-compose.prod.yml` con `web`, `api`, `postgres` y `cloudflared` en una red interna.
- `deploy/.env.prod.example` (sin valores reales).
- `docs/despliegue.md` con los pasos para la persona:
  1. Endurecer el VPS: actualizaciones automáticas, usuario sin root, SSH solo con clave y **solo por Tailscale**, firewall que bloquee toda entrada.
  2. Añadir el VPS al tailnet con clave de un solo uso y etiqueta `tag:lf-web`; comprobar con las ACLs que **solo** llega al puerto de llama-swap.
  3. Instalar Docker, copiar `.env.prod`, levantar el compose.
  4. Crear el túnel en Cloudflare y el subdominio.
  5. Comprobaciones finales (lista de `curl` y pruebas negativas).
- `deploy/scripts/backup-postgres.sh` (volcado diario con `pg_dump`, rotación de 7 días).

## Seguridad
- **Ningún puerto publicado** en el compose de producción (`ports:` vacío salvo justificación).
- Secretos solo en `.env.prod` en el VPS, con permisos `600`.
- Imágenes con versión fijada (no `latest`).
- Contenedores con `read_only: true` donde sea posible, `no-new-privileges` y `cap_drop: [ALL]`.
- La guía incluye las **pruebas negativas**: desde el VPS, cualquier IP del homelab distinta de llama-swap debe fallar.

## Calidad y clean code
- Un solo compose base y un *override* de producción, sin duplicar definiciones.

## Tests requeridos
- [ ] `docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml config --quiet` en CI.
- [ ] Linter de Dockerfiles (hadolint) en CI.

## Fuera de alcance
- Despliegue automático desde GitHub Actions (US-19).
- Configuración de Tailscale o Cloudflare desde el agente.

## Verificación
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.prod.yml config --quiet
hadolint src/LecturaFacil.Api/Dockerfile src/LecturaFacil.Web/Dockerfile
# En el VPS (humano):
curl -s https://<subdominio>/health
```

## Definition of Done específica
- [ ] La persona ha desplegado siguiendo `docs/despliegue.md` y la URL responde.
- [ ] Pruebas negativas de red hechas y anotadas en la PR.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-10-despliegue-esqueleto-vps.md.
Crea la rama feat/US-10-despliegue-vps, preséntame un plan breve y espera mi confirmación.
No te conectes a ningún servidor ni servicio externo: prepara ficheros, scripts y la guía docs/despliegue.md
para que yo ejecute los pasos. Ejecuta los comandos de verificación locales y termina con un resumen
y la lista de pasos que tengo que hacer yo. No hagas push ni merge.
```
