# Data Model: Short Link Management

## Link

Represents a durable short code and all state required to resolve it, report its
usage, authorize statistics access, and enforce the per-IP creation quota.

| Field                  | Type        | Required | Rules                                                                                                                                                                      |
| ---------------------- | ----------- | -------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Id`                   | `long`      | Yes      | Database-generated primary key.                                                                                                                                            |
| `Code`                 | `string`    | Yes      | Unique, case-sensitive; generated codes have exactly 7 characters; custom codes have 4-20 characters; both use `ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789`. |
| `DestinationUrl`       | `string`    | Yes      | Absolute HTTP or HTTPS URL with host; maximum 2048 characters; preserve path, query, and fragment.                                                                         |
| `CreatedAtUtc`         | `DateTime`  | Yes      | UTC creation instant; used in rolling quota checks.                                                                                                                        |
| `ExpiresAtUtc`         | `DateTime?` | No       | UTC instant; null means no expiration; active only while current UTC time is earlier.                                                                                      |
| `AccessCount`          | `long`      | Yes      | Starts at zero; nonnegative; incremented atomically in the database and never decremented.                                                                                 |
| `ManagementKeyHash`    | `string`    | Yes      | Lowercase hexadecimal SHA-256 hash of a random 256-bit management key; plaintext is returned only in the creation response and never persisted.                            |
| `CreatorIpFingerprint` | `string`    | Yes      | Lowercase hexadecimal HMAC-SHA-256 fingerprint of the request IP using stable secret configuration; raw IP is not persisted.                                               |

## Indexes and Constraints

- Primary key on `Id`.
- Unique index on `Code`, using binary/case-sensitive comparison. The service
  performs an early collision check for a useful error; the index is authoritative
  when requests race.
- Index on `(CreatorIpFingerprint, CreatedAtUtc)` for rolling 60-minute quota
  lookups.
- Database check constraint requires `AccessCount >= 0`.
- Application validation enforces URL, code alphabet/length, and future expiration
  before persistence. The quota check and link insert run as one application
  service operation, serialized for the same fingerprint in the single API process.

## State and Behavior

- A link with `ExpiresAtUtc = null` is active indefinitely.
- A link with `ExpiresAtUtc > nowUtc` is active. A link with
  `ExpiresAtUtc <= nowUtc` is expired and cannot redirect or gain access count.
- Expiration is derived from the stored timestamp; no background job or status
  mutation is required.
- A successful redirect atomically increments `AccessCount` only if the code
  exists and is active at the UTC instant sampled for that access. A zero-row
  update means no redirect is issued.
- A statistics query requires a management key whose hash matches the stored
  hash. Code-only lookup does not authorize reading `AccessCount`.
- The per-IP quota counts successfully persisted links created during
  `[nowUtc - 60 minutes, nowUtc]`; invalid requests and failed inserts do not
  consume quota. The fingerprint secret must remain stable for quota history to
  remain continuous.

## Persistence Notes

- Use UTC `DateTime` rather than `DateTimeOffset`: SQLite supports equality for
  `DateTimeOffset`, but not database-side comparison or ordering through EF Core.
- Keep provider configuration, EF mappings, indexes, transactions, and migrations
  in `ShortLinks.Infrastructure`. Application code uses repository operations and
  does not issue raw SQL.
- No individual access rows are stored. The aggregate counter is the required
  usage record, keeping the schema small and matching the feature's reporting need.
