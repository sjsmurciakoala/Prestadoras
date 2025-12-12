using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameConfigurationColumnsForConciseness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ultima_depreciacion",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fecha_ini_ejer");

            migrationBuilder.RenameColumn(
                name: "titulo_estado_resultados",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "tit_est_result");

            migrationBuilder.RenameColumn(
                name: "titulo_balance_general",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "tit_balance_gral");

            migrationBuilder.RenameColumn(
                name: "symbol_saldo_acreedor",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "sym_acreedor");

            migrationBuilder.RenameColumn(
                name: "separador_codigo",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "sep_codigo");

            migrationBuilder.RenameColumn(
                name: "monto_maximo",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "mto_maximo");

            migrationBuilder.RenameColumn(
                name: "meses_calculados",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "meses_calc");

            migrationBuilder.RenameColumn(
                name: "frecuencia_depreciacion",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "frec_deprec");

            migrationBuilder.RenameColumn(
                name: "formato_cuentas",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fmt_ctas");

            migrationBuilder.RenameColumn(
                name: "formato_centros",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fmt_centros");

            migrationBuilder.RenameColumn(
                name: "fecha_inicio_ejercicio",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fecha_fin_ejer");

            migrationBuilder.RenameColumn(
                name: "fecha_fin_ejercicio",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fec_ult_deprec");

            migrationBuilder.RenameColumn(
                name: "descripcion_pasivo_capital",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "desc_pasiv_cap");

            migrationBuilder.RenameColumn(
                name: "descripcion_orden",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "desc_orden");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_ejercicio_inflacion",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_util_ejer_inf");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_ejercicio_historica",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_util_ejer_hist");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_acumulada_inflacion",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_util_acum_inf");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_util_acumulada_historica",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_util_acum_hist");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_ejercicio_inflacion",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_perd_ejer_inf");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_ejercicio_historica",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_perd_ejer_hist");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_acumulada_inflacion",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_perd_acum_inf");

            migrationBuilder.RenameColumn(
                name: "codigo_cuenta_perdida_acumulada_historica",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "cod_perd_acum_hist");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "tit_est_result",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "titulo_estado_resultados");

            migrationBuilder.RenameColumn(
                name: "tit_balance_gral",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "titulo_balance_general");

            migrationBuilder.RenameColumn(
                name: "sym_acreedor",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "symbol_saldo_acreedor");

            migrationBuilder.RenameColumn(
                name: "sep_codigo",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "separador_codigo");

            migrationBuilder.RenameColumn(
                name: "mto_maximo",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "monto_maximo");

            migrationBuilder.RenameColumn(
                name: "meses_calc",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "meses_calculados");

            migrationBuilder.RenameColumn(
                name: "frec_deprec",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "frecuencia_depreciacion");

            migrationBuilder.RenameColumn(
                name: "fmt_ctas",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "formato_cuentas");

            migrationBuilder.RenameColumn(
                name: "fmt_centros",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "formato_centros");

            migrationBuilder.RenameColumn(
                name: "fecha_ini_ejer",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "ultima_depreciacion");

            migrationBuilder.RenameColumn(
                name: "fecha_fin_ejer",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fecha_inicio_ejercicio");

            migrationBuilder.RenameColumn(
                name: "fec_ult_deprec",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "fecha_fin_ejercicio");

            migrationBuilder.RenameColumn(
                name: "desc_pasiv_cap",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "descripcion_pasivo_capital");

            migrationBuilder.RenameColumn(
                name: "desc_orden",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "descripcion_orden");

            migrationBuilder.RenameColumn(
                name: "cod_util_ejer_inf",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_ejercicio_inflacion");

            migrationBuilder.RenameColumn(
                name: "cod_util_ejer_hist",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_ejercicio_historica");

            migrationBuilder.RenameColumn(
                name: "cod_util_acum_inf",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_acumulada_inflacion");

            migrationBuilder.RenameColumn(
                name: "cod_util_acum_hist",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_util_acumulada_historica");

            migrationBuilder.RenameColumn(
                name: "cod_perd_ejer_inf",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_ejercicio_inflacion");

            migrationBuilder.RenameColumn(
                name: "cod_perd_ejer_hist",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_ejercicio_historica");

            migrationBuilder.RenameColumn(
                name: "cod_perd_acum_inf",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_acumulada_inflacion");

            migrationBuilder.RenameColumn(
                name: "cod_perd_acum_hist",
                schema: "public",
                table: "con_configuracion_sistema",
                newName: "codigo_cuenta_perdida_acumulada_historica");

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
        }
    }
}
