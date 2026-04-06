using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class Contabilidad_01_TablasNuevas : Migration
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

            migrationBuilder.CreateTable(
                name: "con_activo_tipo",
                schema: "public",
                columns: table => new
                {
                    type_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    depreciation_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    useful_life_years = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_activo_tipo", x => x.type_id);
                    table.ForeignKey(
                        name: "FK_con_activo_tipo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_apertura_saldo",
                schema: "public",
                columns: table => new
                {
                    opening_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,9)", nullable: true, defaultValue: 1m),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_apertura_saldo", x => x.opening_id);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "con_balance_mensual",
                schema: "public",
                columns: table => new
                {
                    monthly_balance_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    month_number = table.Column<short>(type: "smallint", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    transaction_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_balance_mensual", x => x.monthly_balance_id);
                    table.CheckConstraint("ck_con_balance_mensual_month", "month_number >= 1 AND month_number <= 13");
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "con_tercero",
                schema: "public",
                columns: table => new
                {
                    third_party_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    is_customer = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_tercero", x => x.third_party_id);
                    table.ForeignKey(
                        name: "FK_con_tercero_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_tipo_transaccion",
                schema: "public",
                columns: table => new
                {
                    type_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_automatic = table.Column<bool>(type: "boolean", nullable: false),
                    allows_cost_center = table.Column<bool>(type: "boolean", nullable: false),
                    allows_third_party = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_tipo_transaccion", x => x.type_id);
                    table.ForeignKey(
                        name: "FK_con_tipo_transaccion_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_activo_fijo",
                schema: "public",
                columns: table => new
                {
                    asset_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    asset_type_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    acquisition_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    in_service_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    acquisition_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    salvage_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    useful_life_years = table.Column<short>(type: "smallint", nullable: false),
                    depreciation_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    accumulated_depreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    current_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    asset_account_id = table.Column<long>(type: "bigint", nullable: true),
                    depreciation_account_id = table.Column<long>(type: "bigint", nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_activo_fijo", x => x.asset_id);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_con_activo_tipo_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "public",
                        principalTable: "con_activo_tipo",
                        principalColumn: "type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_con_plan_cuentas_asset_account_id",
                        column: x => x.asset_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_con_plan_cuentas_depreciation_account_id",
                        column: x => x.depreciation_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_libro_iva",
                schema: "public",
                columns: table => new
                {
                    iva_register_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    third_party_id = table.Column<long>(type: "bigint", nullable: true),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    exempt_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    iva_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_libro_iva", x => x.iva_register_id);
                    table.ForeignKey(
                        name: "FK_con_libro_iva_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_libro_iva_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_libro_iva_con_tercero_third_party_id",
                        column: x => x.third_party_id,
                        principalSchema: "public",
                        principalTable: "con_tercero",
                        principalColumn: "third_party_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_deprecacion",
                schema: "public",
                columns: table => new
                {
                    depreciation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    month_number = table.Column<short>(type: "smallint", nullable: false),
                    depreciation_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    accumulated_to_date = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    poliza_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_deprecacion", x => x.depreciation_id);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_con_activo_fijo_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "con_activo_fijo",
                        principalColumn: "asset_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_con_partida_hdr_poliza_id",
                        column: x => x.poliza_id,
                        principalSchema: "public",
                        principalTable: "con_partida_hdr",
                        principalColumn: "poliza_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_asset_account_id",
                schema: "public",
                table: "con_activo_fijo",
                column: "asset_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_asset_type_id",
                schema: "public",
                table: "con_activo_fijo",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_company_id_code",
                schema: "public",
                table: "con_activo_fijo",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_depreciation_account_id",
                schema: "public",
                table: "con_activo_fijo",
                column: "depreciation_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_tipo_company_id_code",
                schema: "public",
                table: "con_activo_tipo",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_account_id",
                schema: "public",
                table: "con_apertura_saldo",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_company_id_period_id_account_id_cost_cen~",
                schema: "public",
                table: "con_apertura_saldo",
                columns: new[] { "company_id", "period_id", "account_id", "cost_center_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_cost_center_id",
                schema: "public",
                table: "con_apertura_saldo",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_period_id",
                schema: "public",
                table: "con_apertura_saldo",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_account_id",
                schema: "public",
                table: "con_balance_mensual",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_company_id_period_id_account_id_cost_ce~",
                schema: "public",
                table: "con_balance_mensual",
                columns: new[] { "company_id", "period_id", "account_id", "cost_center_id", "month_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_cost_center_id",
                schema: "public",
                table: "con_balance_mensual",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_period_id",
                schema: "public",
                table: "con_balance_mensual",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_asset_id_period_id_month_number",
                schema: "public",
                table: "con_deprecacion",
                columns: new[] { "asset_id", "period_id", "month_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_company_id",
                schema: "public",
                table: "con_deprecacion",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_period_id",
                schema: "public",
                table: "con_deprecacion",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_poliza_id",
                schema: "public",
                table: "con_deprecacion",
                column: "poliza_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_company_id",
                schema: "public",
                table: "con_libro_iva",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_period_id",
                schema: "public",
                table: "con_libro_iva",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_third_party_id",
                schema: "public",
                table: "con_libro_iva",
                column: "third_party_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_transaction_date",
                schema: "public",
                table: "con_libro_iva",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "IX_con_tercero_category",
                schema: "public",
                table: "con_tercero",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_con_tercero_company_id_code",
                schema: "public",
                table: "con_tercero",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_tipo_transaccion_company_id",
                schema: "public",
                table: "con_tipo_transaccion",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_tipo_transaccion_company_id_code",
                schema: "public",
                table: "con_tipo_transaccion",
                columns: new[] { "company_id", "code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "con_apertura_saldo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_balance_mensual",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_deprecacion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_libro_iva",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_tipo_transaccion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_activo_fijo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_tercero",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_activo_tipo",
                schema: "public");

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

