using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIAD.Data;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    [DbContext(typeof(SiadDbContext))]
    [Migration("20260115090000_CleanupLegacyConfiguracionSistemaColumns")]
    public partial class CleanupLegacyConfiguracionSistemaColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'sep_codigo'
    ) THEN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'con_configuracion_sistema'
              AND column_name = 'separador_codigo'
        ) THEN
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN sep_codigo TO separador_codigo';
        ELSE
            EXECUTE 'UPDATE public.con_configuracion_sistema SET separador_codigo = COALESCE(separador_codigo, sep_codigo) WHERE sep_codigo IS NOT NULL';
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema DROP COLUMN sep_codigo';
        END IF;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'meses_calc'
    ) THEN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'con_configuracion_sistema'
              AND column_name = 'meses_calculados'
        ) THEN
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN meses_calc TO meses_calculados';
        ELSE
            EXECUTE 'UPDATE public.con_configuracion_sistema SET meses_calculados = COALESCE(meses_calculados, meses_calc) WHERE meses_calc IS NOT NULL';
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema DROP COLUMN meses_calc';
        END IF;
    END IF;
END $$;
");
        }
    }
}
