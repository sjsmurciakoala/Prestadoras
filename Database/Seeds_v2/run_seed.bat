@echo off
REM Script para ejecutar el seed de Plan de Cuentas de prueba
REM Requiere psql instalado en el PATH

setlocal enabledelayedexpansion

set PGHOST=localhost
set PGPORT=5432
set PGDATABASE=siad_v2
set PGUSER=postgres
set PGPASSWORD=12345

echo Ejecutando seed de Plan de Cuentas...
psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -f "11_seed_plan_cuentas_demo.sql"

if %errorlevel% equ 0 (
    echo.
    echo ✓ Seed ejecutado exitosamente
    echo.
    echo Verificando datos insertados...
    psql -h %PGHOST% -p %PGPORT% -U %PGUSER% -d %PGDATABASE% -c "SELECT COUNT(*) as total_cuentas FROM public.con_plan_cuentas WHERE company_id IN (SELECT company_id FROM public.cfg_company WHERE code = 'SIAD-DEMO');"
) else (
    echo.
    echo ✗ Error ejecutando seed
    exit /b 1
)

endlocal
