<#
.SYNOPSIS
    Exercises a running stack end to end: health, admin sign-in, tenant and user creation,
    login, user management, todos, error shape, token rotation and reuse detection, logout,
    deactivation, and the welcome email. Uses the admin from Bootstrap:Admin (Development defaults).

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

Step "admin creates the tenant's manager" {
    $r = Invoke-Api POST "$api/tenants/$($state.TenantId)/users" @{ email = $email; firstName = "Smoke"; lastName = "Test"; password = $password; role = 1 } -token $state.Admin
    Assert-Status $r 200
    $state.UserId = ($r.Body | ConvertFrom-Json)
}

Step "duplicate email (different case) is rejected with 409" {
    Assert-Status (Invoke-Api POST "$api/tenants/$($state.TenantId)/users" @{ email = $email.ToUpperInvariant(); firstName = "A"; lastName = "B"; password = $password; role = 0 } -token $state.Admin) 409
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
    $r = Invoke-Api POST "$api/users" @{ email = $memberEmail; firstName = "Smoke"; lastName = "Member"; password = $password; role = 0 } -token $state.Access
    Assert-Status $r 200
    $memberId = $r.Body | ConvertFrom-Json
    $list = Invoke-Api GET "$api/users" -token $state.Access
    Assert-Status $list 200
    if (($list.Body | ConvertFrom-Json).totalCount -ne 2) { throw "expected two users in the tenant" }
    $member = (Invoke-Api POST "$api/users/login" @{ email = $memberEmail; password = $password }).Body | ConvertFrom-Json
    Assert-Status (Invoke-Api GET "$api/users" -token $member.accessToken) 403
    Assert-Status (Invoke-Api PUT "$api/users/$memberId/deactivate" -token $state.Access) 204
    Assert-Status (Invoke-Api GET "$api/users/me" -token $member.accessToken) 403
    Assert-Status (Invoke-Api POST "$api/users/login" @{ email = $memberEmail; password = $password }) 403
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

Step "welcome email is delivered through the outbox (Mailpit)" {
    $deadline = (Get-Date).AddSeconds(30)
    do {
        $messages = Invoke-RestMethod "$MailpitUrl/api/v1/search?query=to:$email" -UseBasicParsing
        if ($messages.messages_count -ge 1) { return }
        Start-Sleep -Seconds 2
    } until ((Get-Date) -gt $deadline)
    throw "no welcome email for $email in Mailpit"
}

Step "API reference (Scalar) is served" {
    Assert-Status (Invoke-Api GET "$BaseUrl/scalar") 200
}

if ($failures -gt 0) {
    Write-Host "`n$failures step(s) failed." -ForegroundColor Red
    exit 1
}

Write-Host "`nAll smoke-test steps passed." -ForegroundColor Green
