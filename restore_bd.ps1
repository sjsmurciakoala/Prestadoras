# ============================================================
# SCRIPT DE RESTAURACIÓN - Base de Datos PostgreSQL
# Fecha: 10 de diciembre de 2025
# Propósito: Restaurar backup en caso de problemas
# ============================================================

param(
    [Parameter(Mandatory=$false)]
    [string]$BackupFile
)

# Configuración
$pgHost = "localhost"
$pgPort = "5432"
$pgUser = "postgres"
$pgDatabase = "siad_v2"

# ============================================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " RESTAURACIÓN DE BASE DE DATOS" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Si no se especifica backup, buscar el más reciente
if ([string]::IsNullOrWhiteSpace($BackupFile)) {
    $backupFolder = "D:\jesse\Documents\proyectos\HODSOFT_DEVEXPRESS\Prestadoras\Database\Backups"
    $latestBackup = Get-ChildItem -Path $backupFolder -Filter "*.backup" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    
    if ($null -eq $latestBackup) {
        Write-Host "ERROR: No se encontraron archivos de backup" -ForegroundColor Red
        exit 1
    }
    
    $BackupFile = $latestBackup.FullName
    Write-Host "Usando backup más reciente:" -ForegroundColor Yellow
    Write-Host $BackupFile -ForegroundColor White
} else {
    if (-not (Test-Path $BackupFile)) {
        Write-Host "ERROR: Archivo de backup no encontrado: $BackupFile" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Base de datos: $pgDatabase" -ForegroundColor Yellow
Write-Host "Archivo: $BackupFile" -ForegroundColor Yellow
Write-Host ""

# Advertencia
Write-Host "⚠️  ADVERTENCIA ⚠️" -ForegroundColor Red
Write-Host "Esta operación eliminará todos los datos actuales" -ForegroundColor Red
Write-Host "y los reemplazará con el backup especificado." -ForegroundColor Red
Write-Host ""
Write-Host "¿Estás SEGURO de continuar? (Escribe 'SI' para confirmar): " -ForegroundColor Yellow -NoNewline
$confirm = Read-Host

if ($confirm -ne "SI") {
    Write-Host "Restauración cancelada." -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "Ejecutando restauración..." -ForegroundColor Green

# Solicitar contraseña
$env:PGPASSWORD = Read-Host "Ingresa la contraseña de PostgreSQL para usuario '$pgUser'" -AsSecureString | ConvertFrom-SecureString

try {
    & pg_restore `
        --host=$pgHost `
        --port=$pgPort `
        --username=$pgUser `
        --dbname=$pgDatabase `
        --clean `
        --if-exists `
        --verbose `
        "$BackupFile"

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host " RESTAURACIÓN COMPLETADA" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
    } else {
        throw "Error durante la restauración (código: $LASTEXITCODE)"
    }
} catch {
    Write-Host ""
    Write-Host "ERROR AL RESTAURAR:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally {
    Remove-Variable PGPASSWORD -ErrorAction SilentlyContinue
}
