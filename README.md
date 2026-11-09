# School Admission System

Online, first-come-first-served school admission with time-limited seat holds.

- **Backend:** ASP.NET Core 8 Web API · EF Core 8 · PostgreSQL 16
- **Frontend:** React 18 · TypeScript · Vite *(in progress)*
- **Run:** Docker Compose

> 🚧 Work in progress. Full documentation will be added as the project is completed.

## Quick start

Requirements: Docker only.

```bash
docker compose up --build
```

| Service | URL |
|---|---|
| Backend API | http://localhost:8080 |
| API docs (Swagger) | http://localhost:8080/swagger |
| Health check | http://localhost:8080/health |

## Repository layout

```
backend/    ASP.NET Core API (src/) and tests (tests/)
frontend/   React UI
docker-compose.yml
```
