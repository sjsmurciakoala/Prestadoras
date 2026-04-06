using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SIAD.Data;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    [DbContext(typeof(SiadDbContext))]
    [Migration("20260115123000_AddAperturaCentroCostoAndLegacyCostCenterFields")]
    public partial class AddAperturaCentroCostoAndLegacyCostCenterFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "allows_movement",
                schema: "public",
                table: "con_centro_costo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "end_date",
                schema: "public",
                table: "con_centro_costo",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddColumn<bool>(
                name: "is_periodic",
                schema: "public",
                table: "con_centro_costo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "legacy_key_cost",
                schema: "public",
                table: "con_centro_costo",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_notes",
                schema: "public",
                table: "con_centro_costo",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "legacy_parent_code",
                schema: "public",
                table: "con_centro_costo",
                type: "character varying(24)",
                maxLength: 24,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "legacy_status",
                schema: "public",
                table: "con_centro_costo",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<short>(
                name: "legacy_type_trans",
                schema: "public",
                table: "con_centro_costo",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.AddColumn<DateTime>(
                name: "start_date",
                schema: "public",
                table: "con_centro_costo",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()");

            migrationBuilder.AddCheckConstraint(
                name: "ck_con_centro_costo_legacy_type_trans",
                schema: "public",
                table: "con_centro_costo",
                sql: "legacy_type_trans >= 0 AND legacy_type_trans <= 5");

            migrationBuilder.CreateTable(
                name: "con_apertura_centro_costo",
                schema: "public",
                columns: table => new
                {
                    opening_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo_transaccion = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
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
                    table.PrimaryKey("PK_con_apertura_centro_costo", x => x.opening_id);
                    table.CheckConstraint(
                        "ck_con_apertura_centro_costo_tipo_transaccion",
                        "tipo_transaccion >= 0 AND tipo_transaccion <= 5");
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_account_id",
                schema: "public",
                table: "con_apertura_centro_costo",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_company_id_period_id_account_id_cost_center_id_tipo_transaccion",
                schema: "public",
                table: "con_apertura_centro_costo",
                columns: new[] { "company_id", "period_id", "account_id", "cost_center_id", "tipo_transaccion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_cost_center_id",
                schema: "public",
                table: "con_apertura_centro_costo",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_period_id",
                schema: "public",
                table: "con_apertura_centro_costo",
                column: "period_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "con_apertura_centro_costo",
                schema: "public");

            migrationBuilder.DropCheckConstraint(
                name: "ck_con_centro_costo_legacy_type_trans",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "allows_movement",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "end_date",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "is_periodic",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "legacy_key_cost",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "legacy_notes",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "legacy_parent_code",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "legacy_status",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "legacy_type_trans",
                schema: "public",
                table: "con_centro_costo");

            migrationBuilder.DropColumn(
                name: "start_date",
                schema: "public",
                table: "con_centro_costo");
        }
    }
}
