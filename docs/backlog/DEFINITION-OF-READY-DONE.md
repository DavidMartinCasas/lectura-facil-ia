# Definition of Ready y Definition of Done

## Definition of Ready (antes de lanzar el agente)

Una historia está lista cuando:

- [ ] Tiene historia en formato **Como / Quiero / Para**.
- [ ] Tiene **criterios de aceptación** en Gherkin que se pueden comprobar.
- [ ] Sus **dependencias** están terminadas (o se indica cómo simularlas).
- [ ] Indica **qué capas y archivos** toca y qué queda **fuera de alcance**.
- [ ] Tiene su sección de **seguridad** y de **tests**.
- [ ] Si necesita algo **humano** (claves, URLs, infraestructura), ya está disponible o se indica cómo trabajar sin ello.
- [ ] Cabe en **un día** de trabajo. Si no, se divide.

## Definition of Done (global, para todas las historias)

### Funcional
- [ ] Se cumplen **todos** los criterios de aceptación.
- [ ] Probado manualmente al menos el camino feliz y un caso de error.

### Código
- [ ] Respeta la **regla de dependencias** (el test de arquitectura pasa).
- [ ] Sin lógica de negocio en endpoints ni componentes.
- [ ] Sin números mágicos, sin código comentado, sin `TODO` sin referencia.
- [ ] `dotnet build -warnaserror`, `dotnet format --verify-no-changes` y el lint de Angular **sin errores**.

### Tests
- [ ] Tests nuevos para el comportamiento nuevo; **TDD en Domain y Application**.
- [ ] Cobertura de Domain y Application **≥ 80 %**.
- [ ] Los tests **no llaman a LLM reales** ni a servicios externos.
- [ ] Todos los tests pasan; ninguno desactivado.

### Seguridad
- [ ] Sin secretos en código, tests, logs ni commits (`gitleaks` limpio).
- [ ] Entradas validadas en el borde; salidas sin HTML dinámico.
- [ ] Errores sin información interna.
- [ ] Sin vulnerabilidades altas o críticas en dependencias.
- [ ] Revisados los puntos de la sección **Seguridad** de la historia.

### Documentación y entrega
- [ ] Actualizado lo que pida la historia (README, `.env.example`, ADR…).
- [ ] Commits con Conventional Commits y referencia `(US-XX)`.
- [ ] PR abierta con el resumen del agente y revisada por una persona.
- [ ] Marcada como ✅ en `docs/backlog/README.md`.
