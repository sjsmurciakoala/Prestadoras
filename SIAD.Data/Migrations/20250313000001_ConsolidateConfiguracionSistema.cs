using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateConfiguracionSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Agregar nuevas columnas a con_configuracion_sistema
            migrationBuilder.AddColumn<long>(
                name: "cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "activo_corto_plazo_1",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "activo_corto_plazo_2",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "activo_largo_plazo_1",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "activo_largo_plazo_2",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pasivo_corto_plazo_1",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pasivo_corto_plazo_2",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pasivo_largo_plazo_1",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pasivo_largo_plazo_2",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "pasivo_y_capital",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "capital_aportado",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "resultados_acumulados",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "utilidad_perdida_ejercicio",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "sobrevaluaciones",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "mostrar_orden",
                table: "con_configuracion_sistema",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "mostrar_percontra",
                table: "con_configuracion_sistema",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "titulo_estado_resultados",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Estado de Resultados");

            migrationBuilder.AddColumn<string>(
                name: "titulo_balance_general",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Balance General");

            migrationBuilder.AddColumn<string>(
                name: "descripcion_activo",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "ACTIVO");

            migrationBuilder.AddColumn<string>(
                name: "descripcion_pasivo",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "PASIVO");

            migrationBuilder.AddColumn<string>(
                name: "descripcion_capital",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "CAPITAL");

            migrationBuilder.AddColumn<string>(
                name: "descripcion_pasivo_capital",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "PASIVO y CAPITAL");

            migrationBuilder.AddColumn<string>(
                name: "descripcion_orden",
                table: "con_configuracion_sistema",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "CUENTAS ORDEN");

            // Crear índices para las claves foráneas
            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema",
                column: "cuenta_util_acumulada_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema",
                column: "cuenta_util_ejercicio_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_acumulada_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_ejercicio_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema",
                column: "cuenta_util_acumulada_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                column: "cuenta_util_ejercicio_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_acumulada_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_ejercicio_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_activo_corto_plazo_1",
                table: "con_configuracion_sistema",
                column: "activo_corto_plazo_1");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_activo_corto_plazo_2",
                table: "con_configuracion_sistema",
                column: "activo_corto_plazo_2");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_activo_largo_plazo_1",
                table: "con_configuracion_sistema",
                column: "activo_largo_plazo_1");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_activo_largo_plazo_2",
                table: "con_configuracion_sistema",
                column: "activo_largo_plazo_2");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_pasivo_corto_plazo_1",
                table: "con_configuracion_sistema",
                column: "pasivo_corto_plazo_1");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_pasivo_corto_plazo_2",
                table: "con_configuracion_sistema",
                column: "pasivo_corto_plazo_2");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_pasivo_largo_plazo_1",
                table: "con_configuracion_sistema",
                column: "pasivo_largo_plazo_1");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_pasivo_largo_plazo_2",
                table: "con_configuracion_sistema",
                column: "pasivo_largo_plazo_2");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_pasivo_y_capital",
                table: "con_configuracion_sistema",
                column: "pasivo_y_capital");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_capital_aportado",
                table: "con_configuracion_sistema",
                column: "capital_aportado");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_resultados_acumulados",
                table: "con_configuracion_sistema",
                column: "resultados_acumulados");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_utilidad_perdida_ejercicio",
                table: "con_configuracion_sistema",
                column: "utilidad_perdida_ejercicio");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_sobrevaluaciones",
                table: "con_configuracion_sistema",
                column: "sobrevaluaciones");

            // Agregar restricciones de clave foránea
            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_acum_hist",
                table: "con_configuracion_sistema",
                column: "cuenta_util_acumulada_historica",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_ej_hist",
                table: "con_configuracion_sistema",
                column: "cuenta_util_ejercicio_historica",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_acum_hist",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_acumulada_historica",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_ej_hist",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_ejercicio_historica",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_acum_inf",
                table: "con_configuracion_sistema",
                column: "cuenta_util_acumulada_inflacion",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_ej_inf",
                table: "con_configuracion_sistema",
                column: "cuenta_util_ejercicio_inflacion",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_acum_inf",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_acumulada_inflacion",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_ej_inf",
                table: "con_configuracion_sistema",
                column: "cuenta_perdida_ejercicio_inflacion",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_cp_1",
                table: "con_configuracion_sistema",
                column: "activo_corto_plazo_1",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_cp_2",
                table: "con_configuracion_sistema",
                column: "activo_corto_plazo_2",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_lp_1",
                table: "con_configuracion_sistema",
                column: "activo_largo_plazo_1",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_lp_2",
                table: "con_configuracion_sistema",
                column: "activo_largo_plazo_2",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_cp_1",
                table: "con_configuracion_sistema",
                column: "pasivo_corto_plazo_1",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_cp_2",
                table: "con_configuracion_sistema",
                column: "pasivo_corto_plazo_2",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_lp_1",
                table: "con_configuracion_sistema",
                column: "pasivo_largo_plazo_1",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_lp_2",
                table: "con_configuracion_sistema",
                column: "pasivo_largo_plazo_2",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_capital",
                table: "con_configuracion_sistema",
                column: "pasivo_y_capital",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_capital",
                table: "con_configuracion_sistema",
                column: "capital_aportado",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_resultados",
                table: "con_configuracion_sistema",
                column: "resultados_acumulados",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_utilidad_perdida",
                table: "con_configuracion_sistema",
                column: "utilidad_perdida_ejercicio",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_sobre",
                table: "con_configuracion_sistema",
                column: "sobrevaluaciones",
                principalTable: "con_plan_cuentas",
                principalColumn: "account_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Eliminar restricciones de clave foránea
            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_acum_hist",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_ej_hist",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_acum_hist",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_ej_hist",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_acum_inf",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_util_ej_inf",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_acum_inf",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_perd_ej_inf",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_cp_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_cp_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_lp_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_activo_lp_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_cp_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_cp_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_lp_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_lp_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_pasivo_capital",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_capital",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_resultados",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_utilidad_perdida",
                table: "con_configuracion_sistema");

            migrationBuilder.DropForeignKey(
                name: "FK_con_configuracion_sistema_con_plan_cuentas_sobre",
                table: "con_configuracion_sistema");

            // Eliminar índices
            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_activo_corto_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_activo_corto_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_activo_largo_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_activo_largo_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_pasivo_corto_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_pasivo_corto_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_pasivo_largo_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_pasivo_largo_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_pasivo_y_capital",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_capital_aportado",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_resultados_acumulados",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_utilidad_perdida_ejercicio",
                table: "con_configuracion_sistema");

            migrationBuilder.DropIndex(
                name: "IX_con_configuracion_sistema_sobrevaluaciones",
                table: "con_configuracion_sistema");

            // Eliminar columnas
            migrationBuilder.DropColumn(
                name: "cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "activo_corto_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "activo_corto_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "activo_largo_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "activo_largo_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "pasivo_corto_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "pasivo_corto_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "pasivo_largo_plazo_1",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "pasivo_largo_plazo_2",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "pasivo_y_capital",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "capital_aportado",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "resultados_acumulados",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "utilidad_perdida_ejercicio",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "sobrevaluaciones",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "mostrar_orden",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "mostrar_percontra",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "titulo_estado_resultados",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "titulo_balance_general",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "descripcion_activo",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "descripcion_pasivo",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "descripcion_capital",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "descripcion_pasivo_capital",
                table: "con_configuracion_sistema");

            migrationBuilder.DropColumn(
                name: "descripcion_orden",
                table: "con_configuracion_sistema");
        }
    }
}
