# Security

## Authentication

- There is no self sign-up. Admins create tenants and users; managers create users in their own tenant (`POST /api/v1/users`, `POST /api/v1/tenants/{tenantId}/users`). Emails are normalized to lower case and globally unique.
- New users are **invited**: they are created without a password, and nobody but the user ever learns one. The user, a single-use invitation token, the audit entry, and the invitation email commit atomically. The emailed link (`ClientApp:BaseUrl` + `/accept-invitation?token=...`) is valid for 3 days; `POST /api/v1/users/invitations/accept` sets the password. Until then the user cannot sign in (same 401 as a wrong password) and `invitationPending` is true. `POST .../users/{userId}/invitation` sends a new link and invalidates the previous one; it fails with `Users.InvitationAlreadyAccepted` once a password exists.
- The first admin comes from the `Bootstrap:Admin` configuration section at start-up: when no admin exists, the platform tenant and the admin are created (audited as `users.admin_bootstrapped`). The step is skipped when the section is absent and fails start-up when it is invalid. It needs the schema, so in production it runs after the migration step; keep the password in user secrets or environment variables and change it after the first sign-in.
- `POST /api/v1/users/login` returns a JWT access token (claims: `sub`, `email`, `tenant_id`, `session_id`; lifetime `Jwt:ExpirationInMinutes`) and a refresh token. Unknown users and wrong passwords get the same 401, and the password is hashed in both cases so timing does not reveal registered emails. A deactivated user, or a user of a deactivated tenant, gets 403 `Users.AccountDisabled` — only after the password was verified.
- Passwords use PBKDF2-SHA512 (500,000 iterations, random salt, constant-time comparison), 8–128 characters.
- **Lockout**: 5 consecutive failed sign-ins lock the account for 15 minutes (`auth.account_locked`, Warning). While locked, sign-in answers 403 `Users.LockedOut` without checking the password, so guessing gains nothing. A successful sign-in resets the counter; a manager or admin can lift the lock early (`PUT .../users/{userId}/unlock`); resetting the password lifts it too. Wrong current passwords on a password change count as failures as well, so a stolen access token cannot be used to guess the password. Attempts on one account hold its row lock (`SELECT ... FOR UPDATE`) until they are recorded, so parallel guesses run one after another and every failure is counted.
- **Lockout trade-off (deliberate):** only existing accounts can be locked, so an attacker who fails 5 times and then sees `Users.LockedOut` learns that the email exists, and can keep a known account locked. The template accepts this in exchange for stopping password guessing: the lock is short, the rate limit slows both, a password reset lifts the lock, and every lock is audited. Replace lockout with, for example, CAPTCHA or progressive delays if enumeration matters more to you.
- **Changing the password** (`PUT /api/v1/users/me/password`, permission `profile:write`) requires the current password, ends every other session of the user (the current one, identified by the `session_id` claim, stays), invalidates outstanding reset links, and emails a notice.
- **Forgotten password**: `POST /api/v1/users/password/forgot` always answers 202, whether or not the email exists, the user is still invited, or the account is disabled — only an active account with a password gets an email. The route never answers in less than one second (`WithMinimumResponseTime`), so the extra work for a real account does not show in the timing. The link (`/reset-password?token=...`) is valid for 1 hour, and each request supersedes the previous link. `POST /api/v1/users/password/reset` sets the new password, lifts any lockout, and revokes **all** sessions.
- **Changing the email address** (`PUT /api/v1/users/me/email`, permission `profile:write`) requires the current password (failures count towards lockout) and answers 202. It changes nothing yet: a single-use link valid for 1 day goes to the **new** address (`/confirm-email?token=...`), and a notice goes to the current one. When the new address already belongs to another account, the answer is the same but no link is sent, so the route does not reveal which addresses are registered. `POST /api/v1/users/email/confirm` (anonymous — the link may be opened anywhere) checks uniqueness again (`Users.EmailNotUnique`, 409), changes the address, and revokes all sessions and outstanding reset links. The audit trail records the request and the change, never the addresses.
- **Emailed tokens** (invitation, password reset, email change) are 256-bit random, URL-safe values. Only their SHA-256 hash is stored (`user_tokens`); they are single-use, expire, and only the newest token of a purpose works. Every reason a token is rejected — unknown, used, superseded, expired, account or tenant disabled — gets the same `Users.InvalidOrExpiredToken`. `ClientApp:BaseUrl` is validated at start-up and must use HTTPS outside Development, since the links carry the tokens.
- The `Jwt` section is validated at start-up (`Secret` ≥ 32 characters). Keep secrets in user secrets or environment variables, never in committed files.

## Sessions (refresh tokens)

