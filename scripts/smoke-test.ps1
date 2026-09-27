<#
.SYNOPSIS
    Exercises a running stack end to end: health, admin sign-in, tenant and user creation,
    invitation acceptance (link read from Mailpit), login, lockout, password change and reset,
    user management, todos, error shape, token rotation and reuse detection, logout, and deactivation. Uses the admin from Bootstrap:Admin (Development defaults).

.EXAMPLE
    docker compose up -d --build
    ./scripts/smoke-test.ps1

.EXAMPLE
    ./scripts/smoke-test.ps1 -BaseUrl http://localhost:5000 -MailpitUrl http://localhost:8025
#>
param(
    [string] $BaseUrl = "http://localhost:5000",
    [string] $MailpitUrl = "http://localhost:8025",
    [string] $AdminEmail = "admin@cleanarchitecture.local",
    [string] $AdminPassword = "Admin123!",
    [int] $StartupTimeoutSeconds = 90
)

$ErrorActionPreference = "Stop"
$api = "$BaseUrl/api/v1"
$failures = 0

function Step([string] $name, [scriptblock] $check) {
    try {
        & $check
        Write-Host "  PASS  $name" -ForegroundColor Green
    }
    catch {
        $script:failures++
        Write-Host "  FAIL  $name -> $($_.Exception.Message)" -ForegroundColor Red
    }
}

function Invoke-Api([string] $method, [string] $url, $body = $null, [string] $token = $null, [hashtable] $headers = @{}) {
    if ($token) { $headers["Authorization"] = "Bearer $token" }
    $parameters = @{ Method = $method; Uri = $url; Headers = $headers; UseBasicParsing = $true }
    if ($null -ne $body) {
        $parameters.Body = ($body | ConvertTo-Json -Depth 5)
        $parameters.ContentType = "application/json"
    }
    try {
        $response = Invoke-WebRequest @parameters
        return @{ Status = [int] $response.StatusCode; Body = $response.Content; Headers = $response.Headers }
    }
    catch [System.Net.WebException] {
        $errorResponse = $_.Exception.Response
        $reader = New-Object System.IO.StreamReader($errorResponse.GetResponseStream())
        return @{ Status = [int] $errorResponse.StatusCode; Body = $reader.ReadToEnd(); Headers = $errorResponse.Headers }
    }
    catch {
        if ($_.Exception.Response) {
            return @{ Status = [int] $_.Exception.Response.StatusCode; Body = $_.ErrorDetails.Message; Headers = $_.Exception.Response.Headers }
        }
        throw
    }
}

function Assert-Status($response, [int] $expected) {
    if ($response.Status -ne $expected) { throw "expected HTTP $expected but got $($response.Status): $($response.Body)" }
}

# Waits for the newest email with this subject to reach Mailpit through the outbox and returns the token of its link.
function Get-EmailedToken([string] $to, [string] $subject) {
    $query = [uri]::EscapeDataString("to:`"$to`" subject:`"$subject`"")
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $messages = Invoke-RestMethod "$MailpitUrl/api/v1/search?query=$query" -UseBasicParsing
        if ($messages.messages_count -ge 1) {
            $message = Invoke-RestMethod "$MailpitUrl/api/v1/message/$($messages.messages[0].ID)" -UseBasicParsing
            if ($message.Text -match "token=(\S+)") { return $Matches[1] }
        }
        Start-Sleep -Seconds 2
    } until ((Get-Date) -gt $deadline)
    throw "no '$subject' email for $to in Mailpit"
}

# Accepts the emailed invitation of a user, choosing the given password.
function Complete-Invitation([string] $to, [string] $password) {
    $token = Get-EmailedToken $to "You have been invited"
    Assert-Status (Invoke-Api POST "$api/users/invitations/accept" @{ token = $token; password = $password }) 204
}

Write-Host "Waiting for $BaseUrl/health/ready ..."
$deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
do {
    try { $ready = (Invoke-WebRequest "$BaseUrl/health/ready" -UseBasicParsing -TimeoutSec 5).StatusCode -eq 200 } catch { $ready = $false }
    if (-not $ready) { Start-Sleep -Seconds 2 }
} until ($ready -or (Get-Date) -gt $deadline)
if (-not $ready) { throw "The API did not become ready within $StartupTimeoutSeconds seconds." }

