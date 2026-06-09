# pack-all.ps1
Write-Host "Empacotando CircuitBreaker..."

# Limpeza
if (Test-Path "local-packages") { Remove-Item -Recurse -Force "local-packages" }
Remove-Item -Recurse -Force "src/CircuitBreaker.Core/bin/Release" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "src/CircuitBreaker.Telemetry/bin/Release" -ErrorAction SilentlyContinue
Remove-Item -Recurse -Force "src/CircuitBreaker.Adaptive/bin/Release" -ErrorAction SilentlyContinue

# Criar pasta de saída
New-Item -ItemType Directory -Path "local-packages" -Force | Out-Null

# Empacotar Core e Telemetry
dotnet pack src/CircuitBreaker.Core -c Release -o local-packages
dotnet pack src/CircuitBreaker.Telemetry -c Release -o local-packages

# Restaurar Adaptive usando a pasta local como fonte adicional
dotnet restore src/CircuitBreaker.Adaptive `
    --source local-packages `
    --source https://api.nuget.org/v3/index.json

# Empacotar Adaptive (--no-restore para usar o restore já feito)
dotnet pack src/CircuitBreaker.Adaptive -c Release -o local-packages --no-restore

Write-Host "`nPacotes gerados com sucesso em ./local-packages"
Get-ChildItem local-packages/*.nupkg | ForEach-Object { Write-Host " - $($_.Name)" }