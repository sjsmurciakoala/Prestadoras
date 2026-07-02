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

    # Restore primero sin RID: -r win-x64 se propaga a apc.Client (WASM browser-wasm)
    # y rompe el restore. Restore aqui sin RID, publish despues con --no-restore.
    Write-Host "    Restoring (sin RID)..." -ForegroundColor DarkGray
    & dotnet restore "$PSScriptRoot\apc\apc.csproj"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Falla en restore del Portal."
        exit 1
    }

    # UseAppHost: NO pasar como -p (se propaga a apc.Client WASM y rompe NETSDK1084).
    # apc.csproj (SDK web) genera apc.exe por default; WASM ya tiene UseAppHost=false.
    & dotnet publish "$PSScriptRoot\apc\apc.csproj" `
        -c Release `
        -r win-x64 `
        --self-contained false `
        --no-restore `
        -p:PublishReadyToRun=true `
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

    # MSBuild discovery: primero via vswhere (cualquier VS 2022+), luego paths conocidos.
    $msbuild = $null
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (Test-Path $vswhere) {
        $msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild `
                       -find "MSBuild\**\Bin\MSBuild.exe" 2>$null | Select-Object -First 1
    }
    if (-not $msbuild -or -not (Test-Path $msbuild)) {
        $msbuildCandidates = @(
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe",
            "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
        )
        $msbuild = $msbuildCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }

    if (-not $msbuild) {
        Write-Error "MSBuild no encontrado. Instala Visual Studio 2022+ con Microsoft.Component.MSBuild."
        exit 1
    }
    Write-Host "    MSBuild: $msbuild" -ForegroundColor DarkGray

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
