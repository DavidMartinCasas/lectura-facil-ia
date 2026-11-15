# US-15 · Login y autorización por documento

| Campo | Valor |
|---|---|
| **Épica** | EP-07 · Seguridad y acceso |
| **Prioridad** | Must |
| **Puntos** | 3 |
| **Día previsto** | Sábado 21 |
| **Depende de** | US-04 |
| **Requiere humano** | No (la contraseña del usuario de prueba se define en `.env`) |

## Historia
**Como** técnica de una administración
**quiero** entrar con mi usuario y ver solo mis documentos
**para** que nadie más pueda leer los documentos que adapto.

## Contexto para el agente
- El TFM exige un **usuario y contraseña de prueba** para el tribunal (se documentan en el README; la contraseña viene del entorno).
- Patrón: **cookie de sesión del propio backend** (estilo BFF), frontend y API en el mismo origen detrás de nginx. Nada de tokens en `localStorage`.
- Curso, módulos 08 y 11: autenticación, autorización y gestión de credenciales.

## Criterios de aceptación
```gherkin
Escenario: Login correcto
  Dado el usuario de prueba sembrado desde el entorno
  Cuando introduzco su correo y contraseña
  Entonces entro a "Mis documentos"
  Y la cookie de sesión es HttpOnly, Secure y SameSite=Strict

Escenario: Login incorrecto
  Cuando la contraseña es incorrecta
  Entonces veo "Correo o contraseña incorrectos" (sin indicar cuál de los dos falla)

Escenario: Bloqueo por intentos
  Cuando fallo 5 veces seguidas
  Entonces la cuenta queda bloqueada 15 minutos

Escenario: Documento de otro usuario
  Dado dos usuarios A y B
  Cuando A pide GET /api/documents/{id de B}
  Entonces la respuesta es 404

Escenario: Sin sesión
  Cuando llamo a cualquier endpoint de documentos sin sesión
  Entonces la respuesta es 401 y el frontend redirige al login

Escenario: Cerrar sesión
  Cuando pulso "Salir"
  Entonces la cookie se invalida en el servidor
```

## Alcance técnico
- **ASP.NET Core Identity** con almacenamiento en PostgreSQL y autenticación por cookie.
- Endpoints: `POST /api/auth/login`, `POST /api/auth/logout`, `GET /api/auth/me`.
- **Siembra** del usuario de prueba desde `SEED__TESTUSER__EMAIL` y `SEED__TESTUSER__PASSWORD` (solo si no existe).
- Sustituir el `ICurrentUser` de desarrollo por el real.
- **Antiforgery** para las peticiones que cambian estado (cabecera con token leído de una cookie no HttpOnly específica, patrón doble envío).
- Angular: pantalla de login, `AuthGuard` y gestión del 401 en el interceptor.
- Filtro de propietario aplicado en **todas** las consultas de documentos del repositorio (no en cada endpoint por separado).

## Seguridad
- **OWASP API1 (BOLA):** filtro de propietario en el repositorio y tests por endpoint.
- **OWASP API2 (autenticación rota):** hash de Identity, bloqueo por intentos, mensajes genéricos, límite de peticiones en `/api/auth/login`.
- Política de contraseña mínima de 12 caracteres.
- Sesión con caducidad deslizante razonable (por ejemplo, 8 horas) y regeneración del identificador al iniciar sesión.
- La contraseña del usuario de prueba **nunca** en el repositorio.

## Calidad y clean code
- La autorización por propietario es una **regla del repositorio**, no código repetido en cada caso de uso.
- `ICurrentUser` en Application; su implementación con `HttpContext` en Api.

## Tests requeridos
- [ ] Integración: login correcto, incorrecto, bloqueo, logout.
- [ ] **Seguridad: matriz de BOLA** para todos los endpoints de documentos (A no accede a nada de B).
- [ ] Seguridad: petición que cambia estado sin token antiforgery → 400.
- [ ] Unitario: la siembra no duplica el usuario.
- [ ] E2E: login → subir documento → logout → acceso denegado.

## Fuera de alcance
- Registro de usuarios, recuperación de contraseña, MFA y roles (versión 2).

## Verificación
```bash
dotnet test --filter "FullyQualifiedName~Auth|FullyQualifiedName~Bola"
```

## Definition of Done específica
- [ ] README: sección "Usuario de prueba" (el correo; la contraseña se entrega en el formulario del TFM).
- [ ] Más la **DoD global**.

## Prompt de arranque
```text
Lee AGENTS.md y docs/backlog/historias/US-15-login-y-autorizacion.md.
Crea la rama feat/US-15-login, preséntame un plan breve y espera mi confirmación.
Implementa con tests primero, incluida la matriz de BOLA para todos los endpoints de documentos.
Ejecuta todos los comandos de verificación y termina con un resumen de lo hecho, cómo probarlo,
lo pendiente y los riesgos. No hagas push ni merge.
```
