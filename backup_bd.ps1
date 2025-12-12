# ============================================================
# SCRIPT DE BACKUP - Base de Datos PostgreSQL
# Fecha: 10 de diciembre de 2025
# Propósito: Backup antes de ejecutar migration de renombrado
# ============================================================

# Configuración
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFolder = "D:\jesse\Documents\proyectos\HODSOFT_DEVEXPRESS\Prestadoras\Database\Backups"
$backupFileName = "BACKUP_LOCAL_$timestamp.backup"
$backupPath = Join-Path $backupFolder $backupFileName

# Credenciales PostgreSQL (AJUSTAR SEGÚN TU CONFIGURACIÓN)
$pgHost = "localhost"
$pgPort = "5432"
$pgUser = "postgres"
$pgDatabase = "siad_v2"  # ⚠️ CAMBIAR ESTO

# ============================================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " BACKUP DE BASE DE DATOS POSTGRESQL" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Base de datos: $pgDatabase" -ForegroundColor Yellow
Write-Host "Archivo: $backupFileName" -ForegroundColor Yellow
Write-Host ""

# Crear carpeta de backups si no existe
if (-not (Test-Path $backupFolder)) {
    Write-Host "Creando carpeta de backups..." -ForegroundColor Green
    New-Item -ItemType Directory -Path $backupFolder -Force | Out-Null
}

# Verificar que pg_dump esté disponible
$pgDumpPath = "pg_dump"
try {
    $null = & $pgDumpPath --version 2>&1
} catch {
    Write-Host "ERROR: pg_dump no encontrado en PATH" -ForegroundColor Red
    Write-Host "Asegúrate de tener PostgreSQL instalado y agregado al PATH" -ForegroundColor Red
    Write-Host "Ruta típica: C:\Program Files\PostgreSQL\<version>\bin" -ForegroundColor Yellow
    exit 1
}

# Confirmar antes de proceder
Write-Host "¿Deseas continuar con el backup? (S/N): " -ForegroundColor Yellow -NoNewline
$confirm = Read-Host
if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "Backup cancelado por el usuario." -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "Ejecutando backup..." -ForegroundColor Green

# Ejecutar backup
# Nota: Se te pedirá la contraseña de PostgreSQL
$env:PGPASSWORD = Read-Host "Ingresa la contraseña de PostgreSQL para usuario '$pgUser'" -AsSecureString | ConvertFrom-SecureString

try {
    & $pgDumpPath `
        --host=$pgHost `
        --port=$pgPort `
        --username=$pgUser `
        --dbname=$pgDatabase `
        --format=custom `
        --blobs `
        --verbose `
        --file="$backupPath"

    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host " BACKUP COMPLETADO EXITOSAMENTE" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Archivo guardado en:" -ForegroundColor Cyan
        Write-Host $backupPath -ForegroundColor Yellow
        Write-Host ""
        
        # Mostrar tamaño del archivo
        $fileSize = (Get-Item $backupPath).Length / 1MB
        Write-Host "Tamaño del backup: $([math]::Round($fileSize, 2)) MB" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Ahora puedes ejecutar la migration con seguridad." -ForegroundColor Green
        Write-Host ""
    } else {
        throw "Error durante el backup (código: $LASTEXITCODE)"
    }
} catch {
    Write-Host ""
    Write-Host "ERROR AL CREAR BACKUP:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "NO EJECUTES LA MIGRATION SIN UN BACKUP EXITOSO" -ForegroundColor Red
    exit 1
} finally {
    # Limpiar variable de contraseña
    Remove-Variable PGPASSWORD -ErrorAction SilentlyContinue
}

# ============================================================
# INSTRUCCIONES PARA RESTAURAR (si es necesario)
# ============================================================
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " INSTRUCCIONES DE RESTAURACIÓN" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Si necesitas restaurar este backup:" -ForegroundColor Yellow
Write-Host ""
Write-Host "pg_restore --host=$pgHost --port=$pgPort --username=$pgUser --dbname=$pgDatabase --clean --verbose" -NoNewline -ForegroundColor White
Write-Host " " -NoNewline
Write-Host $backupPath -ForegroundColor Yellow
Write-Host ""
Write-Host "O usando este script:" -ForegroundColor Yellow
Write-Host ".\restore_bd.ps1 -BackupFile " -NoNewline -ForegroundColor White
Write-Host $backupPath -ForegroundColor Yellow
Write-Host ""

