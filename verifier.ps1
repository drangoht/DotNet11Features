# Lance tous les exemples et s'arrête au premier échec.
# Chaque exemple vérifie lui-même ce que la documentation affirme (lignes ✓ / ✗) ;
# son code de sortie est le nombre de vérifications en échec.
#   ./verifier.ps1

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$etapes = @(
    @{ Nom = 'Exemples 01 à 14'; Commande = { dotnet run -c Release --project src/NouveautesDotNet11.Exemples } },
    @{ Nom = 'Exemple 15 (preview)'; Commande = { dotnet run -c Release --project src/NouveautesDotNet11.SecuriteMemoire } },
    @{ Nom = 'Exemple 16 (application en un fichier)'; Commande = { dotnet run appli-fichier/rapport.cs -- Lyon } }
)

foreach ($etape in $etapes) {
    Write-Host "`n═══ $($etape.Nom) ═══" -ForegroundColor Cyan
    & $etape.Commande
    if ($LASTEXITCODE -ne 0) {
        Write-Host "`n$($etape.Nom) : échec (code $LASTEXITCODE)" -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
Write-Host "`nTout est vert." -ForegroundColor Green
