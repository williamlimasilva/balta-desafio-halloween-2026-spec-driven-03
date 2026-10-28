# Tasks: Short Link Management

**Input**: Design documents from `specs/001-create-short-links/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/openapi.yaml`

**Tests**: Required by the project constitution: xUnit unit tests for every business rule and at least one SQLite-backed integration happy path for every endpoint. Write tests before their implementation tasks.

**Organization**: Tasks are grouped by user story for traceability and incremental validation.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Tasks can run in parallel because they touch separate files and have no unfinished dependencies.
- **[Story]**: Story label maps to the user stories in `spec.md`.
- Each task names the target file or project path.

## Path Conventions

- Production projects: `src/ShortLinks.Api/`, `src/ShortLinks.Application/`, `src/ShortLinks.Infrastructure/`
- Test projects: `tests/ShortLinks.UnitTests/`, `tests/ShortLinks.IntegrationTests/`
- Solution: repository root `ShortLinks.sln`

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the solution and empty .NET 10 projects with only justified dependencies.

- [x] T001 Create the .NET 10 solution file `ShortLinks.sln`.
- [x] T002 [P] Create the empty Minimal API project `src/ShortLinks.Api/ShortLinks.Api.csproj` targeting `net10.0` without template OpenAPI or unrelated packages.
- [x] T003 [P] Create the application class library `src/ShortLinks.Application/ShortLinks.Application.csproj` targeting `net10.0`.
- [x] T004 [P] Create the infrastructure class library `src/ShortLinks.Infrastructure/ShortLinks.Infrastructure.csproj` targeting `net10.0`.
- [x] T005 [P] Create the xUnit unit-test project `tests/ShortLinks.UnitTests/ShortLinks.UnitTests.csproj` targeting `net10.0`.
- [x] T006 [P] Create the xUnit integration-test project `tests/ShortLinks.IntegrationTests/ShortLinks.IntegrationTests.csproj` targeting `net10.0`.
- [x] T007 Add all five projects to `ShortLinks.sln` and configure references exactly as in `plan.md`: API to Application and Infrastructure; Infrastructure to Application; UnitTests to Application; IntegrationTests to API and Infrastructure; Application has no API/Infrastructure reference.
- [x] T008 Add the justified `Microsoft.EntityFrameworkCore.Sqlite` and private-assets `Microsoft.EntityFrameworkCore.Design` 10.0.x references to `src/ShortLinks.Infrastructure/ShortLinks.Infrastructure.csproj`, plus a private-assets `Microsoft.EntityFrameworkCore.Design` reference to startup project `src/ShortLinks.Api/ShortLinks.Api.csproj` for EF CLI discovery.
- [x] T009 Add the justified `Microsoft.AspNetCore.Mvc.Testing` 10.0.x and SQLite provider references to `tests/ShortLinks.IntegrationTests/ShortLinks.IntegrationTests.csproj`; retain the xUnit and test SDK references created by the xUnit template.
- [x] T010 Add the justified local `dotnet-ef` 10.0.x tool manifest at `.config/dotnet-tools.json`.
- [x] T011 Restore the solution and local tools using `ShortLinks.sln` and `.config/dotnet-tools.json`; resolve any framework or package-version mismatch before continuing.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Establish persistence, shared service contracts, configuration, and test hosts before any user-story implementation.

