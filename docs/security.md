# Security

## Authentication

- `POST /api/v1/users/register` creates a tenant and its first user (email normalized to lower case, globally unique), records an audit entry, and queues a welcome email — atomically.
- `POST /api/v1/users/login` returns a JWT access token (claims: `sub`, `email`, `tenant_id`; lifetime `Jwt:ExpirationInMinutes`) and a refresh token. Unknown users and wrong passwords get the same 401, and the password is hashed in both cases so timing does not reveal registered emails.
- Passwords use PBKDF2-SHA512 (500,000 iterations, random salt, constant-time comparison).
- The `Jwt` section is validated at start-up (`Secret` ≥ 32 characters). Keep secrets in user secrets or environment variables, never in committed files.

## Sessions (refresh tokens)

- Only the SHA-256 hash of a refresh token is stored.
- `POST /api/v1/users/refresh-token` rotates: the presented token is revoked and replaced by a successor in the same **family**.
- Presenting a token that was already **rotated** means it leaked: the whole family is revoked and a `Critical` audit entry is written. A token revoked by logout or by an earlier family revocation is simply rejected — no alarm.
- Two concurrent rotations of the same token cannot both succeed (PostgreSQL `xmin` concurrency token → 409).
- `POST /api/v1/users/logout` (authenticated) revokes the session family of a token that belongs to the caller.

## Tenancy

- Every request resolves `ICurrentTenantContext` from the JWT (`tenant_id`, `sub`). It is scoped per request and never cached statically.
- Tenant-owned entities carry `TenantId`. Store methods take the tenant explicitly and filter by it; data of another tenant is reported as `NotFound`, never `Forbidden`, so its existence is not revealed.
- Cache keys include the tenant.
- Sign-in lookups by email are tenant-agnostic by design; everything else is tenant-scoped.
- To introduce a tenant hierarchy, extend how `AccessibleTenantIds` is resolved and filter by it in the stores that allow descendant access. A tenant must never be able to grant a permission it does not hold.

## Permissions

- Every endpoint requires a permission (`resource:action`, see `Permissions`) through `.HasPermission(...)`, except the anonymous sign-in routes. An integration test fails when a route has neither.
- `PermissionProvider` currently grants a default set to every authenticated user. Replace it with a persisted role/permission model when a product needs one.

## Transport and abuse

- Rate limiting: a global fixed window per user (or IP), and a stricter policy on authentication routes.
- Forwarded headers are honoured from loopback proxies only; add your proxy to `KnownProxies`/`KnownNetworks`.
- Races between an up-front check and the insert (for example two sign-ups with one email) hit a unique index and return 409 `General.UniqueConstraintViolation`, never 500.
- `Correlation-Id` from clients is accepted only if short and made of safe characters; otherwise the trace identifier is used. The id is echoed in the response header and every error body.

## Audit

Audit entries (`audit_entries`) record: id, tenant, user, correlation id, action, entity name and id, old/new values and metadata (JSON), IP address, user agent, timestamp, and severity. They are written in the same unit of work as the change they describe.

Audited today: `users.registered`, `auth.login_succeeded`, `auth.login_failed` (Warning), `auth.refresh_token_rotated`, `auth.refresh_token_reuse_detected` (Critical), `auth.logged_out`.

The table is append-only: a database trigger rejects `UPDATE`, `DELETE`, and `TRUNCATE`. There is no purge job; adding one requires an approved decision record. Never put passwords, tokens, or other secrets into audit values.
