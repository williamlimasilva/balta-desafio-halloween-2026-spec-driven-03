# Research: Short Link Management

## Decisions

### Application boundaries

- **Decision**: Use `ShortLinks.Api`, `ShortLinks.Application`, and
  `ShortLinks.Infrastructure`, with `LinkService` owning business rules and
  repository interfaces defined in Application.
- **Rationale**: This keeps Minimal API handlers free of persistence and business
  logic while making the EF Core provider replaceable without adding a separate
  Domain project for one resource.
- **Alternatives considered**: A single project was simpler initially but would
  couple HTTP, business rules, and persistence; a separate Domain project adds a
  fourth production project without a distinct model requirement in this feature.

### SQLite and provider replacement

- **Decision**: Use EF Core 10.0.x with `Microsoft.EntityFrameworkCore.Sqlite`
  10.0.x and a durable database file for v1. Keep provider configuration and
  `LinkDbContext` in Infrastructure; add PostgreSQL only in a later feature.
- **Rationale**: SQLite is the requested first provider and is sufficient for the
  single-instance v1 assumption. EF Core provides relational LINQ and migrations
  while keeping Application independent of provider-specific APIs.
- **Alternatives considered**: PostgreSQL now conflicts with the requested SQLite
  scope; EF Core InMemory does not validate relational constraints and is not a
  substitute for the production SQLite provider.
- **Provider note**: SQLite cannot compare/order `DateTimeOffset` values through
  EF Core. Persist timestamps as UTC `DateTime` and compare them in UTC. SQLite
  migration scripts also do not support idempotent scripts; use versioned
  migrations with `dotnet ef database update`.

### Code generation and uniqueness

- **Decision**: Generate seven characters using
  `ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789`; validate custom
  codes against the same alphabet and 4-20 character length. Check for collisions
  in the service, then enforce a unique database index on the exact code.
- **Rationale**: The alphabet omits visually ambiguous I, O, l, o, 0, and 1. The
  service check gives a clear conflict path, while the unique index is the final
  authority under races. If a generated code collides, generate another; a
  conflicting custom code returns a conflict without replacing existing data.
- **Alternatives considered**: Relying on an application-only lookup has a race
  between lookup and insert; accepting the full alphanumeric alphabet violates
  the requested readability rule.

### Access counting

- **Decision**: Increment `AccessCount` with one conditional EF Core
  `ExecuteUpdateAsync` statement that adds one in the database. Filter by link
  code and active expiration at the sampled UTC access time; redirect only if one
  row was updated.
- **Rationale**: The database performs the increment without a read-modify-write
  race. Including expiration in the update prevents a request whose access time
  is at or after expiry from incrementing the counter.
- **Alternatives considered**: Loading the entity, incrementing in memory, and
  saving can lose increments when requests race. Raw SQL is unnecessary and
  prohibited from spreading through the application.

### Management key

- **Decision**: Generate a cryptographically random 256-bit management key at
  creation, return it only in the creation response, and persist its SHA-256 hash
  as lowercase hexadecimal. Require it in the `X-Management-Key` header for stats;
  compare hashes with a constant-time comparison. Never log or return it again.
- **Rationale**: The creator can retrieve metrics without user accounts, while
  the short code remains public for redirects. A uniformly random 256-bit key
  has enough entropy for a fast cryptographic hash rather than a password KDF.
  A header avoids placing the secret in URLs, browser history, or referrer logs.
- **Alternatives considered**: Public stats contradict the accepted clarification;
  storing plaintext exposes the credential if the database is disclosed; a slow
  password hash is unnecessary for a machine-generated high-entropy secret.

### Per-IP creation quota

- **Decision**: Permit at most 60 successfully created links from an IP in any
  rolling 60-minute period. Persist an HMAC-SHA-256 fingerprint of the creator IP
  on each link and use the creation timestamps to check the quota. Use a
  per-fingerprint asynchronous lock around check-and-create in the single API
  instance. Do not apply the quota to redirect or stats routes.
- **Rationale**: Persisted timestamps keep the quota effective across process
  restarts without storing raw IP addresses. HMAC prevents straightforward
  recovery of low-entropy IP values from a plain digest. The per-key lock makes
  the check-and-create sequence atomic within the explicitly single-instance v1.
- **Alternatives considered**: An in-memory ASP.NET Core limiter resets on
  restart and cannot count only successful persisted creations. A multi-instance
  deployment requires a shared atomic quota mechanism and is out of v1 scope.
- **Operational constraint**: Supply a stable secret configuration value for
  the HMAC key; rotating it changes fingerprints and can reset the effective
  quota for prior links. Trust forwarded client IP headers only from configured
  reverse proxies; otherwise use the direct connection IP.

### API errors and tests

- **Decision**: Use three Minimal API routes in `LinksEndpoints.cs`: create,
  redirect, and stats. Return Portuguese `ProblemDetails` for errors; use 400 for
  invalid input, 409 for a custom code already in use, 404 for an unknown code,
  410 for an expired link, 401 for a missing/invalid management key, and 429 for
  a creation quota rejection (with `Retry-After`).
- **Rationale**: Status codes distinguish invalid input, resource state,
  authorization, and throttling while preserving the constitution's 400 rule for
  validation errors. Each route remains independently testable.
- **Testing**: xUnit unit tests cover every `LinkService` business rule. Separate
  integration tests use `WebApplicationFactory` with SQLite and cover a happy
  path for each endpoint, database uniqueness, concurrent atomic increments,
  persistence after restart, authorization, expiration, and quota boundaries.
- **Alternatives considered**: EF Core InMemory does not prove SQLite unique
  indexes, translation, or migration behavior; adding a mocking framework is not
  needed to test these boundaries.

## References

- [EF Core SQLite provider](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/)
- [EF Core SQLite limitations](https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations)
- [EF Core ExecuteUpdate and ExecuteDelete](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete)
- [ASP.NET Core integration tests](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0)
