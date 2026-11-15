# US-20 · README, licencia y documentación open source

| Campo | Valor |
|---|---|
| **Épica** | EP-09 · Documentación |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Jueves 26 |
| **Depende de** | Todas |
| **Requiere humano** | Sí: URL del despliegue, de las slides y del vídeo, y revisión final del texto |

## Historia
**Como** tribunal del TFM y como cualquier persona que encuentre el proyecto en GitHub
**quiero** un README completo que explique qué es, cómo instalarlo en mi infraestructura, cómo configurarlo y bajo qué condiciones puedo usarlo
**para** evaluarlo o desplegarlo sin tener que adivinar nada y sabiendo qué responsabilidades asumo.

## Contexto para el agente
- **Requisitos obligatorios del TFM** para el README:
  1. Descripción general del proyecto.
  2. Stack tecnológico.
  3. Instalación y ejecución.
  4. Estructura del proyecto.
  5. Funcionalidades principales.
  6. Usuario y contraseña de prueba (el correo en el README; la contraseña en el formulario de entrega).
  7. URL del despliegue, de las slides y del vídeo.
- **Es un proyecto open source y autoalojable.** Quien lo despliega elige su infraestructura y sus proveedores de IA y es **responsable de los datos que procesa**.
- **Licencia decidida: Apache 2.0.** Aporta concesión explícita de patentes, cláusula de represalia por demandas de patentes, protección de la marca y regula las contribuciones de terceros. Exige conservar el fichero `NOTICE` e indicar los ficheros modificados en las redistribuciones.
- Fuente: `docs/TFM-Lectura-Facil.md` (resumir, no copiar entero).

## Criterios de aceptación
```gherkin
Escenario: Checklist del TFM
  Dado el README final
  Entonces contiene los 7 apartados obligatorios
  Y todos los enlaces funcionan

Escenario: Instalación por un tercero
  Dado una persona con Docker y el repositorio clonado
  Cuando sigue "Instalación y ejecución" con un proveedor compatible con OpenAI (por ejemplo, Ollama en local)
  Entonces tiene la aplicación funcionando sin necesitar Azure, Tailscale ni Cloudflare

Escenario: Responsabilidad y privacidad
  Dado el README
  Entonces explica las tres configuraciones de proveedores (solo local, local + respaldo, solo nube) y su efecto en la privacidad
  Y deja claro que quien despliega es responsable del tratamiento de los datos (RGPD)
  Y que la herramienta genera borradores que deben validar personas

Escenario: Licencia del proyecto
  Entonces el repositorio tiene un fichero LICENSE con el texto íntegro y sin modificar de Apache License 2.0
  Y un fichero NOTICE con el nombre del proyecto, el año y el autor
  Y el README muestra la licencia (texto y badge) y enlaza a LICENSE
  Y los ficheros de código fuente llevan la cabecera SPDX "Apache-2.0"

Escenario: Licencias de terceros
  Entonces docs/licencias.md lista las licencias de las dependencias, la fuente incluida y los textos de ejemplo
  Y confirma que todas son compatibles con Apache 2.0
  Y avisa de que cada modelo de IA que se configure tiene su propia licencia y condiciones de uso

Escenario: Decisiones documentadas
  Entonces existen los ADR de arquitectura, verificador y proveedores configurables
```

## Alcance técnico
- `README.md` con: descripción, captura o GIF, diagrama de arquitectura (Mermaid), stack, **instalación rápida** (`docker compose up` + un proveedor compatible con OpenAI), configuración (tabla de variables y valores por defecto), las tres configuraciones de proveedores, estructura, funcionalidades, seguridad (resumen), evaluación (tabla de resultados), **instancia de demostración** (URL, usuario de prueba), enlaces de entrega, licencia y créditos.
- `LICENSE`: texto **íntegro y sin modificar** de Apache License 2.0, copiado de la fuente oficial (`https://www.apache.org/licenses/LICENSE-2.0.txt`). **No lo redactes ni lo resumas.**
- `NOTICE`:
  ```text
  Lectura Fácil IA
  Copyright 2026 David Martín Casas

  Este producto incluye software desarrollado por terceros.
  Consulta docs/licencias.md para ver sus licencias.
  ```
