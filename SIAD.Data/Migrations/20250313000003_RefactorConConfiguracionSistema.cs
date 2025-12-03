using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorConConfiguracionSistema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Renombrar columnas de IDs a códigos de cuenta (cuentas de utilidad)
            migrationBuilder.RenameColumn(
                name: "cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_acumulada_historica");

            migrationBuilder.RenameColumn(
                name: "cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_ejercicio_historica");

            migrationBuilder.RenameColumn(
                name: "cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_acumulada_historica");

            migrationBuilder.RenameColumn(
                name: "cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_ejercicio_historica");

            migrationBuilder.RenameColumn(
                name: "cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_acumulada_inflacion");

            migrationBuilder.RenameColumn(
                name: "cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_ejercicio_inflacion");

            migrationBuilder.RenameColumn(
                name: "cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_acumulada_inflacion");

            migrationBuilder.RenameColumn(
                name: "cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_ejercicio_inflacion");

            // Eliminar columnas de balance sheet (ahora en tabla separada)
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

            // Cambiar tipo de datos: de bigint a varchar(30) para códigos de cuenta
            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(long),
                oldType: "bigint",
                oldNullable: true);

            // Eliminar foreign keys antiguas (ya no existen)
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

            // Eliminar índices antiguos
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revertir cambios (restaurar columnas antiguas)
            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema",
                newName: "cuenta_util_acumulada_historica");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema",
                newName: "cuenta_util_ejercicio_historica");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema",
                newName: "cuenta_perdida_acumulada_historica");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema",
                newName: "cuenta_perdida_ejercicio_historica");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema",
                newName: "cuenta_util_acumulada_inflacion");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                newName: "cuenta_util_ejercicio_inflacion");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema",
                newName: "cuenta_perdida_acumulada_inflacion");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                newName: "cuenta_perdida_ejercicio_inflacion");

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_util_acumulada_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_util_ejercicio_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_perdida_acumulada_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_perdida_ejercicio_historica",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_util_acumulada_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_util_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_perdida_acumulada_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<long>(
                name: "cuenta_perdida_ejercicio_inflacion",
                table: "con_configuracion_sistema",
                type: "bigint",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            // Agregar columnas de balance sheet
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

            // Recrear foreign keys
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
    }
}
