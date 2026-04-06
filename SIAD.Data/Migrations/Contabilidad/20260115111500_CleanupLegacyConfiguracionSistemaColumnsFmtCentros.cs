using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SIAD.Data;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    [DbContext(typeof(SiadDbContext))]
    [Migration("20260115111500_CleanupLegacyConfiguracionSistemaColumnsFmtCentros")]
    public partial class CleanupLegacyConfiguracionSistemaColumnsFmtCentros : Migration
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
          AND column_name = 'fmt_centros'
    ) THEN
        IF NOT EXISTS (
            SELECT 1
            FROM information_schema.columns
            WHERE table_schema = 'public'
              AND table_name = 'con_configuracion_sistema'
              AND column_name = 'formato_centros'
        ) THEN
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN fmt_centros TO formato_centros';
        ELSE
            EXECUTE 'UPDATE public.con_configuracion_sistema SET formato_centros = COALESCE(formato_centros, fmt_centros) WHERE fmt_centros IS NOT NULL';
            EXECUTE 'ALTER TABLE public.con_configuracion_sistema DROP COLUMN fmt_centros';
        END IF;
    END IF;
END $$;
");
        }
    }
}
