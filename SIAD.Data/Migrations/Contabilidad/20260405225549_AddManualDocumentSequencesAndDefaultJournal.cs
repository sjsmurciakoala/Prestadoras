using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class AddManualDocumentSequencesAndDefaultJournal : Migration
    {
        private const string PeriodFixUpdatedBy = "migration-20260405225549";
        private const string PeriodFixRollbackUpdatedBy = "rollback-20260405225549";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS public.\"IX_con_partida_hdr_company_id_type_id\";");

            migrationBuilder.AddColumn<long>(
                name: "document_sequence_start",
                schema: "public",
                table: "con_tipo_transaccion",
                type: "bigint",
                nullable: false,
                defaultValue: 1L);

            migrationBuilder.AddColumn<long>(
                name: "last_document_number",
                schema: "public",
                table: "con_tipo_transaccion",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<bool>(
                name: "is_default_manual",
                schema: "public",
                table: "con_diario",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_hdr_company_id_type_id_document_number",
                schema: "public",
                table: "con_partida_hdr",
                columns: new[] { "company_id", "type_id", "document_number" },
                unique: true,
                filter: "document_number IS NOT NULL AND module = 'MANUAL'");

            migrationBuilder.CreateIndex(
                name: "IX_con_diario_company_id",
                schema: "public",
                table: "con_diario",
                column: "company_id",
                unique: true,
                filter: "is_default_manual AND is_active AND allows_manual");

            migrationBuilder.Sql(
                $@"UPDATE public.con_periodo_contable
   SET end_date = TIMESTAMPTZ '2026-01-31 23:59:59-06',
       updated_at = NOW(),
       updated_by = '{PeriodFixUpdatedBy}'
 WHERE start_date::date = DATE '2026-01-01'
   AND end_date::date <= DATE '2026-01-30';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_con_partida_hdr_company_id_type_id_document_number",
                schema: "public",
                table: "con_partida_hdr");

            migrationBuilder.DropIndex(
                name: "IX_con_diario_company_id",
                schema: "public",
                table: "con_diario");

            migrationBuilder.DropColumn(
                name: "document_sequence_start",
                schema: "public",
                table: "con_tipo_transaccion");

            migrationBuilder.DropColumn(
                name: "last_document_number",
                schema: "public",
                table: "con_tipo_transaccion");

            migrationBuilder.DropColumn(
                name: "is_default_manual",
                schema: "public",
                table: "con_diario");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_hdr_company_id_type_id",
                schema: "public",
                table: "con_partida_hdr",
                columns: new[] { "company_id", "type_id" });

            migrationBuilder.Sql(
                $@"UPDATE public.con_periodo_contable
   SET end_date = TIMESTAMPTZ '2026-01-30 23:59:59-06',
       updated_at = NOW(),
       updated_by = '{PeriodFixRollbackUpdatedBy}'
 WHERE updated_by = '{PeriodFixUpdatedBy}'
   AND start_date::date = DATE '2026-01-01'
   AND end_date::date = DATE '2026-01-31';");
        }
    }
}