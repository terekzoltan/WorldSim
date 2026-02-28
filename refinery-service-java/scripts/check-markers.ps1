param(
    [Parameter(Mandatory = $true)]
    [string]$ResponsePath,
    [string]$ExpectedMode = "both",
    [switch]$RequireProposedLlm,
    [switch]$AllowFallback
)

if (-not (Test-Path $ResponsePath)) {
    Write-Host "FAIL: response file not found: $ResponsePath" -ForegroundColor Red
    exit 1
}

$response = Get-Content $ResponsePath -Raw | ConvertFrom-Json

function Get-Marker([string]$prefix) {
    return $response.explain | Where-Object { $_ -like "$prefix*" } | Select-Object -First 1
}

$refineryStage = Get-Marker "refineryStage:"
$directorStage = Get-Marker "directorStage:"
$directorMode = Get-Marker "directorOutputMode:"
$llmStage = Get-Marker "llmStage:"
$llmRetries = Get-Marker "llmRetries:"
$llmFallback = Get-Marker "llmFallbackReason:"
$rawCaptured = Get-Marker "llmRawCaptured:"

$ok = $true
if ($refineryStage -ne "refineryStage:enabled") { $ok = $false }
if ($directorMode -ne "directorOutputMode:$ExpectedMode") { $ok = $false }
if ($RequireProposedLlm -and $llmStage -ne "llmStage:proposed") { $ok = $false }
if (-not $AllowFallback -and $directorStage -ne "directorStage:refinery-validated") { $ok = $false }

if ($ok) {
    Write-Host "PASS" -ForegroundColor Green
} else {
    Write-Host "FAIL" -ForegroundColor Red
}

Write-Host "refineryStage=$refineryStage"
Write-Host "directorStage=$directorStage"
Write-Host "directorMode=$directorMode"
Write-Host "llmStage=$llmStage"
Write-Host "llmRetries=$llmRetries"
Write-Host "llmFallback=$llmFallback"
Write-Host "llmRawCaptured=$rawCaptured"

if ($response.warnings.Count -gt 0) {
    Write-Host "warnings:"
    $response.warnings | ForEach-Object { Write-Host " - $_" }
}

Write-Host "ops:"
$response.patch | Select-Object op, opId, beatId, colonyId, directive, durationTicks | Format-Table -AutoSize

if ($ok) { exit 0 } else { exit 1 }
