param(
    [string]$BaseUrl = "http://localhost:8080"
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$pass = 0
$fail = 0

function Assert-True([string]$Label, [bool]$Condition, [string]$Detail = "") {
    if ($Condition) { $script:pass++; Write-Host "  [PASS] $Label" -ForegroundColor Green }
    else { $script:fail++; Write-Host "  [FAIL] $Label  $Detail" -ForegroundColor Red }
}

Write-Host "=== Domain Copilot Docker smoke test ==="

# 0) Health
$health = $null
for ($i = 0; $i -lt 20; $i++) {
    try {
        $health = Invoke-WebRequest -Uri "$BaseUrl/health/ready" -UseBasicParsing -TimeoutSec 5
        if ($health.StatusCode -eq 200) { break }
    } catch { Start-Sleep -Seconds 2 }
}
$healthDetail = if ($null -eq $health) { "no healthy response" } else { $health.Content }
Assert-True "GET /health/ready returns 200" ($null -ne $health -and $health.StatusCode -eq 200) $healthDetail

# 1) Static UI served (no 500 from UseHttpsRedirection in Production)
$pageStatus = 0
$pageDetail = "no response"
try {
    $homePage = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 10
    $pageStatus = [int]$homePage.StatusCode
    $pageDetail = "status=$pageStatus, length=$($homePage.Content.Length)"
    Assert-True "GET / serves the UI (no 500)" ($pageStatus -eq 200) $pageDetail
    Assert-True "UI html detected" ($homePage.Content -match "domain-copilot|copilot|auth|sign in") "no recognizable UI markup"
} catch {
    Assert-True "GET / serves the UI (no 500)" $false "$pageDetail :: $($_.Exception.Message)"
}

# 2) Register a fresh lawyer account over plain HTTP (cookie behavior)
$jar = New-Object System.Net.CookieContainer
$handler = New-Object System.Net.Http.HttpClientHandler
$handler.CookieContainer = $jar
$client = New-Object System.Net.Http.HttpClient($handler)
$client.Timeout = [TimeSpan]::FromSeconds(20)

$email = "smoke$([DateTime]::UtcNow.ToString('HHmmss'))@test.local"
$body = @{ email = $email; password = "Test#Pass2026x"; displayName = "Smoke Tester"; role = "Lawyer" } | ConvertTo-Json

try {
    $reg = $client.PostAsync("$BaseUrl/api/Auth/register", [System.Net.Http.StringContent]::new($body, [System.Text.Encoding]::UTF8, "application/json")).GetAwaiter().GetResult()
    $regBody = $reg.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Assert-True "POST /api/Auth/register succeeds over HTTP (2xx)" ([int]$reg.StatusCode -ge 200 -and [int]$reg.StatusCode -lt 300) "status=$([int]$reg.StatusCode) body=$regBody"

    $cookieHeader = @()
    try { $cookieHeader = @($reg.Headers.GetValues("Set-Cookie")) } catch { }
    if ($cookieHeader.Count -gt 0) {
        $cookieLine = $cookieHeader -join " | "
        $secureFlag = $cookieLine -match ";\s*Secure\b"
        Write-Host "  [cookie] $cookieLine"
        Assert-True "Set-Cookie present" $true $cookieLine
        Write-Host "  [info] Secure flag on cookie: $secureFlag"
    } else {
        Assert-True "Set-Cookie present" $false "no Set-Cookie header on register"
    }

    # 3) Use the session: GET /api/Auth/me
    $me = $client.GetAsync("$BaseUrl/api/Auth/me").GetAwaiter().GetResult()
    $meBody = $me.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    Assert-True "GET /api/Auth/me with session returns 200" ($me.StatusCode -eq [System.Net.HttpStatusCode]::OK) "status=$([int]$me.StatusCode) body=$meBody"
    if ($meBody -match '"email"') { Write-Host "  [info] authenticated as: $($meBody.Substring(0, [Math]::Min(120, $meBody.Length)))" }

    # 4) Protected endpoint as Lawyer
    $probe = $client.GetAsync("$BaseUrl/api/ReviewRuns").GetAwaiter().GetResult()
    Assert-True "GET /api/ReviewRuns as Lawyer returns 200 (session works)" ($probe.StatusCode -eq [System.Net.HttpStatusCode]::OK) "status=$([int]$probe.StatusCode)"
} finally {
    $client.Dispose()
    $handler.Dispose()
}

Write-Host ""
Write-Host "PASS=$pass FAIL=$fail"
if ($fail -gt 0) { Write-Host "SMOKE TEST: FAILED" -ForegroundColor Red; exit 1 }
Write-Host "SMOKE TEST: ALL PASS" -ForegroundColor Green