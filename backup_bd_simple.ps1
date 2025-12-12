# ============================================================
# SCRIPT DE BACKUP - Base de Datos PostgreSQL
# ============================================================

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFolder = "D:\jesse\Documents\proyectos\HODSOFT_DEVEXPRESS\Prestadoras\Database\Backups"
$backupFileName = "backup_local_$timestamp.backup"
$backupPath = Join-Path $backupFolder $backupFileName

$pgHost = "localhost"
$pgPort = "5432"
$pgUser = "postgres"
$pgDatabase = "siad_v2"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BACKUP DE BASE DE DATOS POSTGRESQL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Base de datos: $pgDatabase" -ForegroundColor Yellow
Write-Host "Archivo: $backupFileName" -ForegroundColor Yellow
Write-Host ""

if (-not (Test-Path $backupFolder)) {
    Write-Host "Creando carpeta de backups..." -ForegroundColor Green
    New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
}

$pgDumpPath = "pg_dump"
try {
    $null = & $pgDumpPath --version 2>&1
} catch {
    Write-Host "ERROR: pg_dump no encontrado en PATH" -ForegroundColor Red
    Write-Host "Asegurate de tener PostgreSQL instalado y agregado al PATH" -ForegroundColor Red
    exit 1
}

Write-Host "Deseas continuar con el backup? (S/N): " -ForegroundColor Yellow -NoNewline
$confirm = Read-Host
if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "Backup cancelado." -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "Ejecutando backup..." -ForegroundColor Green
Write-Host "Se te pedira la contrasena de PostgreSQL" -ForegroundColor Yellow
Write-Host ""

try {
    & $pgDumpPath --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgDatabase --format=custom --blobs --verbose --file="$backupPath"

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host " BACKUP COMPLETADO EXITOSAMENTE" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Archivo guardado en:" -ForegroundColor Cyan
        Write-Host $backupPath -ForegroundColor Yellow
        Write-Host ""
        
        $fileSize = (Get-Item $backupPath).Length / 1MB
        Write-Host "Tamanio del backup: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Ahora puedes ejecutar la migration con seguridad." -ForegroundColor Green
        Write-Host ""
        Write-Host "Para restaurar este backup en caso necesario:" -ForegroundColor Yellow
        Write-Host ".\restore_bd.ps1" -ForegroundColor White
        Write-Host ""
    } else {
        throw "Error durante el backup (codigo: $LASTEXITCODE)"
    }
} catch {
    Write-Host ""
    Write-Host "ERROR AL CREAR BACKUP:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "NO EJECUTES LA MIGRATION SIN UN BACKUP EXITOSO" -ForegroundColor Red
    exit 1
}