- [x] T012 Create `LinkEntity` in `src/ShortLinks.Infrastructure/Persistence/LinkEntity.cs` with fields and constraints from `data-model.md`: `Id` is a database-generated `long`; `Code` is unique, case-sensitive, 7 characters when generated and 4-20 when custom, using the documented alphabet; `DestinationUrl` is absolute HTTP/HTTPS with host and maximum 2048 characters; `CreatedAtUtc` is UTC `DateTime`; `ExpiresAtUtc` is nullable UTC `DateTime`; `AccessCount` starts at zero, is nonnegative, and never decreases; `ManagementKeyHash` is lowercase hexadecimal SHA-256; `CreatorIpFingerprint` is lowercase hexadecimal HMAC-SHA-256 and raw IP is never persisted.
- [x] T013 Configure `LinkDbContext` in `src/ShortLinks.Infrastructure/Persistence/LinkDbContext.cs` with binary/case-sensitive unique index on `Code`, index on `(CreatorIpFingerprint, CreatedAtUtc)`, maximum URL length 2048, and check constraint `AccessCount >= 0`.
- [x] T014 Define persistence and application request/result contracts in `src/ShortLinks.Application/Links/ILinkRepository.cs` and `src/ShortLinks.Application/Links/LinkModels.cs`, including create, quota count/oldest timestamp, active redirect, atomic access increment, and protected statistics operations; implement per-IP keyed serialization in `src/ShortLinks.Application/Links/AsyncKeyedLock.cs`.

- [x] T015 Add `LinkOptions` in `src/ShortLinks.Api/Options/LinkOptions.cs` and non-secret defaults in `src/ShortLinks.Api/appsettings.json` for the SQLite file path and public base URL; read `LinkSecurity__ClientIpFingerprintKey` from external configuration and fail startup if it is missing or invalid.
- [x] T016 Register SQLite `LinkDbContext`, Infrastructure repository, `TimeProvider.System`, and ProblemDetails/exception handling in `src/ShortLinks.Api/Program.cs` and `src/ShortLinks.Api/Errors/ApiExceptionHandler.cs`; ensure validation details are Portuguese and HTTP 400 responses use ProblemDetails.
- [x] T017 Generate the timestamped `InitialSchema` EF migration and designer file under `src/ShortLinks.Infrastructure/Persistence/Migrations/`, plus `LinkDbContextModelSnapshot.cs`, from `LinkDbContext`.
- [x] T018 Create `ShortLinksApiFactory` in `tests/ShortLinks.IntegrationTests/Infrastructure/ShortLinksApiFactory.cs` using `WebApplicationFactory<Program>`, a temporary file-backed SQLite database with an exposed stable path for host restart tests, migrations, and a deterministic test HMAC key.
- [x] T019 Create a reusable `FakeLinkRepository` in `tests/ShortLinks.UnitTests/Links/FakeLinkRepository.cs` implementing `ILinkRepository` for isolated xUnit service tests.

**Checkpoint**: Solution restores; migrations create the SQLite schema; integration and unit test hosts can be built; no user-story endpoint is implemented yet.

---

## Phase 3: User Story 1 - Create a Short Link (Priority: P1)

**Goal**: Create a valid short link with an automatically generated seven-character code, return its shareable URL and one-time management key, and enforce the per-IP creation quota.

**Independent Test**: A SQLite-backed POST happy path returns 201 with a seven-character code and management key, persists the link, and rejects invalid URLs with Portuguese 400 ProblemDetails.

### Tests for User Story 1

- [x] T020 [P] [US1] Write xUnit creation-rule tests in `tests/ShortLinks.UnitTests/Links/LinkCreationTests.cs` for absolute HTTP/HTTPS and empty/invalid URLs, host and 2048-character boundaries, seven-character code generation/alphabet, one-time key hash verification, stable IPv4-normalized HMAC fingerprints without raw IP exposure, and per-IP quota behavior (60 allowed, 61st rejected, 61 simultaneous attempts from one IP allow at most 60, invalid creates do not consume quota).
- [x] T021 [P] [US1] Write SQLite-backed create-endpoint tests in `tests/ShortLinks.IntegrationTests/Links/CreateLinkEndpointTests.cs` for the happy path, invalid requests, and 61 simultaneous creates from one IP; assert 201, persisted destination, generated code, `shortUrl` equals configured public base URL plus code, Portuguese ProblemDetails, at most 60 successes, and 429 with `Retry-After` for excess requests.

### Implementation for User Story 1

