<#
    publicar.ps1

    Gera o aplicativo pronto para distribuir.

    Modos:
      (padrao)      executavel autonomo: nao exige .NET instalado (~160 MB)
      -ComRuntime   exige .NET 10 Desktop Runtime na maquina (~8 MB)

    Resultado em:  dist\UltimateMediaDownloader\
                   dist\UltimateMediaDownloader-1.0.0-portable.zip

    Uso:
        powershell -ExecutionPolicy Bypass -File publicar.ps1
        powershell -ExecutionPolicy Bypass -File publicar.ps1 -ComRuntime
#>

param(
    [switch]$ComRuntime,
    [string]$Versao = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$root = Get-Location

if (-not (Test-Path (Join-Path $root 'UltimateMediaDownloader.sln'))) {
    Write-Host "ERRO: rode este script na pasta que contem UltimateMediaDownloader.sln" -ForegroundColor Red
    exit 1
}

$dist    = Join-Path $root 'dist'
$saida   = Join-Path $dist 'UltimateMediaDownloader'
$projeto = Join-Path $root 'src\UMD.App\UMD.App.csproj'

Write-Host "=== Publicando Ultimate Media Downloader $Versao ===`n" -ForegroundColor Cyan

# --- 1. Conferir os binarios externos --------------------------------------

$toolsOrigem = Join-Path $root 'tools'
$obrigatorios = @('yt-dlp.exe', 'ffmpeg.exe', 'deno.exe')
$faltando = @()

foreach ($t in $obrigatorios) {
    if (-not (Test-Path (Join-Path $toolsOrigem $t))) { $faltando += $t }
}

if ($faltando.Count -gt 0) {
    Write-Host "ERRO: faltam binarios em tools\: $($faltando -join ', ')" -ForegroundColor Red
    Write-Host "Rode primeiro: powershell -ExecutionPolicy Bypass -File tools\get-tools.ps1" -ForegroundColor Yellow
    exit 1
}

Write-Host "Binarios externos: OK" -ForegroundColor Green

# --- 2. Limpar a saida anterior --------------------------------------------

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }
New-Item -ItemType Directory -Path $saida -Force | Out-Null

# --- 3. Publicar -----------------------------------------------------------

$autonomo = -not $ComRuntime

Write-Host "`nModo: $(if ($autonomo) { 'autonomo (nao exige .NET instalado)' } else { 'exige .NET 10 Desktop Runtime' })" -ForegroundColor Cyan
Write-Host "Compilando..." -ForegroundColor Cyan

$argumentos = @(
    'publish', $projeto,
    '-c', 'Release',
    '-r', 'win-x64',
    '--self-contained', $autonomo.ToString().ToLower(),
    '-o', $saida,
    "-p:Version=$Versao",
    '-p:PublishSingleFile=true',
    '--nologo'
)

& dotnet @argumentos

if ($LASTEXITCODE -ne 0) {
    Write-Host "`nA publicacao falhou." -ForegroundColor Red
    exit 1
}

# --- 4. Copiar os binarios externos ----------------------------------------

# O alvo CopyExternalTools do csproj roda depois de Build, que grava em bin\.
# A publicacao usa outra pasta, entao a copia precisa ser refeita aqui.
$toolsDestino = Join-Path $saida 'tools'
New-Item -ItemType Directory -Path $toolsDestino -Force | Out-Null
Copy-Item (Join-Path $toolsOrigem '*.exe') $toolsDestino -Force

Write-Host "`nFerramentas copiadas para tools\" -ForegroundColor Green

# --- 5. Arquivos legais ----------------------------------------------------

foreach ($arquivo in @('LICENSE', 'NOTICE')) {
    if (Test-Path (Join-Path $root $arquivo)) {
        Copy-Item (Join-Path $root $arquivo) $saida -Force
    }
}

# Remove restos de compilacao que nao devem ser distribuidos.
Get-ChildItem $saida -Filter '*.pdb' -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

# --- 6. Resumo -------------------------------------------------------------

Write-Host "`n--- Conteudo ---" -ForegroundColor Cyan

$total = 0
Get-ChildItem $saida -Recurse -File | ForEach-Object { $total += $_.Length }

Get-ChildItem $saida -File | Sort-Object Length -Descending | ForEach-Object {
    Write-Host ("  {0,-42} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB))
}
Get-ChildItem $toolsDestino -File | ForEach-Object {
    Write-Host ("  tools\{0,-36} {1,8:N1} MB" -f $_.Name, ($_.Length / 1MB))
}

Write-Host ("`n  TOTAL: {0:N1} MB" -f ($total / 1MB)) -ForegroundColor Yellow

# --- 7. Pacote portatil ----------------------------------------------------

$zip = Join-Path $dist "UltimateMediaDownloader-$Versao-portable.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }

Write-Host "`nCompactando (pode demorar)..." -ForegroundColor Cyan
Compress-Archive -Path "$saida\*" -DestinationPath $zip -CompressionLevel Optimal

$zipMb = (Get-Item $zip).Length / 1MB
Write-Host ("  {0}  ({1:N1} MB)" -f (Split-Path $zip -Leaf), $zipMb) -ForegroundColor Green

# --- 8. Instalador, se o Inno Setup estiver disponivel ---------------------

$iss = Join-Path $root 'instalador.iss'
$iscc = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc -and (Test-Path $iss)) {
    Write-Host "`nGerando instalador com Inno Setup..." -ForegroundColor Cyan
    & $iscc "/DMyAppVersion=$Versao" $iss

    if ($LASTEXITCODE -eq 0) {
        Get-ChildItem $dist -Filter '*setup*.exe' | ForEach-Object {
            Write-Host ("  {0}  ({1:N1} MB)" -f $_.Name, ($_.Length / 1MB)) -ForegroundColor Green
        }
    } else {
        Write-Warning "O Inno Setup retornou erro."
    }
} elseif (Test-Path $iss) {
    Write-Host "`nInno Setup nao encontrado. Para gerar o instalador:" -ForegroundColor Yellow
    Write-Host "  1. Instale de https://jrsoftware.org/isdl.php"
    Write-Host "  2. Rode este script novamente"
    Write-Host "`nO pacote portatil (zip) ja esta pronto e funciona sem instalador." -ForegroundColor DarkGray
}

Write-Host "`n=== Pronto ===" -ForegroundColor Green
Write-Host "Aplicativo: $saida\UltimateMediaDownloader.exe"
Write-Host "Portatil:   $zip`n"