$email = "smoke-$([guid]::NewGuid().ToString('N'))@example.com"
$password = "Password123"
Write-Host "Running smoke test as $email"

$state = @{}

Step "admin signs in and creates a tenant" {
    $r = Invoke-Api POST "$api/users/login" @{ email = $AdminEmail; password = $AdminPassword }
    Assert-Status $r 200
    $state.Admin = ($r.Body | ConvertFrom-Json).accessToken
    $r = Invoke-Api POST "$api/tenants" @{ name = "Smoke $([guid]::NewGuid().ToString('N'))" } -token $state.Admin
    Assert-Status $r 200
    $state.TenantId = ($r.Body | ConvertFrom-Json)
}

Step "admin invites the tenant's manager" {
    $r = Invoke-Api POST "$api/tenants/$($state.TenantId)/users" @{ email = $email; firstName = "Smoke"; lastName = "Test"; role = 1 } -token $state.Admin
    Assert-Status $r 200
    $state.UserId = ($r.Body | ConvertFrom-Json)
}

Step "duplicate email (different case) is rejected with 409" {
    Assert-Status (Invoke-Api POST "$api/tenants/$($state.TenantId)/users" @{ email = $email.ToUpperInvariant(); firstName = "A"; lastName = "B"; role = 0 } -token $state.Admin) 409
}

Step "an invited user cannot sign in before accepting the invitation" {
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $email; password = $password }) 401
}

Step "the invitation email is delivered (Mailpit) and accepting it sets the password, once" {
    $token = Get-EmailedToken $email "You have been invited"
    Assert-Status (Invoke-Api POST "$api/users/invitations/accept" @{ token = $token; password = $password }) 204
    Assert-Status (Invoke-Api POST "$api/users/invitations/accept" @{ token = $token; password = "Another123" }) 400
}

Step "self sign-up does not exist" {
    Assert-Status (Invoke-Api POST "$api/users/register" @{ email = "x-$email"; firstName = "A"; lastName = "B"; password = $password }) 405
}

Step "wrong password is rejected with 401" {
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $email; password = "WrongPassword1" }) 401
}

Step "login returns access and refresh tokens" {
    $r = Invoke-Api POST "$api/users/login" @{ email = $email; password = $password }
    Assert-Status $r 200
    $tokens = $r.Body | ConvertFrom-Json
    $state.Access = $tokens.accessToken
    $state.Refresh = $tokens.refreshToken
}

Step "protected route without a token is 401" {
    Assert-Status (Invoke-Api GET "$api/todos") 401
}

Step "current user is readable within its tenant" {
    $r = Invoke-Api GET "$api/users/$($state.UserId)" -token $state.Access
    Assert-Status $r 200
    if (($r.Body | ConvertFrom-Json).tenantId -eq [guid]::Empty) { throw "missing tenant id" }
}

Step "manager creates a member, lists the tenant's users, and deactivates the member" {
    $memberEmail = "member-$email"
    $r = Invoke-Api POST "$api/users" @{ email = $memberEmail; firstName = "Smoke"; lastName = "Member"; role = 0 } -token $state.Access
    Assert-Status $r 200
    $memberId = $r.Body | ConvertFrom-Json
    Complete-Invitation $memberEmail $password
    $list = Invoke-Api GET "$api/users" -token $state.Access
    Assert-Status $list 200
    if (($list.Body | ConvertFrom-Json).totalCount -ne 2) { throw "expected two users in the tenant" }
    $member = (Invoke-Api POST "$api/users/login" @{ email = $memberEmail; password = $password }).Body | ConvertFrom-Json
    Assert-Status (Invoke-Api GET "$api/users" -token $member.accessToken) 403
    Assert-Status (Invoke-Api PUT "$api/users/$memberId/deactivate" -token $state.Access) 204
    Assert-Status (Invoke-Api GET "$api/users/me" -token $member.accessToken) 403
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $memberEmail; password = $password }) 403
}

Step "repeated wrong passwords lock the account until a manager unlocks it" {
    $lockedEmail = "locked-$email"
    $r = Invoke-Api POST "$api/users" @{ email = $lockedEmail; firstName = "Smoke"; lastName = "Locked"; role = 0 } -token $state.Access
    Assert-Status $r 200
    $lockedId = $r.Body | ConvertFrom-Json
    Complete-Invitation $lockedEmail $password
    1..5 | ForEach-Object { Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $lockedEmail; password = "WrongPassword1" }) 401 }
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $lockedEmail; password = $password }) 403
    Assert-Status (Invoke-Api PUT "$api/users/$lockedId/unlock" -token $state.Access) 204
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $lockedEmail; password = $password }) 200
}

