# US-02 · Configuración y secretos validados al arrancar

| Campo | Valor |
|---|---|
| **Épica** | EP-01 · Base |
| **Prioridad** | Must |
| **Puntos** | 2 |
| **Día previsto** | Martes 17 |
| **Depende de** | US-01 |
| **Requiere humano** | No |

## Historia
**Como** responsable de la operación del sistema
**quiero** que toda la configuración y los secretos se carguen desde el entorno y se validen al arrancar
**para** que la aplicación nunca funcione con una configuración incompleta y ningún secreto acabe en el código.

## Contexto para el agente
- Curso, módulo 08: variables de entorno y secretos (validación en tiempo de ejecución, separar público de privado).
- `AGENTS.md`, sección 6 "Secretos y configuración".

## Criterios de aceptación
```gherkin
Escenario: Falta un secreto obligatorio
  Dado que la variable LLM__LOCAL__APIKEY no está definida
  Cuando arranca la API
  Entonces la aplicación no arranca
  Y el log indica qué opción falta, sin mostrar ningún valor

Escenario: Configuración completa
  Dado un entorno con todas las variables de .env.example rellenadas
  Cuando arranca la API
  Entonces arranca correctamente

Escenario: Un secreto en el código se detecta
  Dado un commit que contiene algo con forma de API key
  Cuando se ejecuta gitleaks en CI
  Entonces el workflow falla
```

## Alcance técnico
- Clases de `Options` tipadas y validadas (`ValidateDataAnnotations` + `ValidateOnStart`), una por área:
  - `DatabaseOptions` (cadena de conexión).
  - `LlmOptions` con `Primary` (obligatorio) y `Fallback` (opcional, con `Enabled`). Cada uno lleva el **tipo** de proveedor (`OpenAICompatible` para Ollama, LM Studio, vLLM, llama-swap…, o `AzureFoundry`), un **nombre visible** (por ejemplo, "Servidor propio"), endpoint, API key y el modelo o despliegue para los roles adaptador y verificador. **Por defecto `Fallback.Enabled = false`**; si está deshabilitado, su configuración no se exige.
  - `UiOptions`: texto de **aviso opcional** que se muestra en la interfaz (por ejemplo, *"Instancia de demostración: no subas datos personales"*). Vacío por defecto.
  - `LimitsOptions` (tamaño máximo de fichero, caracteres máximos, palabras por frase, `MaxOutputTokens`, timeouts).
- Validaciones: URLs absolutas `https` (se admite `http` solo hacia direcciones privadas o `localhost`, para proveedores locales), números en rangos razonables.
- **Valores por defecto seguros** para quien instala sin tocar nada: respaldo deshabilitado, Turnstile deshabilitado, límites conservadores.
- `deploy/.env.example` con **todas** las variables, comentadas, y valores de ejemplo evidentes.
- Ninguna opción con valor por defecto que sea un secreto.
- Configuración de `.gitleaks.toml` si hace falta afinar reglas (sin silenciar detecciones reales).

## Seguridad
- Los **valores de los secretos nunca se registran**: ni en logs ni en mensajes de error de validación.
- Las opciones que contienen secretos se marcan y se excluyen de cualquier endpoint de diagnóstico.
- En `Development`, se permite `dotnet user-secrets`; nunca `appsettings.Development.json` con secretos subido al repositorio.

## Calidad y clean code
- Sin lecturas de `Environment.GetEnvironmentVariable` dispersas: todo pasa por `IOptions<T>`.
- Los umbrales de negocio (por ejemplo, 15 palabras por frase) son opciones, no constantes en el código.

## Tests requeridos
- [ ] Unitarios de validación de cada `Options` (válida, falta campo, rango incorrecto).
- [ ] Integración: la API no arranca si falta una opción obligatoria.
- [ ] Test que comprueba que el mensaje de error de validación **no contiene** el valor del secreto.

## Fuera de alcance
- Llamadas reales a LLM (US-03).
- Gestores de secretos externos (Key Vault): fuera del MVP.

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Options"
gitleaks detect --no-banner
```

## Definition of Done específica
- [ ] `.env.example` completo y documentado.
- [ ] README: sección de configuración con la tabla de variables.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-02-configuracion-y-secretos.md.
Crea la rama feat/US-02-config-secretos, preséntame un plan breve y espera mi confirmación.
Implementa con tests primero, ejecuta todos los comandos de verificación y termina con un resumen
de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
