# git-bump-and-pack.ps1
param(
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump = "patch"
)

function Get-LastTag {
    $tag = git describe --tags --abbrev=0 2>$null
    if (-not $tag) {
        Write-Host "Nenhuma tag encontrada. Criando tag inicial 0.1.0"
        return "0.1.0"
    }
    return $tag -replace '^v', ''
}

function Bump-Version {
    param([string]$Version, [string]$Level)
    $parts = $Version -split '\.'
    if ($parts.Count -lt 3) { $parts += @(0) * (3 - $parts.Count) }
    $major = [int]$parts[0]
    $minor = [int]$parts[1]
    $patch = [int]$parts[2]

    switch ($Level) {
        "major" { $major++; $minor = 0; $patch = 0 }
        "minor" { $minor++; $patch = 0 }
        "patch" { $patch++ }
    }
    return "$major.$minor.$patch"
}

$currentTag = Get-LastTag
Write-Host "Última tag: $currentTag"

$newVersion = Bump-Version $currentTag $Bump
Write-Host "Nova versão: $newVersion"

git tag -a "$newVersion" -m "Release $newVersion"
Write-Host "Tag $newVersion criada."

$packableProjects = @(
    "src/CircuitBreaker.Core/CircuitBreaker.Core.csproj",
    "src/CircuitBreaker.Telemetry/CircuitBreaker.Telemetry.csproj",
    "src/CircuitBreaker.Adaptive/CircuitBreaker.Adaptive.csproj"
)

foreach ($project in $packableProjects) {
    $content = Get-Content $project -Raw
    $content = $content -replace '<Version>.*?</Version>', "<Version>$newVersion</Version>"
    Set-Content -Path $project -Value $content -NoNewline
    Write-Host "Atualizado: $project"
}

git add $packableProjects
git commit -m "Bump version to $newVersion" --no-verify 2>$null

.\pack-all.ps1

Write-Host "Pronto! Versão $newVersion empacotada e tag criada."
