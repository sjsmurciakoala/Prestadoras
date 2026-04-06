using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIAD.Data;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    [DbContext(typeof(SiadDbContext))]
    [Migration("20251230203000_NormalizePlanCuentaCodes")]
    public partial class NormalizePlanCuentaCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM (
            SELECT company_id,
                   regexp_replace(code, '[\.\-\/\s]', '', 'g') AS norm_code,
                   COUNT(*) AS cnt
            FROM public.con_plan_cuentas
            WHERE code IS NOT NULL AND code <> ''
            GROUP BY company_id, norm_code
            HAVING COUNT(*) > 1
        ) d
    ) THEN
        RAISE EXCEPTION 'Duplicate account codes after normalization. Fix data before applying migration.';
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
UPDATE public.con_plan_cuentas
SET code = regexp_replace(code, '[\.\-\/\s]', '', 'g')
WHERE code IS NOT NULL;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.con_saldo_cuenta') IS NOT NULL THEN
        EXECUTE 'UPDATE public.con_saldo_cuenta SET codigo_cuenta = regexp_replace(codigo_cuenta, ''[./\s-]'', '''', ''g'') WHERE codigo_cuenta IS NOT NULL';
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.con_configuracion_balance') IS NOT NULL THEN
        EXECUTE 'UPDATE public.con_configuracion_balance SET codigo_cuenta = regexp_replace(codigo_cuenta, ''[./\s-]'', '''', ''g'') WHERE codigo_cuenta IS NOT NULL';
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.con_configuracion_linea_resultado') IS NOT NULL THEN
        EXECUTE 'UPDATE public.con_configuracion_linea_resultado SET codigo_cuenta = regexp_replace(codigo_cuenta, ''[./\s-]'', '''', ''g'') WHERE codigo_cuenta IS NOT NULL';
    END IF;
END $$;
");

            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF to_regclass('public.con_configuracion_sistema') IS NOT NULL THEN
        EXECUTE 'UPDATE public.con_configuracion_sistema
        SET codigo_cuenta_util_acumulada_historica = regexp_replace(codigo_cuenta_util_acumulada_historica, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_util_ejercicio_historica = regexp_replace(codigo_cuenta_util_ejercicio_historica, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_perdida_acumulada_historica = regexp_replace(codigo_cuenta_perdida_acumulada_historica, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_perdida_ejercicio_historica = regexp_replace(codigo_cuenta_perdida_ejercicio_historica, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_util_acumulada_inflacion = regexp_replace(codigo_cuenta_util_acumulada_inflacion, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_util_ejercicio_inflacion = regexp_replace(codigo_cuenta_util_ejercicio_inflacion, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_perdida_acumulada_inflacion = regexp_replace(codigo_cuenta_perdida_acumulada_inflacion, ''[./\s-]'', '''', ''g''),
            codigo_cuenta_perdida_ejercicio_inflacion = regexp_replace(codigo_cuenta_perdida_ejercicio_inflacion, ''[./\s-]'', '''', ''g'')';
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
