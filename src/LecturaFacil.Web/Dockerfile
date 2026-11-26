# SPDX-License-Identifier: Apache-2.0
# Contexto de build: esta carpeta.

FROM node:26.13.1-alpine3.23 AS build
WORKDIR /app
COPY package.json package-lock.json .npmrc ./
RUN npm ci
COPY . .
RUN npm run build

# nginx sin root: escucha en 8080, sirve Angular y hace de proxy a la API.
FROM nginxinc/nginx-unprivileged:1.31.7-alpine AS runtime
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/lectura-facil-web/browser /usr/share/nginx/html
EXPOSE 8080
