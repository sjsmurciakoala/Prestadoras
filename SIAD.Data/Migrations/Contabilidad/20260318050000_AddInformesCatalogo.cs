using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SIAD.Data;

namespace SIAD.Data.Migrations.Contabilidad;

[DbContext(typeof(SiadDbContext))]
[Migration("20260318050000_AddInformesCatalogo")]
public sealed class AddInformesCatalogo : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "rep_catalogo_informe",
            schema: "public",
            columns: table => new
            {
                informe_id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                company_id = table.Column<long>(type: "bigint", nullable: false),
                codigo = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                categoria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                tipo_origen = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                ruta = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                consulta_clave = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                icono_css_class = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                orden = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                permite_exportar = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                permite_imprimir = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                filtros_schema_json = table.Column<string>(type: "text", nullable: true),
                metadata_json = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_rep_catalogo_informe", x => x.informe_id);
                table.ForeignKey(
                    name: "FK_rep_catalogo_informe_cfg_company_company_id",
                    column: x => x.company_id,
                    principalSchema: "public",
                    principalTable: "cfg_company",
                    principalColumn: "company_id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_rep_catalogo_informe_company_id_codigo",
            schema: "public",
            table: "rep_catalogo_informe",
            columns: new[] { "company_id", "codigo" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_rep_catalogo_informe_company_id_ruta",
            schema: "public",
            table: "rep_catalogo_informe",
            columns: new[] { "company_id", "ruta" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "rep_catalogo_informe",
            schema: "public");
    }
}
