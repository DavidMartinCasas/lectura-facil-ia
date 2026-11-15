# US-XX · Título corto

| Campo | Valor |
|---|---|
| **Épica** | EP-0X · Nombre |
| **Prioridad** | Must / Should / Could |
| **Puntos** | 1 · 2 · 3 · 5 · 8 |
| **Día previsto** | Día de la semana y fecha |
| **Depende de** | US-YY |
| **Requiere humano** | No / Sí: qué |

## Historia
**Como** [rol]
**quiero** [capacidad]
**para** [beneficio].

## Contexto para el agente
Qué necesita saber el agente que no está en `AGENTS.md`. Enlaces a secciones de `docs/TFM-Lectura-Facil.md`.

## Criterios de aceptación
```gherkin
Escenario: …
  Dado …
  Cuando …
  Entonces …
```

## Alcance técnico
- Capa / archivo → qué se crea o se cambia.

## Seguridad
- Riesgos concretos de esta historia y cómo se mitigan (con referencia OWASP si aplica).

## Calidad y clean code
- Decisiones de diseño concretas para esta historia.

## Tests requeridos
- [ ] Unitarios: …
- [ ] Integración: …
- [ ] Seguridad: …

## Fuera de alcance
- Lo que el agente **no** debe hacer en esta historia.

## Verificación
```bash
# comandos específicos además de los de AGENTS.md
```

## Definition of Done específica
- [ ] …
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-XX-….md.
Crea la rama feat/US-XX-…, preséntame un plan breve y espera mi confirmación.
Implementa con tests primero en Domain y Application, ejecuta todos los comandos de verificación
y termina con un resumen de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