- [x] T022 [P] [US1] Implement cryptographically random short-code generation with the exact alphabet `ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789` in `src/ShortLinks.Application/Links/ShortCodeGenerator.cs`.
- [x] T023 [P] [US1] Implement random 256-bit management-key generation, SHA-256 hashing, and constant-time verification in `src/ShortLinks.Application/Security/ManagementKeyService.cs`; return plaintext only to the creation response.
- [x] T024 [P] [US1] Implement HMAC-SHA-256 client-IP fingerprinting from stable secret configuration in `src/ShortLinks.Application/Security/ClientIpFingerprintService.cs`; never persist or log raw IP values.
- [x] T025 [US1] Implement `LinkService.CreateAsync` in `src/ShortLinks.Application/Links/LinkService.cs` to validate absolute HTTP/HTTPS destination and maximum 2048 characters, create seven-character codes, check generated-code collisions, issue a management key, and serialize per-IP rolling 60-minute check-and-create so only 60 successful creations count.
- [x] T026 [US1] Implement request/response DTOs in `src/ShortLinks.Api/Contracts/CreateLinkRequest.cs` and `src/ShortLinks.Api/Contracts/CreateLinkResponse.cs`, then map `POST /api/links` in `src/ShortLinks.Api/Endpoints/LinksEndpoints.cs` to return 201 and Portuguese 400/409/429 ProblemDetails.
- [x] T027 [US1] Register `LinkService`, `ShortCodeGenerator`, `ManagementKeyService`, and `ClientIpFingerprintService` in `src/ShortLinks.Api/Program.cs`; resolve client IP only from the direct connection or configured trusted proxies.
- [x] T028 [US1] Run `dotnet test tests/ShortLinks.UnitTests/ShortLinks.UnitTests.csproj` and `dotnet test tests/ShortLinks.IntegrationTests/ShortLinks.IntegrationTests.csproj`; make all User Story 1 tests pass against SQLite.

**Checkpoint**: Link creation is independently usable, the generated code is unique, quota failures do not create links, and the management key is returned only once.

---

## Phase 4: User Story 2 - Redirect and Count Visits (Priority: P1)

**Goal**: Redirect public visitors to active destinations, count each successful visit exactly once, and expose statistics only to a caller with the management key.

**Independent Test**: With a persisted active link, a redirect returns 302 and increments once; a statistics request with the correct key returns that count while missing/invalid keys do not.

### Tests for User Story 2

- [x] T029 [P] [US2] Write xUnit access/statistics service tests in `tests/ShortLinks.UnitTests/Links/LinkAccessTests.cs` for active access, one increment per visit, correct management key, missing/invalid key, and unknown code behavior.
- [x] T030 [P] [US2] Write SQLite-backed redirect tests in `tests/ShortLinks.IntegrationTests/Links/RedirectEndpointTests.cs` for the happy path, malformed-code 400, and 100 concurrent active accesses; assert every valid request returns 302, `Location` preserves the destination path/query/fragment exactly, and `AccessCount` rises by exactly 100.
- [x] T031 [P] [US2] Write SQLite-backed statistics tests in `tests/ShortLinks.IntegrationTests/Links/LinkStatsEndpointTests.cs` for the happy path, malformed-code 400, and missing/invalid-key 401; assert the count is returned only with `X-Management-Key`.

### Implementation for User Story 2

- [x] T032 [US2] Implement `EfLinkRepository` in `src/ShortLinks.Infrastructure/Persistence/EfLinkRepository.cs` with a conditional `ExecuteUpdateAsync` increment filtered by code and active expiry; never read and rewrite `AccessCount` in application memory.
- [x] T033 [US2] Implement active redirect and protected statistics operations in `src/ShortLinks.Application/Links/LinkService.cs`; ensure stats verify the management-key hash and never return the hash or key.
- [x] T034 [US2] Add `GET /{code}` and `GET /api/links/{code}/stats` in `src/ShortLinks.Api/Endpoints/LinksEndpoints.cs`; return 302 with `Location`, 404 for a well-formed unknown code, 410 for expired links, 401 for missing/invalid management key, and 400 ProblemDetails for malformed codes.
- [x] T035 [US2] Run unit and integration tests in `tests/ShortLinks.UnitTests/ShortLinks.UnitTests.csproj` and `tests/ShortLinks.IntegrationTests/ShortLinks.IntegrationTests.csproj`; verify SQLite-backed concurrent increments. Verify persistence across restart with the 100-link scenario in T048.

