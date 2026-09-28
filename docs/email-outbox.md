# Email Outbox

Use cases never send email. They enqueue it with `IEmailOutbox.EnqueueAsync` inside the same database transaction as the state change, and a background worker sends it later. Either both the change and the email commit, or neither does.

## Using it in a use case

```csharp
Result<EmailMessage> email = EmailMessage.Create(Guid.CreateVersion7(), recipient, subject, text, html);
// HTML-encode every untrusted value in `html` before this point.

await using IUnitOfWorkTransaction transaction = await unitOfWork.BeginTransactionAsync(ct);
// domain write...
await unitOfWork.SaveChangesAsync(ct);
await emailOutbox.EnqueueAsync(email.Value, expiresAtUtc, ct); // bound by any link/token validity
await transaction.CommitAsync(ct);
```

`EnqueueAsync` throws when no transaction is active on the current unit of work, and a duplicate logical id fails on the primary key instead of overwriting a queued email. `EmailMessage.Create` rejects invalid recipients, multi-line subjects (header injection), control characters, and bodies over 256 KB.

## Storage contract (`email_outbox_messages`)

| Column | Meaning |
|---|---|
| `id` | Logical email id (PK), reused across retries and sent as the `Message-ID` |
| `status` | `0 Pending`, `1 Processing`, `2 Sent`, `3 Failed`, `4 Expired` — a persistent contract |
| `payload` | Encrypted recipient, subject, and bodies; present only while Pending/Processing |
| `created_at_utc`, `next_attempt_at_utc` | Set by the database clock |
| `expires_at_utc` | After this the email is settled as Expired |
| `attempt_count`, `lease_id`, `lease_expires_at_utc` | Claim bookkeeping; the lease exists only while Processing |
| `processed_at_utc`, `error_code` | Set on terminal states; the error code is always one of `EmailErrorCodes` |

Check constraints enforce valid statuses, non-negative attempts, payload presence only while active, completion time only when terminal, and lease presence only while Processing. Partial indexes serve due-pending scheduling, expired-lease recovery, expiry, and cleanup.

Payloads are protected with ASP.NET Core Data Protection. The protection purpose is bound to the id and the expiry, so moving a payload or extending its expiry makes it unreadable. Keys are stored in the database (`data_protection_keys`) so every instance shares one key ring. Keys are encrypted with an RSA certificate whose private key stays outside PostgreSQL. Startup also wraps legacy plaintext keys without changing their IDs. Production requires certificate configuration; see [upgrade notes](security-hardening.md).

## Worker

- Disabled by default (`EmailOutbox:Enabled = false`): emails are still queued, just not sent. Enabled in `Development` against Mailpit.
- Each pass runs in fresh scopes: **cleanup → settlement → dispatch one message**. Messages are sent one at a time per instance.
- Instances coordinate only through the database: a row is claimed with `FOR UPDATE SKIP LOCKED`, moved to Processing with a fresh lease and an incremented attempt count, and its outcome is recorded with a conditional update that re-checks id, lease, and status. No transaction or row lock is held during the SMTP call.
- An abandoned Processing row becomes claimable again when its lease expires. Settlement moves expired rows to Expired and attempt-exhausted rows to Failed, in bounded batches. Terminal rows are deleted after `RetentionDays`.
- Retries use capped exponential backoff with jitter (`RetryBaseDelaySeconds` … `RetryMaxDelaySeconds`).
- Options are validated at start-up even when disabled; changes need a restart. The lease must cover `Smtp:TimeoutSeconds + CompletionTimeoutSeconds + 5`.

## SMTP delivery

- MailKit, one attempt per call. `Smtp:SecurityMode` must be `StartTls` or `SslOnConnect` outside `Development`. Credentials (both user name and password, or neither) belong in user secrets or a secret store.
- Failures map to fixed codes only: `smtp_temporary_failure`, `smtp_connection_failure`, `smtp_timeout`, `smtp_permanent_failure` (5xx), `payload_protection_failure`, `delivery_window_elapsed`, `attempts_exhausted`. Recipients, subjects, bodies, and raw SMTP or exception text are never logged or stored.

## Health

The `email-outbox` check reports **Degraded** when pending work or an expired lease has waited longer than `HealthBacklogThresholdSeconds`, including expired or exhausted pending messages. It also reports Degraded when at least `HealthFailureThreshold` (5) emails failed or expired within the last `HealthFailureWindowMinutes` (60) — a single bad address is not an outage, and the state clears once the window passes. The check remains active when this instance's worker is disabled because the queue is shared. It is part of `/health` but not `/health/ready`: an email backlog must not take an instance out of rotation. Monitor the health status in the response; Degraded may still have HTTP status 200.

## Delivery semantics

`Sent` means the SMTP server accepted the message, not that it reached an inbox. Delivery is at-least-once: a crash after acceptance can resend after lease recovery. The stable `Message-ID` helps correlate duplicates but does not deduplicate. The outbox is not an audit ledger — its rows are deleted after the retention period.

## Tests

`CleanArchitecture.IntegrationTests/Email` covers transactional enqueue and rollback, payload protection (non-ASCII round trip, tamper detection), duplicate ids, row locking from a second connection, lease ownership, lease recovery, dispatch outcomes, settlement, cleanup, check constraints, the health check, the running worker, and real SMTP delivery to Mailpit.