Step "manager cannot administer tenants" {
    Assert-Status (Invoke-Api GET "$api/tenants" -token $state.Access) 403
}

Step "create, list, and complete a todo" {
    $r = Invoke-Api POST "$api/todos" @{ description = "Smoke test todo"; labels = @("smoke"); priority = 2 } -token $state.Access
    Assert-Status $r 200
    $todoId = $r.Body | ConvertFrom-Json
    $list = Invoke-Api GET "$api/todos" -token $state.Access
    Assert-Status $list 200
    if (@($list.Body | ConvertFrom-Json).Count -ne 1) { throw "expected exactly one todo" }
    Assert-Status (Invoke-Api PUT "$api/todos/$todoId/complete" -token $state.Access) 204
}

Step "errors carry a stable code and the correlation id" {
    $r = Invoke-Api GET "$api/todos/$([guid]::NewGuid())" -token $state.Access -headers @{ "Correlation-Id" = "smoke-correlation" }
    Assert-Status $r 404
    $problem = $r.Body | ConvertFrom-Json
    if ($problem.code -ne "TodoItems.NotFound" -or $problem.correlationId -ne "smoke-correlation") { throw "unexpected body: $($r.Body)" }
}

Step "refresh rotates the token" {
    $r = Invoke-Api POST "$api/users/refresh-token" @{ refreshToken = $state.Refresh }
    Assert-Status $r 200
    $state.Rotated = ($r.Body | ConvertFrom-Json).refreshToken
}

Step "replaying a rotated token is rejected and revokes the family" {
    Assert-Status (Invoke-Api POST "$api/users/refresh-token" @{ refreshToken = $state.Refresh }) 400
    Assert-Status (Invoke-Api POST "$api/users/refresh-token" @{ refreshToken = $state.Rotated }) 400
}

Step "logout revokes a fresh session" {
    $login = (Invoke-Api POST "$api/users/login" @{ email = $email; password = $password }).Body | ConvertFrom-Json
    Assert-Status (Invoke-Api POST "$api/users/logout" @{ refreshToken = $login.refreshToken } -token $login.accessToken) 204
    Assert-Status (Invoke-Api POST "$api/users/refresh-token" @{ refreshToken = $login.refreshToken }) 400
}

Step "changing the password requires the current one and ends the other sessions" {
    $other = (Invoke-Api POST "$api/users/login" @{ email = $email; password = $password }).Body | ConvertFrom-Json
    $current = (Invoke-Api POST "$api/users/login" @{ email = $email; password = $password }).Body | ConvertFrom-Json
    Assert-Status (Invoke-Api PUT "$api/users/me/password" @{ currentPassword = "WrongPassword1"; newPassword = "Changed123" } -token $current.accessToken) 400
    Assert-Status (Invoke-Api PUT "$api/users/me/password" @{ currentPassword = $password; newPassword = "Changed123" } -token $current.accessToken) 204
    Assert-Status (Invoke-Api POST "$api/users/refresh-token" @{ refreshToken = $other.refreshToken }) 400
    Assert-Status (Invoke-Api POST "$api/users/refresh-token" @{ refreshToken = $current.refreshToken }) 200
    $script:password = "Changed123"
}

Step "forgot password answers 202 for unknown emails too; the emailed link resets the password once" {
    Assert-Status (Invoke-Api POST "$api/users/password/forgot" @{ email = "nobody-$email" }) 202
    Assert-Status (Invoke-Api POST "$api/users/password/forgot" @{ email = $email }) 202
    $token = Get-EmailedToken $email "Reset your password"
    Assert-Status (Invoke-Api POST "$api/users/password/reset" @{ token = $token; newPassword = "Reset12345" }) 204
    Assert-Status (Invoke-Api POST "$api/users/password/reset" @{ token = $token; newPassword = "Again12345" }) 400
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $email; password = "Reset12345" }) 200
}

Step "API reference (Scalar) is served" {
    Assert-Status (Invoke-Api GET "$BaseUrl/scalar") 200
}

if ($failures -gt 0) {
    Write-Host "`n$failures step(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "`nAll smoke-test steps passed." -ForegroundColor Green