**Checkpoint**: Redirects remain public; visits are atomic; statistics require the management key; unknown/expired/unauthorized requests do not leak or increment counts.

---

## Phase 5: User Story 3 - Customize Code and Expiration (Priority: P2)

**Goal**: Allow a caller to choose a valid unique code and optional future expiry; expired links never redirect or increment their access count.

**Independent Test**: Create a link with a custom code and future expiry, redirect before expiry, then verify at/after expiry it returns 410 without changing the count; invalid custom codes and past/current expirations return 400.

### Tests for User Story 3

- [x] T036 [P] [US3] Write xUnit customization tests in `tests/ShortLinks.UnitTests/Links/LinkCustomizationTests.cs` for custom lengths 4 and 20, rejection at 3 and 21, exact alphabet, duplicate code conflict, future/null expiry, and past/current expiry rejection.
- [x] T037 [P] [US3] Write SQLite-backed custom-code, expiration, and conflict integration tests in `tests/ShortLinks.IntegrationTests/Links/LinkCustomizationEndpointTests.cs`; verify the original destination is not replaced on duplicate code.
- [x] T038 [P] [US3] Write a SQLite-backed expiration redirect integration test in `tests/ShortLinks.IntegrationTests/Links/LinkExpirationEndpointTests.cs`; assert the exact expiry boundary returns 410 and does not increment `AccessCount`.

### Implementation for User Story 3

- [x] T039 [US3] Extend `LinkService.CreateAsync` in `src/ShortLinks.Application/Links/LinkService.cs` to accept custom codes only when length is 4-20 and every character belongs to `ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789`; accept only null or future expiry and reject past/current values with validation errors.
- [x] T040 [US3] Enforce the unique `Code` index and handle concurrent insert collisions in `src/ShortLinks.Infrastructure/Persistence/EfLinkRepository.cs`; reject a duplicate custom code with conflict and retry generated-code collisions without replacing existing links.
- [x] T041 [US3] Complete expiration-aware redirect handling in `src/ShortLinks.Infrastructure/Persistence/EfLinkRepository.cs`, `src/ShortLinks.Application/Links/LinkService.cs`, and `src/ShortLinks.Api/Endpoints/LinksEndpoints.cs`; at `ExpiresAtUtc <= nowUtc`, return 410 and do not increment or redirect.
- [x] T042 [US3] Run `dotnet test tests/ShortLinks.UnitTests/ShortLinks.UnitTests.csproj`; verify custom-code boundaries, URL length 2048, future/null/past expiry rules, and duplicate-code service behavior.
- [x] T043 [US3] Run `dotnet test tests/ShortLinks.IntegrationTests/ShortLinks.IntegrationTests.csproj`; verify custom-code creation, duplicate conflict without replacement, exact UTC expiry boundary, and unchanged access counts for expired links.

**Checkpoint**: All three user stories work together; custom codes remain unique and expired links cannot be used or counted.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Validate contract fidelity, security, documentation, and all required quality gates.

