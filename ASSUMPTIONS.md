# Assumptions & Design Decisions

Where the brief left something open, this is what I decided and why.

## Concurrency — no overbooking

- **How:** creating a hold runs in a database transaction that first locks the class row with
  `SELECT … FOR UPDATE`. Then it marks overdue holds as expired, checks the student has no active
  application, counts `HELD + CONFIRMED`, and inserts the new hold only if a seat is free.
  All hold creations for the **same class** therefore run one after another **inside PostgreSQL**.
  Different classes do not block each other.
- **Why the database and not C# locks:** a `lock`/`SemaphoreSlim` only works inside one process. The lock
  lives in PostgreSQL, so it also works with **two or more backend processes** (bonus requirement).
  This is tested by `MultiInstanceTests` (two separate app instances, one DB) and can be tried manually
  with `docker compose --profile multi up` (second backend on port 8081).
- **Safety net:** a partial unique index `(class_id, student_id) WHERE status IN ('HELD','CONFIRMED')`
  makes a second active application per student per class impossible even if the code had a bug.
- Confirm and cancel lock the hold row (`FOR UPDATE`), so a confirm racing with an expiry or a second
  confirm is handled correctly.
- Startup migrations take a PostgreSQL advisory lock, so several backend processes can start at the same time.

## Expiry

- `HOLD_DURATION_SECONDS` (default 600) sets the hold time. `expiresAt` is rounded down to whole seconds.
- Seat counts **never depend on a background job**: a `HELD` row whose `expiresAt` has passed is not
  counted, and the API reports it as `EXPIRED` immediately. So the seat is free at the exact moment
  the time ends, with no API call needed.
- A background worker (`HoldExpiryWorker`, every `HOLD_EXPIRY_SWEEP_SECONDS`, default 5 s) also updates the stored
  status to `EXPIRED`, so the database matches what the API shows. Running it in several processes is harmless.
- All "now" values come from the server clock (`TimeProvider`), read **after** the lock is taken.

## Status codes the brief did not specify

| Situation | Response |
|---|---|
| `X-Student-Id` missing | 400 `STUDENT_ID_REQUIRED` |
| `X-Student-Id` not exactly 17 digits | 400 `STUDENT_ID_INVALID` |
| Invalid body (empty name, seats < 1, wrong type) | 400 `VALIDATION_FAILED` with per-field messages |
| Class / hold not found | 404 `CLASS_NOT_FOUND` / `HOLD_NOT_FOUND` |
| Confirm / cancel someone else's hold | 403 `HOLD_NOT_OWNED` |
| Student already has an active (HELD or CONFIRMED) application in the class | 409 `ACTIVE_HOLD_EXISTS` (includes the existing `holdId`) |
| Class name already exists (case and extra spaces ignored) | 409 `CLASS_NAME_TAKEN` |
| Confirm an expired hold | 409 `HOLD_EXPIRED` |
| Confirm a cancelled hold | 409 `HOLD_CANCELLED` |
| Confirm an already confirmed hold | **200** `CONFIRMED` again (idempotent; seat counted once) |
| Cancel an already cancelled hold | **204** (idempotent) |
| Cancel a confirmed admission | 409 `HOLD_ALREADY_CONFIRMED` — the fee is paid; refunds are an office matter |
| Cancel an expired hold | 409 `HOLD_EXPIRED` — there is nothing left to cancel |

409 was chosen over 410 for "expired" because the request conflicts with the hold's current state.

## Other decisions

- **Identity:** the 17-digit birth registration number is the only identity (no login), as in the brief.
  Leading/trailing spaces are trimmed; anything that is not exactly 17 digits is rejected.
- **`GET /holds/{holdId}`:** the header is optional (the contract shows none). If it is sent, it must match
  the hold's student. Hold ids are random 128-bit values, so they cannot be guessed.
- **After expiry or cancel, the student may apply again** to the same class if seats are free.
- **Class names are unique** (case-insensitive) so admins don't create two "Class 6 - Morning" by mistake.
  Seats: 1–100000. Classes cannot be edited or deleted (not required).
- **Ids** are prefixed opaque strings (`c_…`, `h_…`), as allowed by the brief.
- **Extra response fields** (allowed by the brief): `createdAt`, `className`, `studentId`, `confirmedAt`,
  `cancelledAt`, `serverTime`. No contract field was renamed or removed.
- **Errors** use RFC 7807 problem details plus a `code` field, so the UI can show friendly messages.
- **Payment** is simulated: pressing *Pay fee* calls confirm and is treated as successful.

## Frontend

- **Live updates:** classes and the student's applications are polled every 3 s (and when the tab becomes
  visible again). Polling was chosen over WebSockets/SSE for simplicity and robustness at this scale.
- **Countdown accuracy:** responses include `serverTime`; the UI keeps the offset between server and browser
  clocks, so a wrong computer clock does not break the countdown. At zero the card shows `EXPIRED`
  immediately, disables *Pay fee*, and refreshes from the server.
- **Persistence after refresh:** the student id is stored in `localStorage`; applications are always loaded
  from the server (`GET /me/holds`), so nothing important lives only in the browser.
- **Runtime API URL:** Vite bakes `VITE_*` variables in at build time, so the Docker image instead writes
  `config.js` from the `API_BASE_URL` environment variable when the container starts.
- The "Create class" form is on the same page and labelled *Admin* (no roles/authentication were required).
