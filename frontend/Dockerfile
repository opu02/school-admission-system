# syntax=docker/dockerfile:1

# ---------- Build stage ----------
FROM node:20-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

COPY . .
RUN npm run build

# ---------- Runtime stage: static files served by nginx ----------
FROM nginx:1.27-alpine AS runtime

COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
# Scripts in /docker-entrypoint.d run automatically before nginx starts.
COPY docker-entrypoint.d/40-write-runtime-config.sh /docker-entrypoint.d/40-write-runtime-config.sh
RUN chmod +x /docker-entrypoint.d/40-write-runtime-config.sh

# Backend URL as seen from the user's browser. Override at run time.
ENV API_BASE_URL=http://localhost:8080
EXPOSE 80
