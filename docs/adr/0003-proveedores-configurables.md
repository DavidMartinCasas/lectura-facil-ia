# ADR 0003 · Proveedores configurables: privacidad por configuración del despliegue

- **Estado:** propuesta (US-09)
- **Fecha:** 2026-11-16

## Contexto

Lectura Fácil IA es open source y autoalojable. Cada organización lo instala con los proveedores de IA que elige: un servidor propio (Ollama, llama-swap, vLLM…), un proveedor en la nube (Azure AI Foundry) o ambos. Los documentos administrativos pueden contener datos personales, así que **adónde viajan** es una decisión de privacidad, y la toma y responde de ella quien despliega, no la persona que usa la herramienta.

A la vez, un servidor propio puede caer (mantenimiento, suspensión, saturación), y para algunos despliegues la disponibilidad importa más que no salir nunca de la infraestructura propia.

## Decisión

1. **La aplicación funciona siempre igual.** No hay modos ni selección de proveedor por parte del usuario: el comportamiento lo fija la configuración (`Llm:Primary`, `Llm:Fallback`, `Llm:Resilience`).
2. **Un principal obligatorio y un respaldo opcional, deshabilitado por defecto.** Sin configurar nada, ningún documento sale del principal.
3. **La política de respaldo vive en Application**, en un decorador (`ResilientLlmProvider`) de `ILlmProvider`. Ni los casos de uso ni los adaptadores la conocen. Con `Fallback.Enabled = false` el decorador **descarta el respaldo aunque exista** (defensa en profundidad: además, la composición ni siquiera lo crea). Las dos garantías tienen test propio.
4. **El respaldo solo entra cuando el principal no está disponible** (no responde, error de red o 5xx, circuito abierto) o la última comprobación de salud dice que no responde. Una respuesta que no cumple el esquema **no** activa el respaldo: el principal respondió y el documento no debe salir por eso.
5. **Resiliencia en el cliente HTTP del principal** con `Microsoft.Extensions.Http.Resilience` (Polly v8, MIT/BSD-3): un reintento corto y un circuit breaker con umbrales en `Options`. El respaldo no se reintenta.
6. **Comprobación de salud periódica** con la petición mínima del diagnóstico (`ping.v1`, sin ningún documento) al principal y, solo si está habilitado, al respaldo. Su resultado se expone en `/health` (como `Degraded`, para no marcar la API como caída) y en `GET /api/status/llm`, que solo dice *disponible / no disponible*.
7. Cada respuesta lleva el **nombre visible** (`DisplayName`) del proveedor que la generó, para que la interfaz y la versión guardada indiquen quién procesó cada documento.

## Configuraciones posibles

| Configuración | Privacidad | Disponibilidad |
|---|---|---|
| Solo principal local (por defecto) | Ningún documento sale de la infraestructura propia | Si el principal cae, los documentos esperan |
| Principal local + respaldo en la nube | Los documentos salen al respaldo solo mientras el principal no responde | Alta |
| Solo nube | Todo se procesa en el proveedor elegido | La del proveedor |

## Consecuencias

- Habilitar el respaldo es una decisión consciente y documentada (README, `deploy/.env.example`), no un efecto secundario.
- Con un respaldo de pago, la comprobación de salud consume unos pocos tokens por intervalo; se puede alargar o desactivar.
- Antes de la primera comprobación los proveedores se suponen disponibles: se intenta siempre primero el principal.
- Sin respaldo, el decorador devuelve `ProviderUnavailable` y es el caso de uso quien decide qué hacer con el documento (dejarlo en cola y reprocesarlo cuando la comprobación vuelva a verde). Esa cola llega con la adaptación (US-06).
- No hay más de dos proveedores ni balanceo de carga (fuera de alcance).
