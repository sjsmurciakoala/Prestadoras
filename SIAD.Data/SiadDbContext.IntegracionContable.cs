using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Integración Contable ↔ Comercial — Fase 1 (plan 2026-07-02).
// Esquema en Database/ddl_v3/20260702_ci_fase1_integracion_config.sql.
public partial class SiadDbContext
{
    public virtual DbSet<con_integracion_config> con_integracion_configs { get; set; } = null!;

    public virtual DbSet<con_integracion_cuenta> con_integracion_cuentas { get; set; } = null!;

    public virtual DbSet<con_partida_pendiente> con_partida_pendientes { get; set; } = null!;

    public virtual DbSet<con_integracion_asiento> con_integracion_asientos { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigureIntegracionContableModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<con_integracion_config>(entity =>
        {
            entity.HasKey(e => e.config_id);
            entity.ToTable("con_integracion_config", "public");
            entity.HasIndex(e => e.company_id).IsUnique();

            entity.Property(e => e.config_id).ValueGeneratedOnAdd();
            entity.Property(e => e.modo_ventas).HasMaxLength(30).HasDefaultValue("GENERAL");
            entity.Property(e => e.modo_cxc).HasMaxLength(30).HasDefaultValue("GENERAL");
            entity.Property(e => e.encolar_sin_periodo).HasDefaultValue(true);
            entity.Property(e => e.activo_facturacion).HasDefaultValue(false);
            entity.Property(e => e.activo_caja).HasDefaultValue(false);
            entity.Property(e => e.activo_bancos).HasDefaultValue(false);
            entity.Property(e => e.activo_notas).HasDefaultValue(false);
            entity.Property(e => e.activo_miscelaneos).HasDefaultValue(false);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_con_integracion_config_modo_ventas",
                "modo_ventas IN ('GENERAL', 'POR_SERVICIO', 'POR_SERVICIO_CATEGORIA')");
            entity.HasCheckConstraint(
                "ck_con_integracion_config_modo_cxc",
                "modo_cxc IN ('GENERAL', 'POR_SERVICIO', 'POR_SERVICIO_CATEGORIA')");

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<con_integracion_cuenta>(entity =>
        {
            entity.HasKey(e => e.integracion_cuenta_id);
            entity.ToTable("con_integracion_cuenta", "public");
            // La unicidad real es el índice de expresiones ux_con_integracion_cuenta_dims
            // (COALESCE sobre dimensiones NULL) definido en el script SQL.
            entity.HasIndex(e => new { e.company_id, e.uso });

            entity.Property(e => e.integracion_cuenta_id).ValueGeneratedOnAdd();
            entity.Property(e => e.uso).HasMaxLength(30);
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_con_integracion_cuenta_uso",
                "uso IN ('CXC', 'INGRESO', 'CAJA', 'BANCO_DEFAULT', 'ISV', 'DESCUENTO', " +
                "'RECARGO_MORA', 'PREVISION_INCOBRABLE', 'GASTO_INCOBRABLE', " +
                "'RESULTADO_EJERCICIO', 'RESULTADO_ACUMULADO', 'DEVOLUCION_NC', 'TRANSITORIA')");
            entity.HasCheckConstraint(
                "ck_con_integracion_cuenta_dims",
                "servicio_id IS NOT NULL OR (categoria_servicio_id IS NULL AND con_medicion IS NULL)");

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.account)
                .WithMany()
                .HasForeignKey(e => e.account_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<con_integracion_asiento>(entity =>
        {
            entity.HasKey(e => e.integracion_asiento_id);
            entity.ToTable("con_integracion_asiento", "public");
            entity.HasIndex(e => new { e.company_id, e.module }).IsUnique();

            entity.Property(e => e.integracion_asiento_id).ValueGeneratedOnAdd();
            entity.Property(e => e.module).HasMaxLength(30);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_con_integracion_asiento_module",
                "module IN ('FACTURACION', 'CAJA', 'BANCOS', 'NOTAS', 'MISCELANEOS')");

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<con_diario>()
                .WithMany()
                .HasForeignKey(e => e.journal_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<con_tipo_transaccion>()
                .WithMany()
                .HasForeignKey(e => new { e.company_id, e.type_id })
                .HasPrincipalKey(t => new { t.company_id, t.type_id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<con_partida_pendiente>(entity =>
        {
            entity.HasKey(e => e.partida_pendiente_id);
            entity.ToTable("con_partida_pendiente", "public");
            entity.HasIndex(e => new { e.company_id, e.status_id });
            entity.HasIndex(e => new { e.company_id, e.module, e.origen_tipo, e.origen_id });

            entity.Property(e => e.partida_pendiente_id).ValueGeneratedOnAdd();
            entity.Property(e => e.module).HasMaxLength(30);
            entity.Property(e => e.origen_tipo).HasMaxLength(50);
            entity.Property(e => e.origen_referencia).HasMaxLength(120);
            entity.Property(e => e.descripcion).HasMaxLength(500);
            entity.Property(e => e.payload).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
            entity.Property(e => e.motivo).HasMaxLength(200).HasDefaultValue("SIN_PERIODO_ABIERTO");
            entity.Property(e => e.status_id).HasDefaultValue((short)1);
            entity.Property(e => e.intentos).HasDefaultValue(0);
            entity.Property(e => e.ultimo_error).HasColumnType("text");
            entity.Property(e => e.procesada_by).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_con_partida_pendiente_status",
                "status_id IN (1, 2, 3)");

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<con_partida_hdr>()
                .WithMany()
                .HasForeignKey(e => e.poliza_id)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
