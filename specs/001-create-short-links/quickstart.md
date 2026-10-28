# Quickstart: Short Link Management

## Prerequisites

- .NET 10 SDK.
- A writable directory for the SQLite database file.
- Stable secret configuration for the per-IP fingerprint HMAC key. Do not commit
  this value or log it.
- Review the route and payload definitions in
  [contracts/openapi.yaml](contracts/openapi.yaml) and persistence rules in
  [data-model.md](data-model.md).

## Start the API

From the repository root in PowerShell, set a local development key, restore
dependencies and tools, apply migrations, then start the API:

```powershell
$env:LinkSecurity__ClientIpFingerprintKey = [Convert]::ToBase64String(
    [Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
dotnet restore
dotnet tool restore
dotnet ef database update --project src/ShortLinks.Infrastructure --startup-project src/ShortLinks.Api
dotnet run --project src/ShortLinks.Api --urls http://localhost:5000
```

The random value above is suitable only for a local run. Production deployments
must inject the same secret securely into the API configuration and keep it stable
to preserve quota history.

## Validate the Main Flow

In a second PowerShell session, create a link. The response includes the one-time
management key; keep it in memory for the statistics request.

```powershell
$created = Invoke-RestMethod `
    -Method Post `
    -Uri 'http://localhost:5000/api/links' `
    -ContentType 'application/json' `
    -Body (@{
        destinationUrl = 'https://example.org/docs?source=quickstart'
        customCode = 'Guia42'
    } | ConvertTo-Json)

$created.code
$created.shortUrl
$created.managementKey
```

Open the returned `shortUrl` with redirects disabled and verify an HTTP 302 and
the original destination in the `Location` header:

```powershell
curl.exe -i --max-redirs 0 $created.shortUrl
```

Query the count with the management key. After the redirect request, it should be
at least 1. A request without the key or with an invalid key must return 401
`ProblemDetails` and must not reveal the count.

```powershell
Invoke-RestMethod `
    -Uri "http://localhost:5000/api/links/$($created.code)/stats" `
    -Headers @{ 'X-Management-Key' = $created.managementKey }
```

## Validation Scenarios

Run the complete unit and integration suite:

```powershell
dotnet test
```

The suite must verify:

- All three endpoints have a happy-path integration test using SQLite and the
  real HTTP pipeline.
- Generated codes have seven characters; custom codes accept lengths 4 and 20,
  reject lengths 3 and 21, use the documented ambiguity-free alphabet, and
  duplicate custom codes return 409 without replacing the original destination.
- Invalid or overlong destinations and past/current expirations return HTTP 400
  `ProblemDetails` in Portuguese. A URL of exactly 2048 characters is accepted
  when it is otherwise valid.
- Malformed path codes return HTTP 400 `ProblemDetails`; valid but unknown codes
  return 404.
- An active redirect returns 302 and adds exactly one access. One hundred
  concurrent requests all redirect and increase the count by exactly 100.
- Expired links return 410 without a redirect or count increment; unknown codes
  return 404.
- Statistics return the count only with the matching management key; absent and
  invalid keys return 401. The key is returned only during creation and only its
  hash exists in persisted data.
- The 60th successful creation in a rolling 60-minute window for one client IP
  is allowed; the 61st returns 429 with `Retry-After`. Invalid request bodies,
  duplicate custom codes, redirects, and stats do not consume creation quota.
- A created link, expiry, and accumulated count remain intact after stopping and
  restarting the API against the same SQLite file.

## Measure Redirect Latency

Run this check with the API and SQLite file on the same host. It sends 5 warm-up
requests followed by 100 sequential requests to one active link without following
the redirect, then checks that at least 95 responses complete within 2 seconds.
The measured time includes local HTTP client overhead and is intended as a
repeatable reference-environment acceptance check, not a production load test.

```powershell
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$redirectUri = [Uri]$created.shortUrl

for ($index = 0; $index -lt 5; $index++) {
  $response = $client.GetAsync($redirectUri).GetAwaiter().GetResult()
  if ($response.StatusCode -ne [System.Net.HttpStatusCode]::Found) {
    throw "Expected HTTP 302 during warm-up, got $([int]$response.StatusCode)"
  }
  $response.Dispose()
}

$durations = [System.Collections.Generic.List[double]]::new()
for ($index = 0; $index -lt 100; $index++) {
  $timer = [System.Diagnostics.Stopwatch]::StartNew()
  $response = $client.GetAsync($redirectUri).GetAwaiter().GetResult()
  $timer.Stop()
  if ($response.StatusCode -ne [System.Net.HttpStatusCode]::Found) {
    throw "Expected HTTP 302, got $([int]$response.StatusCode)"
  }
  $durations.Add($timer.Elapsed.TotalMilliseconds)
  $response.Dispose()
}
$client.Dispose()

$sortedDurations = @($durations | Sort-Object)
$p95Index = [Math]::Ceiling(0.95 * $sortedDurations.Count) - 1
$p95Milliseconds = $sortedDurations[$p95Index]
[PSCustomObject]@{
  Requests = $sortedDurations.Count
  P95Milliseconds = [Math]::Round($p95Milliseconds, 2)
}
if ($p95Milliseconds -gt 2000) {
  throw 'Redirect latency did not meet the 2-second p95 acceptance criterion.'
}
```

## HTTP Error Summary

| Status | Meaning                                                             |
| ------ | ------------------------------------------------------------------- |
| 400    | Invalid request or validation failure; Portuguese `ProblemDetails`. |
| 401    | Management key missing or invalid; no count is returned.            |
| 404    | Short code does not exist.                                          |
| 409    | Requested custom code is already in use.                            |
| 410    | Link has expired and was not counted.                               |
| 429    | Per-IP creation quota exceeded; includes `Retry-After`.             |
