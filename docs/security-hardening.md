# Security hardening and upgrade notes

The September 2026 security review findings R01–R11 are addressed by the changes below. No production environment has been deployed or modified by this work.

| Finding | Implemented control |
|---|---|
| R01: access after logout/reset/replay | JWT validation (`SessionValidator`, `OnTokenValidated`) requires a live refresh-token family matching the token's session claim; permissions check the user and tenant. Revocation takes effect on subsequent requests. |
| R02: stale password-reset links | Password changes and resets consume all usable reset links in the same transaction. |
| R03: plaintext Data Protection keys | Certificate encryption for new keys; existing plaintext key material is wrapped at startup without changing key identifiers. Private certificates are stored outside PostgreSQL. |
| R04: concurrent reset issuance | Credential operations serialize on the user's PostgreSQL row until commit; redemption reloads the token after obtaining the lock. |
| R05: authorization bypasses request limits | The limiter runs between authentication and authorization, before permission queries, and returns a correlated 429 with Retry-After when available. |
| R06: undefined SMTP mode | Undefined enum values fail validation; the sender rejects unknown modes instead of falling back to plaintext. |
| R07: development services exposed | All published compose ports bind to 127.0.0.1. Development secrets are never production settings. |
| R08: unexpected package contents | Both packaging and generation use explicit source allowlists; CI inspects package entries before release. |
| R09: unbounded reads/inputs | Todos supports page/pageSize (default 1/50, maximum page 10000 and size 100). Labels: at most 20, each 1–100 characters. Authentication input lengths are capped; Kestrel bodies are limited to 64 KiB. User/tenant page bounds prevent integer overflow. |
| R10: stale data across hosts | Todo detail reads go directly to PostgreSQL. No local cache is used for mutable Todo details. |
| R11: misleading email health | Backlog health includes expired leases and expired/exhausted pending messages, regardless of this host's worker flag; a run of failed/expired deliveries (`HealthFailureThreshold` within `HealthFailureWindowMinutes`, default 5 in 60 minutes) degrades health. Readiness remains database-only. |

## Configuration required before production

All application instances sharing a database must use the same active certificate and retain the previous decryption certificates. Supply an RSA PFX with a private key (at least 2048 bits), kept outside the database and its backups:

```text
DataProtection__CertificatePath=/run/secrets/data-protection.pfx
DataProtection__CertificatePassword=<from your secret manager>
DataProtection__PreviousCertificates__0__Path=/run/secrets/previous-data-protection.pfx
DataProtection__PreviousCertificates__0__Password=<previous password, when needed>
```

Startup fails outside Development if the certificate is missing, unreadable, lacks an RSA private key, or the active certificate is not currently valid. Protect the private key with host ACLs or a read-only secret mount. Back it up separately. Keep old decryption certificates until every retained Data Protection key they protect can be retired safely. Configure the new active certificate and all retained old certificates on every instance during rotation.

Development creates a local certificate at `<ContentRoot>/.containers/data-protection/development.pfx`. This directory is ignored by Git and excluded from Docker build context and template distribution. Docker compose persists it in a dedicated named volume. Do not copy this development certificate to production. If multiple development machines share one database, explicitly configure the same certificate on them.

The startup upgrade wraps plaintext key XML already in the database using ASP.NET Core's certificate XML encryptor. It preserves key IDs, so queued mail remains decryptable. A PostgreSQL advisory transaction lock coordinates simultaneous upgrade attempts. Deploy the updated version to all instances; an old binary cannot decrypt certificate-protected keys. Do not roll back only the binaries after the upgrade. Take a backup first, keep its protection certificate separate, and restrict access to older backups containing plaintext keys. Historical leaked copies cannot be made secret again by a code change.

Configure production JWT secrets, issuer/audience, database credentials, ClientApp HTTPS URL and SMTP TLS explicitly. Enable at least one email worker. Use a restricted database runtime account and a separate migration account; apply migrations before startup. Terminate TLS at a trusted reverse proxy and configure KnownProxies/KnownNetworks for that deployment. Per-process rate limits should be supplemented by a shared ingress limit when horizontally scaling.

## API behavior changes

- A token for a revoked, expired or unknown session family fails authentication: 401 on protected endpoints, like a missing or invalid JWT, so clients know to sign in again. 403 remains for authenticated users who lack a permission. Normal refresh rotation preserves the family and existing access tokens until a revocation or expiration.
- Password change preserves its current session, revokes other sessions, and invalidates preexisting reset links. Password reset revokes every session.
- Credential changes and refresh/logout use a shared user row lock. The login lock already used by this template participates in the same ordering.
- `GET /api/v1/todos?page=1&pageSize=50` still returns an array. Clients that previously assumed all records must now fetch pages until a page contains fewer than pageSize items. Invalid bounds return 400.
- Request bodies above 64 KiB are rejected by Kestrel. A reverse proxy should enforce the same or a tighter limit. In-memory TestServer does not emulate Kestrel's transport limits.
- `/health` may return Degraded even on API-only instances when the shared email queue has delivery problems. The standard health endpoint can still return HTTP 200 for Degraded; monitoring must inspect the health status. `/health/ready` remains suitable for database readiness.

## Verification

Run `dotnet build CleanArchitecture.slnx`, then `dotnet test --solution CleanArchitecture.slnx` with Docker running. SecurityHardeningTests cover access revocation, stale reset links, concurrent reset requests, encrypted keys and restart decryption, legacy key upgrades, SMTP validation, throttling of forbidden requests, and request body limits on a real Kestrel server. TodoHardeningTests cover bounded pages and visibility across two application hosts. Outbox tests cover disabled workers, expired leases and terminal failures.

Run `dotnet pack template-pack -o artifacts`, then `./scripts/test-template-package.ps1 -PackagePath <package.nupkg>`. Template CI also generates a separate project and builds/tests it.

MFA, a full session-management UI, migration to Identity, a distributed gateway limit, and organization-specific deployment infrastructure remain product enhancements, not part of the eleven defect fixes. This change does not assert that every possible vulnerability has been eliminated.
