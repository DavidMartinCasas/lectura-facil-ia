# US-03 · Proveedores LLM (servidor propio y Azure)

| Campo | Valor |
|---|---|
| **Épica** | EP-01 · Base |
| **Prioridad** | Must |
| **Puntos** | 5 |
| **Día previsto** | Miércoles 18 |
| **Depende de** | US-02 |
| **Requiere humano** | Sí: endpoint y API key de llama-swap, endpoint y clave de Azure AI Foundry con los despliegues creados |

## Historia
**Como** sistema de adaptación
**quiero** hablar con distintos proveedores de modelos a través de un único puerto
**para** poder usar el servidor propio como principal, Azure como respaldo y modelos falsos en los tests, sin cambiar la lógica de negocio.

## Contexto para el agente
- Diseño: secciones 7 (puertos) y 9 (modelos) de `docs/TFM-Lectura-Facil.md`.
- El proyecto es open source: el proveedor tiene que servir para **cualquier servidor compatible con OpenAI** (Ollama, LM Studio, vLLM, llama-swap…), no solo para el del autor. En la instancia del TFM, el principal es llama-swap en el servidor propio, accesible por Tailscale con API key.
- Azure AI Foundry: Spain Central, despliegues **Data Zone Standard**: `gpt-4.1-mini` (adaptador) y `mistral-medium-3-5` (verificador). Endpoint compatible con OpenAI; **el "modelo" es el nombre del despliegue**. Comprueba en la documentación oficial la URL exacta y si los modelos de Mistral usan el mismo endpoint.

## Criterios de aceptación
```gherkin
Escenario: Petición con salida estructurada
  Dado un proveedor configurado
  Cuando el caso de uso pide una respuesta con un esquema JSON
  Entonces recibe un objeto tipado ya validado contra el esquema

Escenario: El proveedor devuelve JSON inválido
  Dado un proveedor que responde algo que no cumple el esquema
  Cuando se procesa la respuesta
  Entonces se devuelve un Result de error "InvalidProviderOutput"
  Y no se lanza ninguna excepción hacia el caso de uso

Escenario: Se registra quién respondió
  Dado cualquier respuesta correcta
  Entonces el resultado incluye proveedor, modelo, tokens de entrada y salida y duración

Escenario: Los tests no salen a internet
  Dado la suite de tests
  Cuando se ejecuta
  Entonces solo se usa FakeLlmProvider con respuestas grabadas
```

## Alcance técnico
- **Application:** puerto `ILlmProvider` con un método del estilo `Task<Result<LlmResponse<T>>> CompleteStructuredAsync<T>(LlmRequest request, CancellationToken ct)`, y los tipos `LlmRequest` (rol: `Adapter` o `Verifier`, mensajes de sistema y usuario, `MaxOutputTokens`) y `LlmResponse<T>` (valor, proveedor, modelo, tokens, duración).
- **Infrastructure/Llm:**
  - `OpenAICompatibleProvider` y `AzureFoundryProvider`, implementados con `Microsoft.Extensions.AI` (`IChatClient`) sobre el cliente compatible con OpenAI.
  - Elección del modelo según el **rol** (`Adapter`/`Verifier`) leída de `LlmOptions`.
  - **Factoría** que crea el proveedor principal y, si está habilitado, el de respaldo **según el tipo configurado** (`OpenAICompatible` o `AzureFoundry`). Cambiar de proveedor es cambiar la configuración, no el código.
  - `JsonSchemaValidator` para validar la salida.
  - `PromptLoader` que carga prompts desde `Infrastructure/Llm/Prompts/*.md` (recursos embebidos).
- **tests:** `FakeLlmProvider` con respuestas grabadas en ficheros JSON.
- Endpoint de diagnóstico **solo en Development**: `GET /dev/llm/ping` que hace una llamada mínima a cada proveedor.

## Seguridad
- **OWASP LLM01:** el contenido del usuario va en el mensaje de usuario **entre delimitadores** (`<documento>…</documento>`); el mensaje de sistema dice que lo de dentro son datos, no instrucciones.
- **OWASP LLM05:** la salida no se usa hasta validarla con el esquema.
- **OWASP LLM10:** `MaxOutputTokens` y timeout por petición desde `LimitsOptions`.
- Las API keys se envían solo en cabecera; **nunca** aparecen en logs ni en excepciones.
- `HttpClient` configurado con `IHttpClientFactory`, timeout explícito y sin seguir redirecciones a otros hosts.
- El endpoint `/dev/llm/ping` **no existe** fuera de `Development` (test que lo comprueba).

## Calidad y clean code
- El dominio y los casos de uso **no saben** qué proveedor responde.
- Nada de lógica condicional "si es Azure… si es local…" fuera de la factoría de proveedores.
- Temperatura baja (0 o la mínima permitida) para la adaptación y la verificación, configurable.

## Tests requeridos
- [ ] Unitarios del validador de esquema (válido, campo que falta, tipo incorrecto, JSON truncado).
- [ ] Unitarios de los adaptadores con un `HttpMessageHandler` falso: construcción correcta de la petición, cabeceras, uso del nombre de despliegue en Azure.
- [ ] Test de que las API keys no aparecen en logs (sink de pruebas).
- [ ] Test de que `/dev/llm/ping` devuelve 404 en `Production`.

## Fuera de alcance
- Circuit breaker y respaldo automático (US-09).
- Prompts definitivos de adaptación y verificación (US-06 y US-11).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Llm"
# En local, con .env real (lo ejecuta el humano):
curl -s http://localhost:8080/dev/llm/ping
```

## Definition of Done específica
- [ ] `.env.example` actualizado con las variables de ambos proveedores.
- [ ] Documentado en el README cómo configurar un proveedor compatible con OpenAI (con ejemplos para Ollama y llama-swap) y Azure AI Foundry.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-03-proveedores-llm.md.
Crea la rama feat/US-03-proveedores-llm, preséntame un plan breve y espera mi confirmación.
Antes de escribir los adaptadores, comprueba en la documentación oficial las APIs de Microsoft.Extensions.AI
y del endpoint compatible con OpenAI de Azure AI Foundry; no inventes firmas.
Implementa con tests primero, ejecuta todos los comandos de verificación y termina con un resumen
de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
