using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIAD.Data;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    [DbContext(typeof(SiadDbContext))]
    [Migration("20260115103000_CleanupLegacyConfiguracionSistemaColumnsFmtCtas")]
    public partial class CleanupLegacyConfiguracionSistemaColumnsFmtCtas : Migration
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
          AND column_name = 'fmt_ctas'
    ) THEN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'con_configuracion_sistema'
              AND column_name = 'formato_cuentas'
        ) THEN
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN fmt_ctas TO formato_cuentas';
        ELSE
            EXECUTE 'UPDATE public.con_configuracion_sistema SET formato_cuentas = COALESCE(formato_cuentas, fmt_ctas) WHERE fmt_ctas IS NOT NULL';
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema DROP COLUMN fmt_ctas';
        END IF;
    END IF;
END $$;
");
        }
    }
}
