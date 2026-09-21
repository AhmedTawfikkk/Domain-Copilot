param(
    [string]$ApiUrl = "https://localhost:7082/api/Ingest",
    [string]$FolderPath = (Join-Path $PSScriptRoot "..\corpus\raw"),
    [string]$ApiKey = $env:ApiSecurity__LawyerApiKey,
    [switch]$SkipCertificateValidation,
    [ValidateRange(0, [int]::MaxValue)]
    [int]$MaxFiles = 0
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $FolderPath -PathType Container)) {
    throw "Corpus folder was not found: $FolderPath"
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    $environmentFilePath = Join-Path $PSScriptRoot "..\.env"

    if (Test-Path -LiteralPath $environmentFilePath -PathType Leaf) {
        $keyLine = Get-Content -LiteralPath $environmentFilePath |
            Where-Object { $_ -match '^ApiSecurity__LawyerApiKey=' } |
            Select-Object -First 1

        if ($keyLine) {
            $ApiKey = $keyLine.Substring(
                "ApiSecurity__LawyerApiKey=".Length).Trim()
        }
    }
}

if ([string]::IsNullOrWhiteSpace($ApiKey)) {
    throw "ApiKey is required. Pass -ApiKey, set ApiSecurity__LawyerApiKey, or configure it in .env."
}

# Invoke-WebRequest -Form is unavailable in Windows PowerShell 5.1. HttpClient's
# MultipartFormDataContent works in both Windows PowerShell 5.1 and PowerShell 7+.
Add-Type -AssemblyName System.Net.Http

if ($SkipCertificateValidation) {
    if ($null -eq ("LocalDevelopmentCertificateValidator" -as [type])) {
        Add-Type -TypeDefinition @'
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public static class LocalDevelopmentCertificateValidator
{
    public static bool Validate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        return true;
    }
}
'@
    }
    $validatorMethod = [LocalDevelopmentCertificateValidator].GetMethod("Validate")
    $validator = [System.Delegate]::CreateDelegate(
        [System.Net.Security.RemoteCertificateValidationCallback],
        $validatorMethod)
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $validator
}

$handler = [System.Net.Http.HttpClientHandler]::new()
$client = [System.Net.Http.HttpClient]::new($handler)
$client.DefaultRequestHeaders.Add("X-Api-Key", $ApiKey)
$files = Get-ChildItem -LiteralPath $FolderPath -File -Recurse |
    Where-Object { $_.Extension -in ".pdf", ".docx", ".txt" }

if ($MaxFiles -gt 0) {
    $files = @($files | Select-Object -First $MaxFiles)
}

$successCount = 0
$duplicateCount = 0
$failedCount = 0

function Get-ExceptionDetails {
    param([Exception]$Exception)

    $messages = @()
    while ($null -ne $Exception) {
        $messages += $Exception.Message
        $Exception = $Exception.InnerException
    }

    return $messages -join " --> "
}

try {
    foreach ($file in $files) {
        Write-Host "Ingesting: $($file.Name)..." -NoNewline

        $fileStream = $null
        $multipart = $null
        $response = $null

        try {
            $fileStream = [System.IO.File]::OpenRead($file.FullName)
            $multipart = [System.Net.Http.MultipartFormDataContent]::new()
            $fileContent = [System.Net.Http.StreamContent]::new($fileStream)
            $multipart.Add($fileContent, "file", $file.Name)
            $multipart.Add([System.Net.Http.StringContent]::new("Dataset"), "source")

            $response = $client.PostAsync($ApiUrl, $multipart).GetAwaiter().GetResult()
            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $statusCode = [int]$response.StatusCode

            if ($statusCode -eq 409) {
                Write-Host " DUPLICATE" -ForegroundColor Yellow
                $duplicateCount++
            }
            elseif ($response.IsSuccessStatusCode) {
                $result = $responseBody | ConvertFrom-Json
                Write-Host " OK - Status: $($result.status), Chunks: $($result.chunkCount)" -ForegroundColor Green
                $successCount++
            }
            else {
                Write-Host " FAILED - HTTP ${statusCode}: $responseBody" -ForegroundColor Red
                $failedCount++
            }
        }
        catch {
            Write-Host " FAILED - $(Get-ExceptionDetails $_.Exception)" -ForegroundColor Red
            $failedCount++
        }
        finally {
            if ($response) { $response.Dispose() }
            if ($multipart) { $multipart.Dispose() }
            if ($fileStream) { $fileStream.Dispose() }
        }

        Start-Sleep -Milliseconds 150
    }
}
finally {
    $client.Dispose()
    $handler.Dispose()
}

Write-Host "`nDone. Success: $successCount, Duplicates: $duplicateCount, Failed: $failedCount"
