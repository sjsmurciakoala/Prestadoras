using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SIAD.Data;

#nullable disable

namespace SIAD.Data.Migrations;

/// <summary>
/// Añade company_id a tablas de clientes para soportar multitenancy.
/// Migración manual y acotada para no arrastrar cambios ajenos al módulo.
/// </summary>
[DbContext(typeof(SiadDbContext))]
[Migration("20260119000001_AddCompanyToClientes")]
public partial class AddCompanyToClientes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "company_id",
            table: "cliente_maestro",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.AddColumn<long>(
            name: "company_id",
            table: "cliente_detalle",
            type: "bigint",
            nullable: false,
            defaultValue: 1L);

        migrationBuilder.CreateIndex(
            name: "ix_cliente_maestro_company_clave",
            table: "cliente_maestro",
            columns: new[] { "company_id", "maestro_cliente_clave" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_cliente_maestro_company_id",
            table: "cliente_maestro",
            column: "company_id");

        migrationBuilder.CreateIndex(
            name: "ix_cliente_detalle_company_id",
            table: "cliente_detalle",
            column: "company_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ix_cliente_detalle_company_id",
            table: "cliente_detalle");

        migrationBuilder.DropIndex(
            name: "ix_cliente_maestro_company_id",
            table: "cliente_maestro");

        migrationBuilder.DropIndex(
            name: "ix_cliente_maestro_company_clave",
            table: "cliente_maestro");

        migrationBuilder.DropColumn(
            name: "company_id",
            table: "cliente_detalle");

        migrationBuilder.DropColumn(
            name: "company_id",
            table: "cliente_maestro");
    }
}
