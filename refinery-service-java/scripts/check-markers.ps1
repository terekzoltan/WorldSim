param(
    [Parameter(Mandatory = $true)]
    [string]$ResponsePath,
    [ValidateSet("java_planner_smoke", "full_stack_smoke")]
    [string]$Lane = "java_planner_smoke",
    [string]$ExpectedMode = "both",
    [switch]$RequireLlmCandidate,
    [switch]$AllowFallback
)

if (-not (Test-Path $ResponsePath)) {
    Write-Host "FAIL: response file not found: $ResponsePath" -ForegroundColor Red
    exit 1
}

if ($Lane -eq "full_stack_smoke") {
    Write-Host "FAIL: full_stack_smoke is a manual app/runtime lane; check-markers.ps1 validates Java response markers only." -ForegroundColor Red
    exit 2
}

$response = Get-Content $ResponsePath -Raw | ConvertFrom-Json

if (-not $response.explain) {
    Write-Host "FAIL: response.explain missing" -ForegroundColor Red
    exit 1
}

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
$causalOps = Get-Marker "causalChainOps:"
$causalEq = Get-Marker "causalChainEqPolicy:"

$ok = $true
if ($directorMode -ne "directorOutputMode:$ExpectedMode") { $ok = $false }
if ($RequireLlmCandidate -and $llmStage -ne "llmStage:candidate") { $ok = $false }
if (-not $AllowFallback -and $directorStage -ne "directorStage:refinery-validated") { $ok = $false }
if ($Lane -eq "java_planner_smoke" -and [string]::IsNullOrWhiteSpace($directorStage)) { $ok = $false }
if ([string]::IsNullOrWhiteSpace($causalOps)) { $ok = $false }
if ([string]::IsNullOrWhiteSpace($causalEq)) { $ok = $false }

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
Write-Host "causalOps=$causalOps"
Write-Host "causalEq=$causalEq"

if ($response.warnings -and $response.warnings.Count -gt 0) {
    Write-Host "warnings:"
    $response.warnings | ForEach-Object { Write-Host " - $_" }
}

Write-Host "ops:"
$response.patch | Select-Object op, opId, beatId, colonyId, directive, durationTicks | Format-Table -AutoSize

if ($ok) { exit 0 } else { exit 1 }
