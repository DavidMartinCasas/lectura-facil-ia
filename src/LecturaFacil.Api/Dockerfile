# SPDX-License-Identifier: Apache-2.0
# Contexto de build: raíz del repositorio.
# docker build -f src/LecturaFacil.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0.404-noble AS build
WORKDIR /repo

# Restauración en una capa propia para aprovechar la caché.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/LecturaFacil.Domain/LecturaFacil.Domain.csproj src/LecturaFacil.Domain/
COPY src/LecturaFacil.Application/LecturaFacil.Application.csproj src/LecturaFacil.Application/
COPY src/LecturaFacil.Infrastructure/LecturaFacil.Infrastructure.csproj src/LecturaFacil.Infrastructure/
COPY src/LecturaFacil.Api/LecturaFacil.Api.csproj src/LecturaFacil.Api/
RUN dotnet restore src/LecturaFacil.Api/LecturaFacil.Api.csproj

COPY src/LecturaFacil.Domain/ src/LecturaFacil.Domain/
COPY src/LecturaFacil.Application/ src/LecturaFacil.Application/
COPY src/LecturaFacil.Infrastructure/ src/LecturaFacil.Infrastructure/
COPY src/LecturaFacil.Api/ src/LecturaFacil.Api/
RUN dotnet publish src/LecturaFacil.Api/LecturaFacil.Api.csproj -c Release -o /app --no-restore

# Imagen mínima sin shell ni gestor de paquetes; "extra" incluye ICU para el tratamiento de texto en español.
FROM mcr.microsoft.com/dotnet/aspnet:10.0.15-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "LecturaFacil.Api.dll"]
