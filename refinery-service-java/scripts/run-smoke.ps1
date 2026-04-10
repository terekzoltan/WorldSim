param(
    [string]$ApiUrl = "http://localhost:8091/v1/patch",
    [string]$RequestPath = "examples/requests/patch-season-director-v1.json",
    [ValidateSet("java_planner_smoke", "full_stack_smoke")]
    [string]$Lane = "java_planner_smoke",
    [string]$ExpectedMode = "both",
    [switch]$RequireLlmCandidate,
    [switch]$AllowFallback,
    [string]$ResponseOut = "scripts/last-response.json"
)

if (-not (Test-Path $RequestPath)) {
    Write-Host "Request fixture not found: $RequestPath" -ForegroundColor Red
    exit 1
}

if ($Lane -eq "full_stack_smoke") {
    Write-Host "FAIL: full_stack_smoke is a manual app/runtime lane; run the app and verify F6/HUD/settings end-to-end." -ForegroundColor Red
    exit 2
}

$body = Get-Content $RequestPath -Raw
$response = Invoke-RestMethod -Method Post -Uri $ApiUrl -ContentType "application/json" -Body $body

$json = $response | ConvertTo-Json -Depth 20
$json | Set-Content $ResponseOut -Encoding UTF8
Write-Host "Saved response to $ResponseOut"

$checker = Join-Path $PSScriptRoot "check-markers.ps1"
& $checker -ResponsePath $ResponseOut -Lane $Lane -ExpectedMode $ExpectedMode -RequireLlmCandidate:$RequireLlmCandidate -AllowFallback:$AllowFallback
exit $LASTEXITCODE
