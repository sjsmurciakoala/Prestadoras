using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class CreateAccountBalanceTablesFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "con_saldo_cuenta",
                schema: "public",
                columns: table => new
                {
                    saldo_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo_cuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mes = table.Column<byte>(type: "smallint", nullable: false),
                    tipo_transaccion = table.Column<byte>(type: "smallint", nullable: false),
                    debitos = table.Column<decimal>(type: "numeric(28,2)", nullable: false, defaultValue: 0m),
                    creditos = table.Column<decimal>(type: "numeric(28,2)", nullable: false, defaultValue: 0m),
                    cantidad_debitos = table.Column<int>(type: "integer", nullable: false),
                    cantidad_creditos = table.Column<int>(type: "integer", nullable: false),
                    presupuesto = table.Column<decimal>(type: "numeric(28,2)", nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_saldo_cuenta", x => x.saldo_id);
                    table.ForeignKey(
                        name: "FK_con_saldo_cuenta_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_saldo_cuenta_con_periodo_contable_periodo_id",
                        column: x => x.periodo_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_saldo_cuenta_con_plan_cuentas_codigo_cuenta",
                        columns: x => new { x.company_id, x.codigo_cuenta },
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumns: new[] { "company_id", "code" },
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "con_configuracion_balance",
                schema: "public",
                columns: table => new
                {
                    config_balance_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_linea = table.Column<short>(type: "smallint", nullable: false),
                    clase = table.Column<byte>(type: "smallint", nullable: false),
                    codigo_cuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    descripcion_cuenta = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    descripcion_linea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    porcentaje_activo = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    mostrar_en_reporte = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_balance", x => x.config_balance_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_balance_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_balance_con_periodo_contable_periodo_id",
                        column: x => x.periodo_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_configuracion_balance_con_plan_cuentas_codigo_cuenta",
                        columns: x => new { x.company_id, x.codigo_cuenta },
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumns: new[] { "company_id", "code" },
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateTable(
                name: "con_configuracion_linea_resultado",
                schema: "public",
                columns: table => new
                {
                    config_linea_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_linea = table.Column<short>(type: "smallint", nullable: false),
                    tipo_linea = table.Column<byte>(type: "smallint", nullable: false),
                    codigo_cuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    descripcion_linea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    columna_reporte = table.Column<byte>(type: "smallint", nullable: true),
                    mostrar_subtotal = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    nivel_indentacion = table.Column<byte>(type: "smallint", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_linea_resultado", x => x.config_linea_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_linea_resultado_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_linea_resultado_con_periodo_contable_periodo_id",
                        column: x => x.periodo_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_configuracion_linea_resultado_con_plan_cuentas_codigo_cuenta",
                        columns: x => new { x.company_id, x.codigo_cuenta },
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumns: new[] { "company_id", "code" },
                        onDelete: ReferentialAction.Restrict);
                });
            migrationBuilder.CreateIndex(
                name: "IX_con_saldo_cuenta_company_id_periodo_id_codigo_cuenta_mes_tipo_transaccion",
                schema: "public",
                table: "con_saldo_cuenta",
                columns: new[] { "company_id", "periodo_id", "codigo_cuenta", "mes", "tipo_transaccion" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_balance_company_id_periodo_id_numero_linea",
                schema: "public",
                table: "con_configuracion_balance",
                columns: new[] { "company_id", "periodo_id", "numero_linea" },
                unique: true);
            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_linea_resultado_company_id_periodo_id_numero_linea",
                schema: "public",
                table: "con_configuracion_linea_resultado",
                columns: new[] { "company_id", "periodo_id", "numero_linea" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "con_saldo_cuenta",
                schema: "public");
            migrationBuilder.DropTable(
                name: "con_configuracion_balance",
                schema: "public");
            migrationBuilder.DropTable(
                name: "con_configuracion_linea_resultado",
                schema: "public");
        }
    }
}


