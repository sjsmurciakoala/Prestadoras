using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class DropOldConfigurationTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Eliminar tablas antiguas (ahora todo está en con_configuracion_sistema)
            // Orden importante: eliminar primero las que tienen FK a otras
            
            migrationBuilder.DropTable(
                name: "con_configuracion_estado_resultado",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_correlativo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_esf",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_cuentas_utilidad",
                schema: "public");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restaurar tablas antiguas (para poder revertir si es necesario)
            migrationBuilder.CreateTable(
                name: "con_configuracion_cuentas_utilidad",
                schema: "public",
                columns: table => new
                {
                    config_util_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    cuenta_util_acumulada_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_util_acumulada_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_util_ejercicio_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_util_ejercicio_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_acumulada_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_acumulada_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_ejercicio_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_ejercicio_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_cuentas_utilidad", x => x.config_util_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_perdida_acumulada_historica",
                        column: x => x.cuenta_perdida_acumulada_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_perdida_acumulada_inflacion",
                        column: x => x.cuenta_perdida_acumulada_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_perdida_ejercicio_historica",
                        column: x => x.cuenta_perdida_ejercicio_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_perdida_ejercicio_inflacion",
                        column: x => x.cuenta_perdida_ejercicio_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_util_acumulada_historica",
                        column: x => x.cuenta_util_acumulada_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_util_acumulada_inflacion",
                        column: x => x.cuenta_util_acumulada_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_util_ejercicio_historica",
                        column: x => x.cuenta_util_ejercicio_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_util_ejercicio_inflacion",
                        column: x => x.cuenta_util_ejercicio_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_esf",
                schema: "public",
                columns: table => new
                {
                    config_esf_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    activo_corto_plazo_1 = table.Column<long>(type: "bigint", nullable: true),
                    activo_corto_plazo_2 = table.Column<long>(type: "bigint", nullable: true),
                    activo_largo_plazo_1 = table.Column<long>(type: "bigint", nullable: true),
                    activo_largo_plazo_2 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_corto_plazo_1 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_corto_plazo_2 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_largo_plazo_1 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_largo_plazo_2 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_y_capital = table.Column<long>(type: "bigint", nullable: true),
                    capital_aportado = table.Column<long>(type: "bigint", nullable: true),
                    resultados_acumulados = table.Column<long>(type: "bigint", nullable: true),
                    utilidad_perdida_ejercicio = table.Column<long>(type: "bigint", nullable: true),
                    sobrevaluaciones = table.Column<long>(type: "bigint", nullable: true),
                    mostrar_orden = table.Column<bool>(type: "boolean", nullable: false),
                    mostrar_percontra = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_esf", x => x.config_esf_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_activo_corto_plazo_1",
                        column: x => x.activo_corto_plazo_1,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_activo_corto_plazo_2",
                        column: x => x.activo_corto_plazo_2,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_activo_largo_plazo_1",
                        column: x => x.activo_largo_plazo_1,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_activo_largo_plazo_2",
                        column: x => x.activo_largo_plazo_2,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_capital_aportado",
                        column: x => x.capital_aportado,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_corto_plazo_1",
                        column: x => x.pasivo_corto_plazo_1,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_corto_plazo_2",
                        column: x => x.pasivo_corto_plazo_2,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_largo_plazo_1",
                        column: x => x.pasivo_largo_plazo_1,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_largo_plazo_2",
                        column: x => x.pasivo_largo_plazo_2,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_y_capital",
                        column: x => x.pasivo_y_capital,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_resultados_acumulados",
                        column: x => x.resultados_acumulados,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_sobrevaluaciones",
                        column: x => x.sobrevaluaciones,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_utilidad_perdida_ejercicio",
                        column: x => x.utilidad_perdida_ejercicio,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_estado_resultado",
                schema: "public",
                columns: table => new
                {
                    config_resultado_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_estado_resultado", x => x.config_resultado_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_estado_resultado_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_correlativo",
                schema: "public",
                columns: table => new
                {
                    config_correlativo_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    numerador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    siguiente_numero = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_correlativo", x => x.config_correlativo_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_correlativo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_company_id",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_company_id",
                schema: "public",
                table: "con_configuracion_esf",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_estado_resultado_company_id_codigo",
                schema: "public",
                table: "con_configuracion_estado_resultado",
                columns: new[] { "company_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_correlativo_company_id_tipo",
                schema: "public",
                table: "con_configuracion_correlativo",
                columns: new[] { "company_id", "tipo" },
                unique: true);
        }
    }
}
