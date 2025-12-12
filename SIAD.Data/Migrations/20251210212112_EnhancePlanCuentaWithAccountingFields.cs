using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnhancePlanCuentaWithAccountingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "minimo",
                table: "tarifas_contador",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "maximo",
                table: "tarifas_contador",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "recibo",
                table: "pagos_bancos",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "sel",
                table: "historicomedicion",
                type: "numeric(1)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1,0)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "revision",
                table: "historicomedicion",
                type: "numeric(1)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1,0)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "numeroord",
                table: "historicomedicion",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "mes",
                table: "historicomedicion",
                type: "numeric(2)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,0)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "lect_ant",
                table: "historicomedicion",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "lect_act",
                table: "historicomedicion",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "lec_prom",
                table: "historicomedicion",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "consumoant",
                table: "historicomedicion",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "consumo",
                table: "historicomedicion",
                type: "numeric(12)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12,0)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ano",
                table: "historicomedicion",
                type: "numeric(4)",
                precision: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,0)",
                oldPrecision: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "sep2",
                table: "historialmes",
                type: "numeric(1)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1,0)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "sep",
                table: "historialmes",
                type: "numeric(1)",
                precision: 1,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1,0)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "mes",
                table: "historialmes",
                type: "numeric(2)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2,0)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ano",
                table: "historialmes",
                type: "numeric(4)",
                precision: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4,0)",
                oldPrecision: 4);

            migrationBuilder.AlterColumn<bool>(
                name: "allows_posting",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AddColumn<bool>(
                name: "allows_amount",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "allows_bank",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_budget",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_cost_center",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_multi_currency",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "allows_third",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "budget_amount",
                schema: "public",
                table: "con_plan_cuentas",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_reference",
                schema: "public",
                table: "con_plan_cuentas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_tax_base",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "short_description",
                schema: "public",
                table: "con_plan_cuentas",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "allows_amount",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "allows_bank",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "allows_budget",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "allows_cost_center",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "allows_multi_currency",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "allows_third",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "budget_amount",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "external_reference",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "is_tax_base",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.DropColumn(
                name: "short_description",
                schema: "public",
                table: "con_plan_cuentas");

            migrationBuilder.AlterColumn<decimal>(
                name: "minimo",
                table: "tarifas_contador",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "maximo",
                table: "tarifas_contador",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "recibo",
                table: "pagos_bancos",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "sel",
                table: "historicomedicion",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "revision",
                table: "historicomedicion",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "numeroord",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "mes",
                table: "historicomedicion",
                type: "numeric(2,0)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "lect_ant",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "lect_act",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "lec_prom",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "consumoant",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "consumo",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ano",
                table: "historicomedicion",
                type: "numeric(4,0)",
                precision: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4)",
                oldPrecision: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "sep2",
                table: "historialmes",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "sep",
                table: "historialmes",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "mes",
                table: "historialmes",
                type: "numeric(2,0)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ano",
                table: "historialmes",
                type: "numeric(4,0)",
                precision: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4)",
                oldPrecision: 4);

            migrationBuilder.AlterColumn<bool>(
                name: "allows_posting",
                schema: "public",
                table: "con_plan_cuentas",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);
        }
    }
}
