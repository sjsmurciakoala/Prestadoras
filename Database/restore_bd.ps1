# ============================================================
# SCRIPT DE RESTAURACION - Base de Datos PostgreSQL
# ============================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RESTAURACION DE BASE DE DATOS POSTGRESQL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$backupFolder = "E:\Koala\proyectos\HODSOFT_DEVEXPRESS\Prestadoras\Database\Backups"

if (-not (Test-Path $backupFolder)) {
    Write-Host "ERROR: Carpeta de backups no encontrada: $backupFolder" -ForegroundColor Red
    exit 1
}

$backupFiles = Get-ChildItem -Path $backupFolder -Filter "*.backup" | Sort-Object -Property LastWriteTime -Descending

if ($backupFiles.Count -eq 0) {
    Write-Host "ERROR: No se encontraron archivos de backup en $backupFolder" -ForegroundColor Red
    exit 1
}

Write-Host "Backups disponibles:" -ForegroundColor Yellow
for ($i = 0; $i -lt $backupFiles.Count; $i++) {
    $size = [math]::Round($backupFiles[$i].Length / 1MB, 2)
    Write-Host "$($i + 1)) $($backupFiles[$i].Name) - $($size) MB - $($backupFiles[$i].LastWriteTime)" -ForegroundColor White
}

Write-Host ""
Write-Host "Selecciona el numero del backup a restaurar (o Enter para el mas reciente): " -ForegroundColor Yellow -NoNewline
$selection = Read-Host

if ([string]::IsNullOrWhiteSpace($selection)) {
    $selectedBackup = $backupFiles[0]
    $backupIndex = 0
} else {
    $backupIndex = [int]$selection - 1
    if ($backupIndex -lt 0 -or $backupIndex -ge $backupFiles.Count) {
        Write-Host "ERROR: Seleccion invalida" -ForegroundColor Red
        exit 1
    }
    $selectedBackup = $backupFiles[$backupIndex]
}

$backupPath = $selectedBackup.FullName
Write-Host ""
Write-Host "Backup seleccionado: $($selectedBackup.Name)" -ForegroundColor Green
Write-Host ""

$pgHost = "172.16.0.9"
$pgPort = "5432"
$pgUser = "postgres"
$pgDatabase = "siad_v3"
$pgMaintenanceDb = "postgres"

Write-Host "Modo de restauracion:" -ForegroundColor Yellow
Write-Host "1) Nueva BD (recomendado)" -ForegroundColor White
Write-Host "2) Reemplazar BD existente (BORRA TODO)" -ForegroundColor White
Write-Host ""
Write-Host "Selecciona el modo (1/2, Enter = 1): " -ForegroundColor Yellow -NoNewline
$restoreModeInput = Read-Host
if ([string]::IsNullOrWhiteSpace($restoreModeInput)) {
    $restoreModeInput = "1"
}

if ($restoreModeInput -ne "1" -and $restoreModeInput -ne "2") {
    Write-Host "ERROR: Modo invalido" -ForegroundColor Red
    exit 1
}

$targetDb = $pgDatabase
$dropAndCreate = $false

if ($restoreModeInput -eq "1") {
    $defaultDbName = "${pgDatabase}_restore"
    Write-Host ""
    Write-Host "Nombre de la nueva BD (Enter = $defaultDbName): " -ForegroundColor Yellow -NoNewline
    $newDbName = Read-Host
    if ([string]::IsNullOrWhiteSpace($newDbName)) {
        $newDbName = $defaultDbName
    }
    $targetDb = ($newDbName -replace "[^a-zA-Z0-9_]", "_").ToLowerInvariant()
    if ($targetDb -eq $pgDatabase) {
        Write-Host "ERROR: La nueva BD no puede llamarse igual que la BD principal." -ForegroundColor Red
        exit 1
    }
} else {
    $dropAndCreate = $true
}

Write-Host ""
Write-Host "Modo SSL: P) Prefer (default), S) Requerir, N) Deshabilitar: " -ForegroundColor Yellow -NoNewline
$sslInput = Read-Host
$sslMode = "prefer"
if (-not [string]::IsNullOrWhiteSpace($sslInput)) {
    if ($sslInput -eq "S" -or $sslInput -eq "s") {
        $sslMode = "require"
    } elseif ($sslInput -eq "N" -or $sslInput -eq "n") {
        $sslMode = "disable"
    } elseif ($sslInput -eq "P" -or $sslInput -eq "p") {
        $sslMode = "prefer"
    } else {
        Write-Host "ERROR: Opcion SSL invalida (usa P, S o N)" -ForegroundColor Red
        exit 1
    }
}
Set-Item -Path Env:PGSSLMODE -Value $sslMode

if (-not $dropAndCreate) {
    # Para crear una BD nueva, usamos una base de mantenimiento segura
    $pgMaintenanceDb = "postgres"
} else {
    Write-Host ""
    Write-Host "Base de mantenimiento para crear/borrar (Enter = $pgMaintenanceDb): " -ForegroundColor Yellow -NoNewline
    $maintenanceInput = Read-Host
    if (-not [string]::IsNullOrWhiteSpace($maintenanceInput)) {
        $pgMaintenanceDb = $maintenanceInput
    }
}

