# ============================================================
# SCRIPT DE EJECUCIÓN - Migration de Renombrado
# Fecha: 10 de diciembre de 2025
# Propósito: Ejecutar migration con verificaciones de seguridad
# ============================================================

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " EJECUCIÓN DE MIGRATION" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Verificar ubicación
$projectPath = "D:\jesse\Documents\proyectos\HODSOFT_DEVEXPRESS\Prestadoras"
$apcPath = Join-Path $projectPath "apc"
$dataPath = Join-Path $projectPath "SIAD.Data"

if (-not (Test-Path $apcPath)) {
    Write-Host "ERROR: No se encontró el proyecto apc" -ForegroundColor Red
    exit 1
}

# Cambiar a directorio del proyecto
Set-Location $apcPath

Write-Host "Ubicación actual: $apcPath" -ForegroundColor Yellow
Write-Host ""

# Verificar que existe backup reciente (últimas 24 horas)
$backupFolder = Join-Path $projectPath "Database\Backups"
if (Test-Path $backupFolder) {
    $recentBackup = Get-ChildItem -Path $backupFolder -Filter "*.backup" | 
                    Where-Object { $_.LastWriteTime -gt (Get-Date).AddHours(-24) } | 
                    Sort-Object LastWriteTime -Descending | 
                    Select-Object -First 1
    
    if ($null -eq $recentBackup) {
        Write-Host "⚠️  ADVERTENCIA: No hay backup reciente (últimas 24h)" -ForegroundColor Red
        Write-Host "Se recomienda ejecutar .\backup_bd.ps1 primero" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "¿Deseas continuar sin backup reciente? (S/N): " -ForegroundColor Yellow -NoNewline
        $confirm = Read-Host
        if ($confirm -ne "S" -and $confirm -ne "s") {
            Write-Host "Migration cancelada. Ejecuta primero: .\backup_bd.ps1" -ForegroundColor Red
            exit 0
        }
    } else {
        Write-Host "✅ Backup reciente encontrado:" -ForegroundColor Green
        Write-Host "   $($recentBackup.Name)" -ForegroundColor White
        Write-Host "   Fecha: $($recentBackup.LastWriteTime)" -ForegroundColor Gray
        Write-Host ""
    }
}

# Mostrar información de la migration
Write-Host "Migration a aplicar:" -ForegroundColor Cyan
Write-Host "  20251210_RenameConfigurationColumnsForConciseness" -ForegroundColor White
Write-Host ""
Write-Host "Tabla afectada:" -ForegroundColor Cyan
Write-Host "  con_configuracion_sistema (solo renombrado de 22 columnas)" -ForegroundColor White
Write-Host ""

# Confirmación final
Write-Host "¿Ejecutar migration ahora? (S/N): " -ForegroundColor Yellow -NoNewline
$confirm = Read-Host

if ($confirm -ne "S" -and $confirm -ne "s") {
    Write-Host "Migration cancelada por el usuario." -ForegroundColor Red
    exit 0
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Ejecutando migration..." -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""

try {
    # Ejecutar migration
    dotnet ef database update --project ..\SIAD.Data
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Green
        Write-Host " ✅ MIGRATION COMPLETADA EXITOSAMENTE" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Green
        Write-Host ""
        Write-Host "Cambios aplicados:" -ForegroundColor Cyan
        Write-Host "  • 22 columnas renombradas en con_configuracion_sistema" -ForegroundColor White
        Write-Host "  • Todos los datos preservados" -ForegroundColor White
        Write-Host ""
        Write-Host "Próximos pasos:" -ForegroundColor Yellow
        Write-Host "  1. Probar la aplicación" -ForegroundColor White
        Write-Host "  2. Verificar que carga/guarda correctamente" -ForegroundColor White
        Write-Host "  3. Revisar página de Configuración Sistema" -ForegroundColor White
        Write-Host ""
    } else {
        throw "Error durante la migration (código: $LASTEXITCODE)"
    }
} catch {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Red
    Write-Host " ❌ ERROR EN MIGRATION" -ForegroundColor Red
    Write-Host "========================================" -ForegroundColor Red
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Para revertir, ejecuta:" -ForegroundColor Yellow
    Write-Host "  dotnet ef database update <nombre_migration_anterior> --project ..\SIAD.Data" -ForegroundColor White
    Write-Host ""
    Write-Host "O restaura el backup con:" -ForegroundColor Yellow
    Write-Host "  .\restore_bd.ps1" -ForegroundColor White
    Write-Host ""
    exit 1
}

Write-Host "Presiona Enter para cerrar..." -ForegroundColor Gray
Read-Host
