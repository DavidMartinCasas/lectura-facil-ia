# Lectura Fácil IA

Asistente **open source y autoalojable** que adapta textos administrativos a **lectura fácil** (norma UNE 153101 EX) con modelos de IA locales o en la nube. Genera **borradores** que siempre debe validar una persona.

> 🚧 **En desarrollo.** Proyecto Final del Máster en IA de BigSchool (noviembre de 2026). El README completo llegará con la [US-20](docs/backlog/historias/US-20-readme-y-adrs.md).

## Cómo arrancar en local

### Con Docker (todo junto)

Necesitas Docker con Docker Compose.

```bash
cp deploy/.env.example deploy/.env   # y cambia POSTGRES_PASSWORD
docker compose -f deploy/docker-compose.yml up --build
```

- Aplicación: <http://localhost:8080>
- Estado de la API: `curl http://localhost:8080/health` → `{"status":"Healthy"}`

Solo se publica el frontend, y solo en `127.0.0.1`. nginx reenvía `/api` y `/health` a la API, y PostgreSQL no expone su puerto.

### Para desarrollar

Necesitas el SDK de .NET 10 (la versión está en `global.json`), Node 26 (`.nvmrc`) y Docker para los tests de integración.

```bash
# Backend
dotnet build -warnaserror
dotnet test            # incluye Testcontainers (PostgreSQL) y la cobertura mínima del 80 % en Domain y Application

# Frontend
cd src/LecturaFacil.Web
npm ci
npm start              # http://localhost:4200
npm run lint && npm test -- --watch=false && npm run build
```

Para ejecutar la API fuera de Docker, define la cadena de conexión `ConnectionStrings__Database` (por ejemplo con `dotnet user-secrets`); nunca la escribas en un `appsettings` del repositorio. El documento OpenAPI solo se publica en `Development`, en `/openapi/v1.json`.

## Documentación

- [Documento de diseño](docs/TFM-Lectura-Facil.md)
- [Backlog y plan del sprint](docs/backlog/README.md)
- [Reglas para agentes de programación](AGENTS.md)

## Licencia

[Apache License 2.0](LICENSE).
