# pack-all.ps1
Write-Host "Empacotando CircuitBreaker..."

if (Test-Path "local-packages") { Remove-Item -Recurse -Force "local-packages" }
New-Item -ItemType Directory -Path "local-packages" -Force | Out-Null

$projects = @(
    "src/CircuitBreaker.Core",
    "src/CircuitBreaker.Telemetry",
    "src/CircuitBreaker.Adaptive",
    "src/CircuitBreaker.AspNetCore.Extensions"
)

foreach ($project in $projects) {
    dotnet pack $project -c Release -o local-packages
    if ($LASTEXITCODE -ne 0) {
        throw "Falha ao empacotar $project"
    }
}

Write-Host "`nPacotes gerados com sucesso em ./local-packages"
Get-ChildItem local-packages/*.nupkg | ForEach-Object { Write-Host " - $($_.Name)" }
