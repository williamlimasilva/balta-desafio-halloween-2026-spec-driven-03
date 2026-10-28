# Implementation Plan: Short Link Management

**Branch**: `001-create-short-links` | **Date**: 2026-10-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/001-create-short-links/spec.md`

## Summary

Build a .NET 10 Minimal API for creating and redirecting short links, managing
expiration, and reporting access counts. Keep HTTP endpoints thin and place all
business rules in the application-layer `LinkService`. Persist links and access
counts with EF Core and SQLite; keep the provider behind the infrastructure
boundary so PostgreSQL can be introduced later without changing application rules.
Use database uniqueness and atomic updates to make code allocation and access
counting correct under concurrent requests. Require a one-time-returned management
key to read statistics, store only its hash, and enforce the per-IP creation quota.

## Technical Context

**Language/Version**: C# / .NET 10.0

**Primary Dependencies**: ASP.NET Core Minimal APIs; EF Core SQLite 10.0.x;
xUnit; Microsoft.AspNetCore.Mvc.Testing 10.0.x for endpoint integration tests.

**Storage**: Durable SQLite database file for v1; EF Core provider configured at
the infrastructure boundary. PostgreSQL is a future provider, not part of this scope.

**Testing**: xUnit unit tests for business rules; ASP.NET Core integration tests
using `WebApplicationFactory` and the SQLite provider used in production.

**Target Platform**: .NET 10 host with write access to the configured SQLite file;
v1 assumes one API deployment using that database file.

**Project Type**: HTTP web service.

**Performance Goals**: At least 95% of 100 active-link requests complete the
redirect response within 2 seconds; 100 simultaneous accesses produce exactly 100
count increments. SQLite write contention must not lose accepted accesses.

**Constraints**: Validation failures return HTTP 400 with Portuguese
`ProblemDetails`; creation quota is 60 successful links per IP in a rolling
60-minute window; stats require the management key; redirects are public and
unlimited. Persist no management-key plaintext or raw client IP. No raw SQL in
application code. All added dependencies require the justifications below.

**Scale/Scope**: One API, one short-link resource, three HTTP operations, SQLite
single-writer characteristics, and the 60-per-IP hourly creation limit. The
specification does not set a total link-count or availability target.

## Constitution Check

_GATE: Must pass before Phase 0 research. Re-check after Phase 1 design._

Pre-research gate: **PASS**.

- Minimal API handlers delegate to `LinkService`; business rules stay in the
  Application project and database operations stay behind the infrastructure
  repository contract.
- EF Core is the only persistence abstraction exposed to Infrastructure. SQLite
  is the v1 provider; a provider switch does not change endpoint or rule code.
- xUnit unit tests cover each business rule; integration tests exercise every
  endpoint's happy path against SQLite.
- Invalid request data returns HTTP 400 `ProblemDetails` with Portuguese detail.
- Proposed package additions and their written justifications are listed below.
- Plans and design follow the constitution; no exception or waiver is requested.

Post-design gate: **PASS** after reviewing `research.md`, `data-model.md`,
`contracts/openapi.yaml`, and `quickstart.md`.

- All persistence actions remain behind Infrastructure and EF Core; the counter
  uses a provider-translated atomic update rather than application-side rewriting.
- Management credentials are returned only on creation and only hashes are
  persisted. The public redirect route does not expose statistics.
- Every error response retains its status-specific behavior; request-validation
  failures use HTTP 400 `ProblemDetails` in Portuguese.
- The OpenAPI contract contains exactly the three planned routes; each has a
  successful integration path in the xUnit test strategy.
- All dependencies, including the local EF CLI tool, have written justifications.

## Dependency Justification

- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x: required for the selected
  durable SQLite provider and its EF Core LINQ translation. Use the same EF Core
  major/minor version across provider packages.
- `Microsoft.EntityFrameworkCore.Design` 10.0.x (`PrivateAssets=all`): required
  in Infrastructure and the API startup project by design-time EF migration
  commands so schema changes are versioned and repeatable; it is not a runtime
  API dependency and does not flow to consumers.
- `dotnet-ef` local tool 10.0.x: required to create and apply the versioned EF
  Core migrations used to initialize and evolve the durable database schema.
- `xunit`, `xunit.runner.visualstudio`, and `Microsoft.NET.Test.Sdk`: required to
  write, discover, and run the constitution-mandated xUnit unit and integration
  tests with `dotnet test`.
- `Microsoft.AspNetCore.Mvc.Testing` 10.0.x: supplies `WebApplicationFactory`
  and `TestServer` to exercise Minimal API routes through the real HTTP pipeline.
- No Npgsql provider is added now: PostgreSQL support is explicitly future work.
  No external hashing, random-number, or rate-limiting package is needed; use
  .NET cryptography APIs and ASP.NET Core/.NET primitives.

## Project Structure

### Documentation (this feature)

```text
specs/001-create-short-links/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── openapi.yaml
└── tasks.md              # Created later by /speckit-tasks
```

### Source Code (repository root)

```text
src/
├── ShortLinks.Api/
│   ├── Endpoints/
│   │   └── LinksEndpoints.cs
│   └── Program.cs
├── ShortLinks.Application/
│   └── Links/
│       ├── LinkService.cs
│       ├── ILinkRepository.cs
│       └── LinkModels.cs
└── ShortLinks.Infrastructure/
    └── Persistence/
        ├── LinkDbContext.cs
        ├── EfLinkRepository.cs
        └── Migrations/

tests/
├── ShortLinks.UnitTests/
└── ShortLinks.IntegrationTests/
```

**Structure Decision**: The repository currently contains documentation and Spec
Kit files but no application source. Use three production projects to keep HTTP,
application rules, and EF Core persistence independently replaceable without
adding a separate Domain project for this single resource. Use separate xUnit
projects for business-rule unit tests and full endpoint/database integration tests.
Keep one `LinksEndpoints.cs` file for the resource's create, redirect, and stats
routes, as requested.

Project reference graph:

- `ShortLinks.Api` references `ShortLinks.Application` and `ShortLinks.Infrastructure`.
- `ShortLinks.Infrastructure` references `ShortLinks.Application`.
- `ShortLinks.UnitTests` references `ShortLinks.Application`.
- `ShortLinks.IntegrationTests` references `ShortLinks.Api` and
  `ShortLinks.Infrastructure` for the test host and SQLite database setup.
- `ShortLinks.Application` has no project references to API or Infrastructure.

## Complexity Tracking

No constitution violations or waivers. The three production projects are the
minimum boundaries needed to isolate HTTP, application rules, and replaceable
persistence; unit and integration tests remain separate to distinguish their
required scopes.
