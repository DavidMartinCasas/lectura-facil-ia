# US-16 · Protección frente a abuso: Turnstile y límites

| Campo | Valor |
|---|---|
| **Épica** | EP-07 · Seguridad y acceso |
| **Prioridad** | Must |
| **Puntos** | 2 |
| **Día previsto** | Domingo 22 |
| **Depende de** | US-15 |
| **Requiere humano** | Sí: claves de Cloudflare Turnstile (site key y secret) |

## Historia
**Como** responsable del servicio
**quiero** frenar a los bots y limitar cuántas adaptaciones puede pedir cada usuario
**para** que nadie agote los modelos ni dispare el gasto del proveedor.

## Contexto para el agente
- Turnstile: el frontend obtiene un token del widget y el backend lo valida con `https://challenges.cloudflare.com/turnstile/v0/siteverify`. Comprueba la API en la documentación oficial.
- Turnstile es **configurable** (`Turnstile.Enabled`) y viene **deshabilitado por defecto**, porque requiere una cuenta de Cloudflare. La instancia pública del TFM lo habilita.
- El **aviso configurable** de `UiOptions` (US-02) se muestra como banner en todas las pantallas si tiene texto.
- Para facilitar la prueba al tribunal, se siembran **6 textos de ejemplo** sin datos personales.

## Criterios de aceptación
```gherkin
Escenario: Turnstile habilitado
  Dado Turnstile habilitado en la configuración
  Cuando un usuario envía un documento sin token o con un token inválido
  Entonces la respuesta es 400
  Y no se llama a ningún LLM

Escenario: Turnstile deshabilitado
  Dado Turnstile deshabilitado
  Cuando un usuario envía un documento
  Entonces se procesa sin pedir el desafío

Escenario: Límite de adaptaciones
  Cuando un usuario supera el número de adaptaciones por hora configurado (por defecto 10)
  Entonces la respuesta es 429 con la cabecera Retry-After

Escenario: Límite de login
  Cuando una IP supera el número de intentos de login por minuto configurado
  Entonces la respuesta es 429

Escenario: Textos de ejemplo
  Dado un usuario autenticado
  Cuando abre "Ejemplos"
  Entonces puede lanzar la adaptación de cualquiera de los 6 textos de ejemplo con un clic
```

## Alcance técnico
- **Application:** puerto `IHumanVerification`.
- **Infrastructure:** `TurnstileVerifier` (cliente HTTP con timeout); implementación nula cuando está deshabilitado.
- **Api:** `RateLimiter` de ASP.NET Core por usuario (adaptaciones y exportaciones) y por IP (login); valores en `Options`.
- Siembra de 6 textos de ejemplo (`GET /api/samples`).
- **Angular:** widget de Turnstile en el formulario de envío (solo si está habilitado), página "Ejemplos" y banner con el aviso configurable (`GET /api/config/public` devuelve solo datos públicos: aviso, si Turnstile está activo y su site key).
- `.env.example` con `TURNSTILE__ENABLED`, `TURNSTILE__SITEKEY`, `TURNSTILE__SECRETKEY` y los límites.

## Seguridad
- **OWASP API4 (consumo sin límites) y LLM10:** límites por usuario e IP, además del tamaño máximo de documento.
- **Bots:** el token de Turnstile se valida **en el servidor**; la secret key solo en el entorno.
- Si la validación de Turnstile falla por timeout, se **rechaza** la petición (fallo seguro).
- Los textos de ejemplo no contienen datos personales y declaran su fuente.

## Calidad y clean code
- La comprobación de Turnstile es un filtro de endpoint reutilizable, no código repetido en cada endpoint.
- Límites con nombres de política claros (`adaptations`, `login`, `exports`).

## Tests requeridos
- [ ] Unitarios de `TurnstileVerifier` con `HttpMessageHandler` falso (válido, inválido, timeout = rechazo).
- [ ] Integración: 400 sin token y **ninguna llamada al LLM** (verificado con doble).
- [ ] Integración: 429 al superar el límite de adaptaciones y el de login.
- [ ] Integración: con Turnstile deshabilitado, el flujo funciona sin token.

## Fuera de alcance
- Cuotas por organización o planes de uso.

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Turnstile|FullyQualifiedName~RateLimit|FullyQualifiedName~Samples"
```

## Definition of Done específica
- [ ] `.env.example` actualizado.
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-16-antiabuso.md.
Crea la rama feat/US-16-antiabuso, preséntame un plan breve y espera mi confirmación.
Comprueba en la documentación oficial la API de verificación de Cloudflare Turnstile.
Implementa con tests primero, ejecuta todos los comandos de verificación y termina con un resumen
de lo hecho, cómo probarlo, lo pendiente y los riesgos. No hagas push ni merge.
```
