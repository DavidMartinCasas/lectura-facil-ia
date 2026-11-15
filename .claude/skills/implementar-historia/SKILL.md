---
name: implementar-historia
description: Flujo completo para implementar una historia de usuario del backlog (US-XX) de principio a fin — leer la historia, crear la rama, plan, TDD, verificación, revisiones y PR. Úsala siempre que te pidan implementar, continuar o terminar una US o una issue del backlog.
---

# Implementar una historia

Las reglas están en `AGENTS.md`; esta skill es el **orden de ejecución**. Si algo de aquí contradice `AGENTS.md`, manda `AGENTS.md`.

## 0. Antes de empezar
- Identifica la historia: `docs/backlog/historias/US-XX-*.md` (la issue #N de GitHub es la US-N y tiene el mismo texto).
- Léela **entera**, incluidos "Fuera de alcance", "Seguridad" y la "Definition of Done específica".
- Comprueba que sus dependencias (campo **Depende de**) ya están en `main`. Si falta alguna, **para y avisa**.
- ¿Estás en una sesión interactiva o en un agente en la nube sin nadie al otro lado? Cambia el paso 2.

## 1. Rama
```bash
git switch main && git pull --ff-only
git switch -c feat/US-XX-slug-corto   # docs/… o chore/… si la historia no es funcional
```

## 2. Plan breve
Contenido mínimo:
- **Archivos** que vas a crear o tocar, por capa (Domain, Application, Infrastructure, Api, Web, tests).
- **Tests** que escribirás primero (uno por criterio Gherkin, como mínimo).
- **Decisiones de diseño** que no estén en la historia, con la opción elegida (la más simple y segura).
- **Riesgos** y lo que necesitará prueba manual de una persona.
- **Dependencias nuevas:** ninguna salvo que la historia las pida. Si propones una, indica su licencia.

Dónde va:
- **Interactivo:** muéstralo y **espera confirmación**.
- **En la nube:** ponlo al principio de la descripción de la PR (abre la PR en borrador pronto) y sigue.

## 3. Implementación en pasos pequeños (TDD)
Para cada criterio de aceptación:
1. Escribe el test que falla (Domain/Application primero; nombre `Metodo_Cuando_Entonces`).
2. Escribe el mínimo código que lo pasa.
3. Refactoriza con los tests en verde.
4. Commit pequeño con Conventional Commits: `feat(scope): descripción (US-XX)`.

Recetas relacionadas: endpoint nuevo → skill `nuevo-endpoint`; prompt o llamada a LLM → skill `prompt-llm`.

Recordatorios que más se olvidan:
- Cabecera `// SPDX-License-Identifier: Apache-2.0` en cada fichero de código nuevo.
- Umbrales en `Options`, no números mágicos.
- `CancellationToken` en toda la cadena.
- Ningún log con contenido de documentos.

## 4. Verificación
Ejecuta la skill `verificar`. No sigas hasta que todo esté en verde o lo que falle esté justificado (por ejemplo, el frontend aún no existe).

## 5. Revisiones antes de la PR
Si tu herramienta tiene subagentes, lanza en paralelo sobre el diff (`git diff main...HEAD`):
- `revisor-seguridad`
- `revisor-arquitectura`
- `experto-lectura-facil` **solo** si la historia toca pautas, prompts, detección, verificación o evaluaciones (US-05, 06, 07, 08, 11, 18).

Si no tienes subagentes, lee sus ficheros en `.claude/agents/` y aplica tú mismo sus listas de comprobación.
Corrige lo bloqueante. Lo que decidas no corregir, explícalo en la PR.

## 6. Documentación
Actualiza lo que pida la historia (`docs/…`, `deploy/.env.example`, README). Marca la historia en la tabla de `docs/backlog/README.md` como 🔍 (en revisión), no como ✅: el ✅ lo pone la persona al fusionar.

## 7. Pull request
```bash
git push -u origin HEAD
gh pr create --title "feat: <título de la historia> (US-XX)" --body-file <fichero>
```
Plantilla de la descripción:
```markdown
Closes #N

## Plan
…

## Qué se ha hecho
…

## Cómo probarlo
- Automático: comandos y resultado.
- Manual (persona): pasos concretos, incluidos los que necesitan un modelo real o claves.

## Decisiones tomadas
…

## Revisiones
- Seguridad: …
- Arquitectura: …

## Pendiente y riesgos
…

## Definition of Done
- [ ] (copia aquí la DoD específica de la historia y la global)
```

## Nunca
Merge ni push a `main`, *force push*, desactivar tests o reglas para que pase el build, añadir dependencias sin confirmación, usar secretos o modelos reales.
