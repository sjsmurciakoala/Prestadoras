# =============================================================================
# Script de publicacion on-prem para APC PROD (Windows Server + IIS)
# Genera carpetas listas para copiar al server.
#
# Uso:
#   .\publish-onprem.ps1                 # publica portal + ws (default)
#   .\publish-onprem.ps1 -Solo portal    # solo portal
#   .\publish-onprem.ps1 -Solo ws        # solo ws
#   .\publish-onprem.ps1 -Output D:\deploy\apc\2026-05-09  # carpeta custom
# =============================================================================

param(
    [ValidateSet("portal", "ws", "todos")]
    [string]$Solo = "todos",
    [string]$Output = ""
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"

if ([string]::IsNullOrWhiteSpace($Output)) {
    $Output = Join-Path $repoRoot "publish_$timestamp"
}

Write-Host "Repo root: $repoRoot"
Write-Host "Output:    $Output"
Write-Host ""

if (-not (Test-Path $Output)) {
    New-Item -ItemType Directory -Path $Output -Force | Out-Null
}

# ----- Portal Blazor (.NET 9) -----
if ($Solo -in @("portal", "todos")) {
    Write-Host "==> Publicando Portal apc.csproj (.NET 9 Release)..." -ForegroundColor Cyan
    $portalOut = Join-Path $Output "portal"
    & dotnet publish "$PSScriptRoot\apc\apc.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained false `
        -p:PublishReadyToRun=true `
        -p:UseAppHost=true `
        -o $portalOut

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falla publicando Portal."
        exit 1
    }
    Write-Host "    Portal publicado en: $portalOut" -ForegroundColor Green
    Write-Host ""
}

# ----- WS WCF (.NET Framework 4.8) -----
if ($Solo -in @("ws", "todos")) {
    Write-Host "==> Publicando WS WS_APC.csproj (.NET Framework 4.8)..." -ForegroundColor Cyan
    $wsOut = Join-Path $Output "ws"
    $wsCsproj = Join-Path $repoRoot "WSappLectores\WS_APC\WS_APC.csproj"

    if (-not (Test-Path $wsCsproj)) {
        Write-Error "No se encuentra WS_APC.csproj en $wsCsproj"
        exit 1
    }

    # MSBuild path (Visual Studio 2022 Community / Pro / BuildTools)
    $msbuildCandidates = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
    )
    $msbuild = $msbuildCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $msbuild) {
        Write-Error "MSBuild 2022 no encontrado. Instala Visual Studio 2022 Build Tools."
        exit 1
    }

    & $msbuild $wsCsproj `
        /p:Configuration=Release `
        /p:DeployOnBuild=true `
        /p:WebPublishMethod=FileSystem `
        /p:DeployDefaultTarget=WebPublish `
        /p:PublishUrl=$wsOut `
        /p:DeleteExistingFiles=True `
        /v:minimal

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falla publicando WS."
        exit 1
    }
    Write-Host "    WS publicado en: $wsOut" -ForegroundColor Green
    Write-Host ""
}

# ----- Resumen + checklist -----
Write-Host ""
Write-Host "===================================================================" -ForegroundColor Yellow
Write-Host " PUBLICACION COMPLETA" -ForegroundColor Yellow
Write-Host "===================================================================" -ForegroundColor Yellow
Write-Host "Carpetas generadas:"
if (Test-Path (Join-Path $Output "portal")) {
    Write-Host "  - $($Output)\portal  -> Copiar a IIS app root del Portal"
}
if (Test-Path (Join-Path $Output "ws")) {
    Write-Host "  - $($Output)\ws      -> Copiar a IIS app root del WS"
}
Write-Host ""
Write-Host "Checklist post-deploy:"
Write-Host "  [ ] iisreset (o recycle del app pool de cada uno)"
Write-Host "  [ ] Verificar Portal:    https://<host>/  -> login OK"
Write-Host "  [ ] Verificar WS:        https://<host>/WS_APC.svc?wsdl  -> WSDL visible"
Write-Host "  [ ] Verificar BD scripts SQL aplicados (ver PLAN_ENTREGA seccion SQL)"
Write-Host "  [ ] Tail logs IIS:       %SystemDrive%\inetpub\logs\LogFiles"
Write-Host ""
