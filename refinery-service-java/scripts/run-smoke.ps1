param(
    [string]$ApiUrl = "http://localhost:8091/v1/patch",
    [string]$RequestPath = "examples/requests/patch-season-director-v1.json",
    [string]$ExpectedMode = "both",
    [switch]$RequireProposedLlm,
    [switch]$AllowFallback,
    [string]$ResponseOut = "scripts/last-response.json"
)

if (-not (Test-Path $RequestPath)) {
    Write-Host "Request fixture not found: $RequestPath" -ForegroundColor Red
    exit 1
}

$body = Get-Content $RequestPath -Raw
$response = Invoke-RestMethod -Method Post -Uri $ApiUrl -ContentType "application/json" -Body $body

$json = $response | ConvertTo-Json -Depth 20
$json | Set-Content $ResponseOut -Encoding UTF8
Write-Host "Saved response to $ResponseOut"

$checker = Join-Path $PSScriptRoot "check-markers.ps1"
& $checker -ResponsePath $ResponseOut -ExpectedMode $ExpectedMode -RequireProposedLlm:$RequireProposedLlm -AllowFallback:$AllowFallback
exit $LASTEXITCODE