if ($dropAndCreate -and $pgMaintenanceDb -eq $pgDatabase) {
    Write-Host "ERROR: La base de mantenimiento no puede ser la misma que la base a reemplazar." -ForegroundColor Red
    exit 1
}

# Verifica que psql y pg_restore esten disponibles antes de validar la BD de mantenimiento
function Resolve-PgToolPath([string]$toolName) {
    $cmd = Get-Command $toolName -ErrorAction SilentlyContinue
    if ($null -eq $cmd) { return $null }
    if ($cmd -is [System.Management.Automation.AliasInfo]) {
        $cmd = $cmd.ResolvedCommand
    }
    return $cmd.Source
}

$pgRestorePath = Resolve-PgToolPath "pg_restore"
$psqlPath = Resolve-PgToolPath "psql"

if ([string]::IsNullOrWhiteSpace($pgRestorePath)) {
    Write-Host "ERROR: pg_restore no encontrado en PATH" -ForegroundColor Red
    Write-Host "Asegurate de tener PostgreSQL instalado y agregado al PATH" -ForegroundColor Red
    exit 1
}

if ([string]::IsNullOrWhiteSpace($psqlPath)) {
    Write-Host "ERROR: psql no encontrado en PATH" -ForegroundColor Red
    Write-Host "Asegurate de tener PostgreSQL instalado y agregado al PATH" -ForegroundColor Red
    exit 1
}

# Verifica que la base de mantenimiento exista; si no, usa template1 como fallback
if (-not $dropAndCreate) {
    $existsMaintenance = & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb -tAc "SELECT 1;"
    if ($LASTEXITCODE -ne 0) {
        $pgMaintenanceDb = "template1"
        $existsMaintenance = & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb -tAc "SELECT 1;"
        if ($LASTEXITCODE -ne 0) {
            Write-Host "ERROR: No se pudo conectar a una base de mantenimiento (postgres/template1)." -ForegroundColor Red
            exit 1
        }
    }
}

if ($dropAndCreate) {
    Write-Host ""
    Write-Host "ADVERTENCIA: Esta operacion BORRARA la base de datos '$pgDatabase'" -ForegroundColor Red
    Write-Host "Deseas continuar? (S/N): " -ForegroundColor Yellow -NoNewline
    $confirm = Read-Host
    if ($confirm -ne "S" -and $confirm -ne "s") {
        Write-Host "Restauracion cancelada." -ForegroundColor Red
        exit 0
    }
} else {
    Write-Host ""
    Write-Host "Se restaurara el backup en la BD nueva: '$targetDb'" -ForegroundColor Green
}

Write-Host ""
Write-Host "Ejecutando restauracion..." -ForegroundColor Green
Write-Host "Se te pedira la contrasena de PostgreSQL" -ForegroundColor Yellow
Write-Host ""

if ($dropAndCreate) {
    $terminateSql = "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '$pgDatabase' AND pid <> pg_backend_pid();"
    $dropSql = "DROP DATABASE IF EXISTS `"$pgDatabase`";"
    $createSql = "CREATE DATABASE `"$pgDatabase`";"

    & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb --command $terminateSql
    if ($LASTEXITCODE -ne 0) { exit 1 }
    & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb --command $dropSql
    if ($LASTEXITCODE -ne 0) { exit 1 }
    & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb --command $createSql
    if ($LASTEXITCODE -ne 0) { exit 1 }
} else {
    $existsCheck = & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb -tAc "SELECT 1 FROM pg_database WHERE datname = '$targetDb';"
    if ($LASTEXITCODE -ne 0) { exit 1 }
    if ([string]::IsNullOrWhiteSpace($existsCheck)) {
        $existsCheck = ""
    }
    if ($existsCheck.Trim() -eq "1") {
        Write-Host "ERROR: La BD '$targetDb' ya existe. Usa otro nombre." -ForegroundColor Red
        exit 1
    }
    $createSql = "CREATE DATABASE `"$targetDb`";"
    & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgMaintenanceDb --command $createSql
    if ($LASTEXITCODE -ne 0) { exit 1 }
}

try {
    $connCheck = & $psqlPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$targetDb -tAc "SELECT 1;"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: No se pudo conectar a la BD '$targetDb'. Revisa pg_hba.conf." -ForegroundColor Red
        exit 1
    }

    & $pgRestorePath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$targetDb --verbose --format=custom "$backupPath"
    if ($LASTEXITCODE -ne 0) {
        throw "pg_restore termino con errores. Revisa el log anterior."
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host " RESTAURACION COMPLETADA EXITOSAMENTE" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Base de datos: $targetDb" -ForegroundColor Yellow
    Write-Host "Archivo restaurado: $($selectedBackup.Name)" -ForegroundColor Yellow
    Write-Host ""
} catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host " ERROR DURANTE LA RESTAURACION" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
