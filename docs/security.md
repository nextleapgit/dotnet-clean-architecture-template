# Security

## Authentication

- There is no self sign-up. Admins create tenants and users; managers create users in their own tenant (`POST /api/v1/users`, `POST /api/v1/tenants/{tenantId}/users`). Emails are normalized to lower case and globally unique; creating a user records an audit entry and queues a welcome email — atomically.
- The first admin comes from the `Bootstrap:Admin` configuration section at start-up: when no admin exists, the platform tenant and the admin are created (audited as `users.admin_bootstrapped`). The step is skipped when the section is absent and fails start-up when it is invalid. It needs the schema, so in production it runs after the migration step; keep the password in user secrets or environment variables and change it after the first sign-in.
- `POST /api/v1/users/login` returns a JWT access token (claims: `sub`, `email`, `tenant_id`; lifetime `Jwt:ExpirationInMinutes`) and a refresh token. Unknown users and wrong passwords get the same 401, and the password is hashed in both cases so timing does not reveal registered emails. A deactivated user, or a user of a deactivated tenant, gets 403 `Users.AccountDisabled` — only after the password was verified.
- Passwords use PBKDF2-SHA512 (500,000 iterations, random salt, constant-time comparison).
- The `Jwt` section is validated at start-up (`Secret` ≥ 32 characters). Keep secrets in user secrets or environment variables, never in committed files.

## Sessions (refresh tokens)

- Only the SHA-256 hash of a refresh token is stored.
- `POST /api/v1/users/refresh-token` rotates: the presented token is revoked and replaced by a successor in the same **family**.
- Presenting a token that was already **rotated** means it leaked: the whole family is revoked and a `Critical` audit entry is written. A token revoked by logout or by an earlier family revocation is simply rejected — no alarm.
- Two concurrent rotations of the same token cannot both succeed (PostgreSQL `xmin` concurrency token → 409).
- `POST /api/v1/users/logout` (authenticated) revokes the session family of a token that belongs to the caller.
- Deactivating a user revokes all of their sessions. Refreshing fails for a deactivated user or tenant.

## Tenancy

- Every request resolves `ICurrentTenantContext` from the JWT (`tenant_id`, `sub`). It is scoped per request and never cached statically.
- Tenant-owned entities carry `TenantId`. Store methods take the tenant explicitly and filter by it; data of another tenant is reported as `NotFound`, never `Forbidden`, so its existence is not revealed.
- Cache keys include the tenant.
- Sign-in lookups by email are tenant-agnostic by design; everything else is tenant-scoped.
- The one exception is platform administration: admin routes (`/api/v1/tenants/{tenantId}/...`) name the target tenant in the URL. `TenantAccess` accepts another tenant only when the caller is an **active admin** — checked in the use case, not just by the endpoint permission — and otherwise answers `NotFound`, as for any other tenant's data.
- To introduce a tenant hierarchy, extend how `AccessibleTenantIds` is resolved and filter by it in the stores that allow descendant access. A tenant must never be able to grant a permission it does not hold.

## Permissions

- Every endpoint requires a permission (`resource:action`, see `Permissions`) through `.HasPermission(...)`, except the anonymous sign-in routes. An integration test fails when a route has neither.
- Each user has one role, stored on the user. `RolePermissions` maps roles to permissions; each role holds everything the role below it holds:

  | Role | Permissions | Can do |
  |---|---|---|
  | `Member` (0) | `profile:read`, `todos:*`, `sessions:manage` | Own profile and data |
  | `Manager` (1) | + `users:read`, `users:write` | List, create, rename, promote/demote, deactivate/activate users of their own tenant |
  | `Admin` (2) | + `tenants:read`, `tenants:write` | Create, list, deactivate/activate tenants; everything a manager can do, in any tenant |

- `PermissionProvider` reads the role, and whether the user and tenant are active, from the database on **every** request — not from the token — so deactivation and role changes take effect immediately. An inactive user or tenant has no permissions (403).
- The Admin role is never assignable through the API (validation error); admins come only from the bootstrap. Admin accounts cannot be managed through user management (`Users.AdminNotManageable`). Nobody can change their own role or deactivate themselves (`Users.CannotChangeOwnAccess`), and an admin cannot deactivate their own tenant (`Tenants.CannotDeactivateOwnTenant`), so the platform cannot be locked out through the API.

## Transport and abuse

- Rate limiting: a global fixed window per user (or IP), and a stricter policy on authentication routes.
- Forwarded headers are honoured from loopback proxies only; add your proxy to `KnownProxies`/`KnownNetworks`.
- Races between an up-front check and the insert (for example two user creations with one email) hit a unique index and return 409 `General.UniqueConstraintViolation`, never 500.
- `Correlation-Id` from clients is accepted only if short and made of safe characters; otherwise the trace identifier is used. The id is echoed in the response header and every error body.

## Audit

Audit entries (`audit_entries`) record: id, tenant, user, correlation id, action, entity name and id, old/new values and metadata (JSON), IP address, user agent, timestamp, and severity. They are written in the same unit of work as the change they describe.

Audited today: `users.created`, `users.role_changed` (Warning), `users.deactivated` (Warning), `users.activated`, `users.admin_bootstrapped` (Warning), `tenants.created`, `tenants.deactivated` (Warning), `tenants.activated`, `auth.login_succeeded`, `auth.login_failed` (Warning), `auth.refresh_token_rotated`, `auth.refresh_token_reuse_detected` (Critical), `auth.logged_out`. Management actions record the acting user and the tenant that was changed.

The table is append-only: a database trigger rejects `UPDATE`, `DELETE`, and `TRUNCATE`. There is no purge job; adding one requires an approved decision record. Never put passwords, tokens, or other secrets into audit values.
