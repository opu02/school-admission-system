# School Admission System

Online, **first-come-first-served** school admission. A parent applies for a seat, the seat
is **held** for a limited time, and the admission is **confirmed** when the fee is paid. If
the fee is not paid in time, the seat is released automatically for the next applicant.

| Part | Stack |
|---|---|
| Backend | ASP.NET Core 8 Web API · EF Core 8 · PostgreSQL 16 |
| Frontend | React 18 · TypeScript · Vite · nginx (in Docker) |
| Tests | xUnit + Testcontainers (real PostgreSQL) · Vitest + Testing Library |
| Run | Docker Compose (only Docker is needed) |

---

## Run with Docker

```bash
docker compose up --build
```

| What | URL |
|---|---|
| **Frontend (open this)** | **http://localhost:3000** |
| Backend API | http://localhost:8080 |
| API docs (Swagger) | http://localhost:8080/swagger |
| Health check | http://localhost:8080/health |

Data is stored in the named volume `pgdata`, so it survives `docker compose down` + `docker compose up`.
(`docker compose down -v` deletes it.)

### Configuration (environment variables)

All have defaults; override them in a `.env` file (see `.env.example`) or in the shell.

| Variable | Default | Used by | Meaning |
|---|---|---|---|
| `HOLD_DURATION_SECONDS` | `600` | backend | How long a seat stays held before it is released |
| `ConnectionStrings__Default` | set in compose | backend | PostgreSQL connection string |
| `CORS_ALLOWED_ORIGINS` | `http://localhost:3000,http://localhost:5173` | backend | Browser origins allowed to call the API |
| `HOLD_EXPIRY_SWEEP_SECONDS` | `5` | backend | How often expired holds are marked `EXPIRED` |
| `API_BASE_URL` | `http://localhost:8080` | frontend | Backend URL used by the browser (applied when the container starts — no rebuild needed) |
| `POSTGRES_DB` / `POSTGRES_USER` / `POSTGRES_PASSWORD` | `school_admission` / `admission` / `admission` | db | Database settings |

Example — 30-second holds for a quick expiry demo:

```bash
HOLD_DURATION_SECONDS=30 docker compose up --build        # bash
$env:HOLD_DURATION_SECONDS=30; docker compose up --build  # PowerShell
```

### Bonus: two backend processes

```bash
docker compose --profile multi up --build
```

Starts a second backend on **http://localhost:8081** using the same database. Requests can be sent
to both ports at once and the class still never goes over capacity (see
[ASSUMPTIONS.md](ASSUMPTIONS.md#concurrency--no-overbooking)). This is also covered by an automated test.

---

## Using the app

1. Enter the student's **17-digit birth registration number** at the top (this is the identity; there is no login).
   It is remembered in the browser, so applications are still visible after a refresh.
2. **Create a class** (admin form) — e.g. *Class 6 - Morning Shift*, 120 seats. Each shift is its own class.
3. Press **Apply** on a class. A seat is held and a **countdown** starts under *My applications*.
4. Press **Pay fee** (payment is simulated as successful) → **CONFIRMED**, or **Cancel** → seat released.
5. If the countdown reaches zero, the application turns **EXPIRED** by itself and *Pay fee* is disabled.

Seat numbers refresh every 3 seconds. Errors (no seats, expired, invalid input, server down) are shown as
plain-language messages.

---

## Running the tests

### Backend (xUnit) — requires Docker running

```bash
cd backend
dotnet test
```

The tests start a throw-away **PostgreSQL 16 container** with Testcontainers (a real database is needed to
test row locking). To use an existing database instead, set `TEST_DB_CONNECTION` to a connection string.

| Test | Proves |
|---|---|
| `ConcurrencyTests.Many_parallel_applications_never_overbook_a_class` | 60 parallel applicants, 10 seats → exactly 10 holds |
| `ConcurrencyTests.Same_student_applying_many_times_in_parallel_gets_only_one_seat` | one active application per student per class |
| `ConcurrencyTests.Parallel_confirms_of_the_same_hold_count_the_seat_only_once` | confirm is idempotent |
| `MultiInstanceTests.Two_api_instances_sharing_one_database_never_overbook` | bonus: two app instances, one DB |
| `HoldLifecycleTests.*` | contract shape, automatic expiry (fake clock), cancel, ownership |
| `ValidationTests.*` | 400 / 404 / 409 cases, 17-digit student id |

### Frontend (Vitest)

```bash
cd frontend
npm ci
npm test
```

Covers student-id validation, API error → friendly message mapping, and the application card switching to
`EXPIRED` (with *Pay fee* disabled) when the time runs out.

---

## Local development (without Docker for the app)

```bash
# 1. Database only, published on localhost:5433
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d db

# 2. Backend (Development profile points at localhost:5433) → http://localhost:8080
cd backend/src/SchoolAdmission.Api
dotnet run

# 3. Frontend → http://localhost:5173
cd frontend
npm install
npm run dev
```

`backend/src/SchoolAdmission.Api/SchoolAdmission.Api.http` contains ready-made requests for every endpoint
(Visual Studio / VS Code REST Client).

---

## API

All responses are JSON. The student is identified by the `X-Student-Id` header (17 digits).
Times are UTC ISO-8601 (`2026-11-01T04:10:00Z`).

| Method | Path | Success | Errors |
|---|---|---|---|
| POST | `/classes` `{name, totalSeats}` | 201 | 400, 409 duplicate name |
| GET | `/classes` | 200 | |
| GET | `/classes/{classId}` | 200 | 404 |
| POST | `/classes/{classId}/holds` | 201 `HELD` | 400, 404, 409 `NO_SEATS_AVAILABLE` / `ACTIVE_HOLD_EXISTS` |
| GET | `/holds/{holdId}` | 200 | 403, 404 |
| POST | `/holds/{holdId}/confirm` | 200 `CONFIRMED` (idempotent) | 400, 403, 404, 409 `HOLD_EXPIRED` / `HOLD_CANCELLED` |
| DELETE | `/holds/{holdId}` | 204 | 400, 403, 404, 409 |
| GET | `/me/holds` | 200 | 400 |

Every error has the same shape (RFC 7807 problem details) with a machine-readable `code`:

```json
{ "status": 409, "title": "Conflict", "code": "NO_SEATS_AVAILABLE",
  "detail": "Sorry, there are no free seats in \"Class 6 - Morning Shift\" right now. ...",
  "traceId": "..." }
```

Responses contain every field from the contract; some extra fields were added (allowed by the brief):
`createdAt`, `className`, `studentId`, `confirmedAt`, `cancelledAt` and `serverTime` (used by the UI
countdown so a wrong client clock does not matter).

---

## Repository layout

```
backend/
  src/SchoolAdmission.Api/
    Domain/            entities (SchoolClass, Hold, HoldStatus)
    Data/              DbContext, migrations, startup migrator
    Features/Classes/  controller + service for classes and seat counts
    Features/Holds/    controller + service for holds, background expiry worker
    Common/            errors (problem+json), student id header, JSON/time helpers
  tests/SchoolAdmission.Api.Tests/
  Dockerfile
frontend/
  src/lib/         API client, config, server clock
  src/hooks/       polling, ticking clock, saved student id
  src/components/  StudentBar, ClassList, CreateClassForm, HoldCard, Notices
  Dockerfile, nginx.conf, docker-entrypoint.d/
docker-compose.yml        db + backend + frontend (+ backend-2 with --profile multi)
docker-compose.dev.yml    publishes the db on :5433 for local development
ASSUMPTIONS.md
AI_USAGE.md
```