- **Cabecera SPDX** en los ficheros de código fuente propios (`.cs`, `.ts`, `.html`, `.scss`):
  ```csharp
  // SPDX-License-Identifier: Apache-2.0
  ```
  Se añade a los existentes en esta historia y se configura en `.editorconfig` o con una regla de lint para que los nuevos la lleven.
- **Metadatos de paquetes:** `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` en `Directory.Build.props` y `"license": "Apache-2.0"` en `package.json`.
- `SECURITY.md`: cómo reportar vulnerabilidades en privado (avisos de seguridad privados de GitHub) y qué versiones se mantienen.
- `CONTRIBUTING.md` breve: cómo arrancar en local, convenciones (remitir a `AGENTS.md`), flujo de PR y que **las contribuciones se aceptan bajo Apache 2.0** (sección 5 de la licencia).
- `docs/despliegue.md`: opción mínima (cualquier servidor con Docker) y el **despliegue de referencia** del TFM (VPS + Cloudflare Tunnel + Tailscale + Azure de respaldo) como ejemplo.
- `docs/adr/0001-clean-architecture.md`, `0002-verificador-de-otra-familia.md` y `0003-proveedores-configurables.md` (crear los que falten), en formato Nygard o MADR.
- `docs/seguridad.md`: medidas aplicadas por categoría de OWASP (Top 10, API Top 10, LLM Top 10).
- `docs/licencias.md`: dependencias (PdfPig, Open XML SDK, PDFsharp/MigraDoc…), fuente, textos de ejemplo y aviso sobre las licencias de los modelos. Generar la lista de dependencias NuGet y npm con una herramienta (por ejemplo, `dotnet-project-licenses` y `license-checker`), no a mano.

## Seguridad
- El README **no** contiene contraseñas, claves, IPs de Tailscale ni nombres internos del homelab.
- Se revisa todo el repositorio con `gitleaks` antes de dar la historia por terminada.

## Calidad y clean code
- Documentación concisa: lo esencial en el README y el detalle en `docs/`.
- Diagramas en Mermaid (se renderizan en GitHub).

## Tests requeridos
- [ ] Comprobador de enlaces rotos en Markdown (por ejemplo, `lychee`) en CI.
- [ ] Comprobación en CI de que todos los ficheros de código propios tienen la cabecera SPDX.
- [ ] Comprobación en CI de que ninguna dependencia tiene una licencia incompatible con Apache 2.0 (GPL, AGPL, comerciales).
- [ ] `gitleaks` limpio.
- [ ] Prueba manual: instalación desde cero siguiendo solo el README, con Ollama o con `FakeLlmProvider`.

## Fuera de alcance
- Slides y vídeo (los hace la persona).
- Sitio web de documentación.

## Verificación
```bash
lychee --offline README.md docs/
gitleaks detect --no-banner
```

## Definition of Done específica
- [ ] `LICENSE` (Apache 2.0 íntegra) y `NOTICE` en la raíz; GitHub detecta la licencia como "Apache-2.0".
- [ ] La persona ha revisado y aprobado el texto.
- [ ] Enlaces de despliegue, slides y vídeo rellenados.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-20-readme-y-adrs.md.
Crea la rama docs/US-20-readme, preséntame el índice del README y la lista de ficheros de documentación,
y espera mi confirmación. Resume a partir de docs/TFM-Lectura-Facil.md y del código real; no inventes
funcionalidades que no existan. La licencia es Apache 2.0: copia el texto oficial sin modificarlo.
Deja marcadores claros donde yo tenga que poner las URLs de slides y vídeo.
No hagas push ni merge.
```