- [x] T044 [P] Review `specs/001-create-short-links/contracts/openapi.yaml` against all three routes in `src/ShortLinks.Api/Endpoints/LinksEndpoints.cs`, including 400/401/404/409/410/429 ProblemDetails and the `X-Management-Key` header.
- [x] T045 [P] Update `specs/001-create-short-links/quickstart.md` if implementation names, configuration keys, migration commands, response fields, or validation behavior changed.
- [x] T046 Review `src/ShortLinks.Api/`, `src/ShortLinks.Application/`, and `src/ShortLinks.Infrastructure/` for constitution compliance: endpoints do not access the database, rules stay in `LinkService`, all error messages are Portuguese, no raw IP or management key is logged/stored, and package references match `plan.md` justifications.
- [x] T047 Run `dotnet build ShortLinks.sln` and `dotnet test ShortLinks.sln`; ensure every business rule has xUnit coverage and each endpoint has an SQLite-backed integration happy path.
- [x] T048 [P] Write a SQLite-backed restart integration test in `tests/ShortLinks.IntegrationTests/Links/LinkPersistenceTests.cs` that seeds 100 links with distinct destinations, expiry values, and access counts, disposes the API host, starts a new `ShortLinksApiFactory` against the same database file, and verifies all 100 retain destination, expiry, count, and active/expired redirect behavior.
- [x] T049 Run the latency check documented in `specs/001-create-short-links/quickstart.md`: 5 warm-up redirects followed by 100 sequential redirects to one active link; record p95 in the reference environment and assert at least 95 requests complete within 2 seconds.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately. Project creation tasks T002-T006 can run in parallel after T001.
- **Foundational (Phase 2)**: Starts after project scaffolding and package setup; blocks all user stories.
- **User Story 1 (Phase 3)**: Starts after Phase 2; delivers create-link MVP.
- **User Story 2 (Phase 4)**: Depends on the persisted link model and creation flow from US1.
- **User Story 3 (Phase 5)**: Depends on creation from US1 and redirect behavior from US2 to test expiration end-to-end.
- **Polish (Phase 6)**: Starts after all three stories are implemented.

### User Story Dependencies

- **US1 (P1)**: No user-story dependency; requires the foundational SQLite schema, repository contract, configuration, and test hosts.
- **US2 (P1)**: Requires US1 to create durable links and management keys; can be developed after US1 checkpoint.
- **US3 (P2)**: Extends the US1 creation path and US2 redirect path for custom codes and expiry; complete after both P1 stories.

### Parallel Opportunities

- Setup project scaffolds T002-T006 can run concurrently after T001 because each creates a separate project directory.
- Foundational entity tests/fake repository and API options can be prepared in separate files after project references exist; DbContext follows entity creation, and migration follows DbContext registration.
- Within each story, the unit-test file and distinct endpoint integration-test files can be authored in parallel before implementation.
- Short-code generation, management-key handling, and IP fingerprinting use distinct files and can be implemented concurrently after US1 tests are written.
- US2 redirect and stats integration tests use distinct files and may run concurrently; route additions share `LinksEndpoints.cs` and must be consolidated in one change.
- US3 customization tests and expiration tests use separate test files; implementation tasks that share `LinkService.cs` or `EfLinkRepository.cs` must remain sequential.
- User stories are not fully independent: US2 consumes links created by US1; US3 extends the creation and redirect behavior. Do not parallelize whole story phases.

## Parallel Example: User Story 1

```text
After T019 is ready:
- T020 Write unit tests in tests/ShortLinks.UnitTests/Links/LinkCreationTests.cs
- T021 Write endpoint integration tests in tests/ShortLinks.IntegrationTests/Links/CreateLinkEndpointTests.cs

After both test tasks are written:
- T022 Implement ShortCodeGenerator.cs
- T023 Implement ManagementKeyService.cs
- T024 Implement ClientIpFingerprintService.cs
```

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 setup and Phase 2 foundation.
2. Complete Phase 3 create-link tests and implementation.
3. Validate the POST happy path, URL validation, generated code, one-time management key, and per-IP quota against SQLite.
4. Stop at the US1 checkpoint for an independently usable link-creation MVP.

### Incremental Delivery

1. Add US2 public redirect and protected statistics; validate atomic count updates and restart persistence.
2. Add US3 custom codes and expiration; validate boundaries and expired-link behavior.
3. Complete Polish gates, the full solution test suite, and quickstart scenarios.

## Notes

- Every task uses the required checkbox, sequential ID, optional parallel marker, user-story label only in story phases, and an exact file/project path.
- All three endpoint happy paths and all business rules have explicit test tasks, as required by the project constitution.
- Package versions and reasons are documented in `specs/001-create-short-links/plan.md`; do not add dependencies without updating that plan first.