- An access token is valid only while its session is: JWT validation requires a live refresh-token family matching the `session_id` claim (`SessionValidator`). Logout, reset and replay revocation therefore reject existing access tokens on the next request with 401 — so clients sign in again — without waiting for JWT expiry.
- Only the SHA-256 hash of a refresh token is stored.
- `POST /api/v1/users/refresh-token` rotates: the presented token is revoked and replaced by a successor in the same **family**.
- Presenting a token that was already **rotated** means it leaked: the whole family is revoked and a `Critical` audit entry is written. A token revoked by logout or by an earlier family revocation is simply rejected — no alarm.
- Two concurrent rotations of the same token serialize on the user row. The second is detected as replay and revokes the family; clients must serialize refresh requests.
- `POST /api/v1/users/logout` (authenticated) revokes the session family of a token that belongs to the caller.
- **Cleanup**: `TokenCleanupWorker` (on by default, hourly) deletes tokens that can never be used again, after `TokenCleanup:RetentionDays` (30). Refresh tokens are deleted by whole family, and only once every token in the family is revoked or expired — a rotated token stays while its family lives, because replaying it is how a leak is detected. Emailed tokens are deleted once used, superseded, or expired. Audit entries are never deleted.
- Deactivating a user, or resetting their password, revokes all of their sessions; changing the password revokes all but the current one. Refreshing fails for a deactivated user or tenant.

## Tenancy

- Every request resolves `ICurrentTenantContext` from the JWT (`tenant_id`, `sub`). It is scoped per request and never cached statically.
- Tenant-owned entities carry `TenantId`. Store methods take the tenant explicitly and filter by it; data of another tenant is reported as `NotFound`, never `Forbidden`, so its existence is not revealed.
- Mutable Todo details are read directly from PostgreSQL so updates are visible across application instances.
- Sign-in and forgot-password lookups by email, and emailed-token lookups by hash, are tenant-agnostic by design (the caller has no tenant yet); everything else is tenant-scoped.
- The one exception is platform administration: admin routes (`/api/v1/tenants/{tenantId}/...`) name the target tenant in the URL. `TenantAccess` accepts another tenant only when the caller is an **active admin** — checked in the use case, not just by the endpoint permission — and otherwise answers `NotFound`, as for any other tenant's data.
- To introduce a tenant hierarchy, extend how `AccessibleTenantIds` is resolved and filter by it in the stores that allow descendant access. A tenant must never be able to grant a permission it does not hold.

## Permissions

- Every endpoint requires a permission (`resource:action`, see `Permissions`) through `.HasPermission(...)`, except the anonymous sign-in routes. An integration test fails when a route has neither.
- Each user has one role, stored on the user. `RolePermissions` maps roles to permissions; each role holds everything the role below it holds:

  | Role | Permissions | Can do |
  |---|---|---|
  | `Member` (0) | `profile:read`, `profile:write`, `todos:*`, `sessions:manage` | Own profile, password, email address, and data |
  | `Manager` (1) | + `users:read`, `users:write` | List, invite (and re-invite), rename, promote/demote, deactivate/activate, unlock users of their own tenant |
  | `Admin` (2) | + `tenants:read`, `tenants:write` | Create, list, deactivate/activate tenants; everything a manager can do, in any tenant |

- `PermissionProvider` reads the role, and whether the user and tenant are active, from the database on **every** request — not from the token — so deactivation and role changes take effect immediately. An inactive user or tenant has no permissions (403).
- The **platform tenant** (`Tenant.IsPlatform`) is the operator's own tenant, created by the bootstrap; it is the home of the admins. The Admin role can be assigned — when inviting or by changing a role — only by an admin, and only to users of the platform tenant (`Users.AdminRoleNotAssignable`). Only admins can manage admin accounts (`Users.AdminNotManageable`). Nobody can change their own role, deactivate, unlock, or re-invite themselves (`Users.CannotChangeOwnAccess`), and an admin cannot deactivate their own tenant (`Tenants.CannotDeactivateOwnTenant`), so the platform cannot be locked out through the API.

## Transport and abuse

- Kestrel limits request bodies to 64 KiB. Authentication strings and Todo labels have explicit bounds; Todos lists default to 50 items and allow at most 100 per page. See [security hardening and upgrade notes](security-hardening.md) for deployment requirements and API changes.
- Rate limiting: a global fixed window per user (or IP), and a stricter policy on authentication routes. The limiter runs before authorization, so forbidden requests consume the limit too.
- Forwarded headers are honoured from loopback proxies only; add your proxy to `KnownProxies`/`KnownNetworks`.
- Races between an up-front check and the insert (for example two user creations with one email) hit a unique index and return 409 `General.UniqueConstraintViolation`, never 500.
- `Correlation-Id` from clients is accepted only if short and made of safe characters; otherwise the trace identifier is used. The id is echoed in the response header and every error body.

## Audit

Audit entries (`audit_entries`) record: id, tenant, user, correlation id, action, entity name and id, old/new values and metadata (JSON), IP address, user agent, timestamp, and severity. They are written in the same unit of work as the change they describe.

Audited today: `users.created`, `users.invitation_resent`, `users.invitation_accepted`, `users.role_changed` (Warning), `users.deactivated` (Warning), `users.activated`, `users.unlocked`, `users.password_changed`, `users.password_reset_requested`, `users.password_reset` (Warning), `users.email_change_requested`, `users.email_changed` (Warning), `users.admin_bootstrapped` (Warning), `auth.account_locked` (Warning), `tenants.created`, `tenants.deactivated` (Warning), `tenants.activated`, `auth.login_succeeded`, `auth.login_failed` (Warning), `auth.refresh_token_rotated`, `auth.refresh_token_reuse_detected` (Critical), `auth.logged_out`. Management actions record the acting user and the tenant that was changed.

The table is append-only: a database trigger rejects `UPDATE`, `DELETE`, and `TRUNCATE`. There is no purge job; adding one requires an approved decision record. Never put passwords, tokens, or other secrets into audit values.
