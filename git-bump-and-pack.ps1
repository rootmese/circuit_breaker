# git-bump-and-pack.ps1
param(
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump = "patch"   # por padrão, incrementa o patch
)

# Função para obter a última tag
function Get-LastTag {
    $tag = git describe --tags --abbrev=0 2>$null
    if (-not $tag) {
        Write-Host "Nenhuma tag encontrada. Criando tag inicial 0.1.0"
        return "0.1.0"
    }
    # Remove 'v' inicial se existir
    return $tag -replace '^v', ''
}

# Função para incrementar a versão
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

# --- Execução principal ---
$currentTag = Get-LastTag
Write-Host "Última tag: $currentTag"

$newVersion = Bump-Version $currentTag $Bump
Write-Host "Nova versão: $newVersion"

# Criar a nova tag no Git
git tag -a "$newVersion" -m "Release $newVersion"
Write-Host "Tag $newVersion criada."

# Atualizar todos os arquivos .csproj dentro de src/
Get-ChildItem -Path "src" -Filter "*.csproj" -Recurse | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    $content = $content -replace '<Version>.*?</Version>', "<Version>$newVersion</Version>"
    Set-Content -Path $_.FullName -Value $content -NoNewline
    Write-Host "Atualizado: $($_.FullName)"
}

# Opcional: dar commit das alterações nos csproj (se quiser)
git add src/**/*.csproj
git commit -m "Bump version to $newVersion" --no-verify 2>$null

# Executar o empacotamento
.\pack-all.ps1

Write-Host "Pronto! Versão $newVersion empacotada e tag criada."