# ============================================================
# BACKUP COMPLETO DE BASE DE DATOS (no interactivo)
# ------------------------------------------------------------
# Genera un .backup (formato custom de pg_dump, comprimido) listo
# para compartir y restaurar con pg_restore.
#
# Uso típico:
#   .\backup_completo.ps1                      # BD local de trabajo (siad_v3_test)
#   .\backup_completo.ps1 -Origen prod         # producción 172.16.0.9 (SOLO LECTURA)
#   .\backup_completo.ps1 -DbHost otro -Db x -Password y   # cualquier servidor
#
# Restaurar en otra máquina (ejemplo):
#   createdb -h localhost -U postgres siad_v3_test
#   pg_restore -h localhost -U postgres -d siad_v3_test --no-owner --no-privileges <archivo>.backup
#   ...y luego aplicar los scripts de Database/ddl_v3 que falten
#   (ver docs/handoff-integracion-contable-2026-07.md §3 para el orden F1-F8).
# ============================================================

param(
    # Atajo: 'local' = siad_v3_test en localhost, 'prod' = siad_v3 en 172.16.0.9
    [ValidateSet('local', 'prod', 'custom')]
    [string]$Origen = 'local',

    [string]$DbHost = $null,
    [int]$Port = 5432,
    [string]$Db = $null,
    [string]$Usuario = 'postgres',
    [string]$Password = 'root',

    # Carpeta de salida (por defecto Database\Backups junto a este script)
    [string]$Salida = $null
)

$ErrorActionPreference = 'Stop'

switch ($Origen) {
    'local' { if (-not $DbHost) { $DbHost = 'localhost' };  if (-not $Db) { $Db = 'siad_v3_test' } }
    'prod'  { if (-not $DbHost) { $DbHost = '172.16.0.9' }; if (-not $Db) { $Db = 'siad_v3' } }
    'custom' {
        if (-not $DbHost -or -not $Db) {
            Write-Host "Con -Origen custom debes indicar -DbHost y -Db." -ForegroundColor Red
            exit 1
        }
    }
}

if (-not $Salida) { $Salida = Join-Path $PSScriptRoot 'Backups' }
if (-not (Test-Path $Salida)) { New-Item -ItemType Directory -Force $Salida | Out-Null }

$pgDump = Get-Command pg_dump -ErrorAction SilentlyContinue
if (-not $pgDump) {
    Write-Host "ERROR: pg_dump no esta en PATH (instala PostgreSQL client tools)." -ForegroundColor Red
    exit 1
}

$timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$archivo = Join-Path $Salida ("{0}_{1}_{2}.backup" -f $Db, $DbHost.Replace('.', '-'), $timestamp)

Write-Host "Backup de $Db en ${DbHost}:$Port -> $archivo" -ForegroundColor Cyan

$env:PGPASSWORD = $Password
try {
    & pg_dump --host=$DbHost --port=$Port --username=$Usuario --dbname=$Db `
              --format=custom --blobs --file="$archivo"
    if ($LASTEXITCODE -ne 0) { throw "pg_dump devolvio codigo $LASTEXITCODE" }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

$mb = [math]::Round((Get-Item $archivo).Length / 1MB, 2)
Write-Host ""
Write-Host "BACKUP OK ($mb MB):" -ForegroundColor Green
Write-Host "  $archivo"
Write-Host ""
Write-Host "Para restaurarlo en otra maquina:" -ForegroundColor Yellow
Write-Host "  createdb -h localhost -U postgres $Db"
Write-Host "  pg_restore -h localhost -U postgres -d $Db --no-owner --no-privileges `"$archivo`""
Write-Host ""
Write-Host "Si el destino es la BD de desarrollo del plan contable, despues del restore" -ForegroundColor Yellow
Write-Host "aplicar los scripts de Database/ddl_v3 en el orden del handoff (F1..F8)." -ForegroundColor Yellow
