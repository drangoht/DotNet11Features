# Runs every example and stops at the first failure.
# Each example checks what the documentation claims (✓ / ✗ lines);
# its exit code is the number of failed checks.
#   ./verify.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$steps = @(
    @{ Name = 'Examples 01 to 14'; Command = { dotnet run -c Release --project src/DotNet11Features.Examples } },
    @{ Name = 'Example 15 (preview)'; Command = { dotnet run -c Release --project src/DotNet11Features.MemorySafety } },
    @{ Name = 'Example 16 (file-based app)'; Command = { dotnet run file-based-app/report.cs -- Lyon } }
)

foreach ($step in $steps) {
    Write-Host "`n=== $($step.Name) ===" -ForegroundColor Cyan
    & $step.Command
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n$($step.Name): failed (code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
Write-Host "`nAll green." -ForegroundColor Green
