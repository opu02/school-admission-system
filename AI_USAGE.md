# AI Usage

## Tools

- **Claude (Anthropic)** — used as a pair programmer throughout the project.
- **Visual Studio 2026** (build, test runner, `.http` requests) and **Docker Desktop** for running and checking everything myself.

## What AI was used for

- Reading the brief and listing the hidden requirements (concurrency across processes, expiry without an
  API call, idempotent confirm, runtime frontend config).
- Drafting the backend (EF Core model and migration, services, controllers, error handling, expiry worker),
  the integration tests, the React UI, the Dockerfiles and these documents.
- Explaining design choices (database row lock vs. in-process locks, polling vs. WebSockets).

I worked part by part: build → run with Docker → test every endpoint by hand with the `.http` file and
Swagger → run the automated tests → commit. Nothing was committed without being built and checked.

## Where the AI was wrong, and how I caught it

| Problem in AI output | How it was caught | Fix |
|---|---|---|
| The `.http` test file declared `@classId` / `@holdId` variables between requests; Visual Studio treated them as part of the previous request (`HTTP0012: Unable to evaluate expression 'classId'`). | Manual testing in Visual Studio. | Moved all variables to the top of the file. |
| Handled 404/409 errors were logged with full stack traces by ASP.NET Core's exception middleware, making normal business errors look like crashes. | Reading the container logs while testing. | Turned off that logger category; real 500 errors are still logged by our handler. |
| Seat counts were first projected into a C# record **constructor**; EF Core cannot filter or sort after such a projection (`GET /classes/{id}` would fail at runtime). | Code review before running. | Switched to a member-initialised class projection. |
| In the hold service, the current time was read **before** waiting for the row lock, so a request that waited in the queue could use an old time and confirm a just-expired hold. | Code review of the concurrency path. | Read the clock only after the lock is acquired. |
| The first two-instance test created the second app with the real clock while the first used the fake test clock — each would disagree about which holds were expired. | Review of the test setup. | Created the second instance from the first (`WithWebHostBuilder`) so both share the clock. |
| The UI showed "No classes yet" when the server was down, which is misleading. | Checking the UI with the backend stopped. | Shows "Classes could not be loaded yet" plus a red retrying banner. |
| Setup: the project targets .NET 8 but my Visual Studio only had the .NET 10 SDK, so the project would not run. | Visual Studio warning on first open. | Installed the .NET 8 runtime component. |

## What I verified myself

- Every endpoint against the API contract (status codes, field names, `Z` timestamps) with the `.http` file.
- Overbooking protection: automated tests with 60 parallel requests and with two app instances.
- Expiry with `HOLD_DURATION_SECONDS=30`: the card switches to `EXPIRED` without reload and the seat comes back.
- `docker compose down` + `up` keeps the data.
