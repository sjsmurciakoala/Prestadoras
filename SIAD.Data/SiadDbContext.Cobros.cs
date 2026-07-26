using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Unificación cobranza F2 (2026-07-26): modelo adm_pago + adm_pago_aplicacion
// + adm_documento_secuencia (docs/PLAN_UNIFICACION_COBRANZA_2026-07.md).
// ConfigureCobrosModel es llamado desde OnModelCreatingPartial en
// SiadDbContext.Accounting.cs. Las 3 entidades son ICompanyScopedEntity →
// filtro global de tenant automático (SiadDbContext.Tenancy.cs).
public partial class SiadDbContext
{
    public virtual DbSet<adm_pago> adm_pagos { get; set; } = null!;
    public virtual DbSet<adm_pago_aplicacion> adm_pago_aplicaciones { get; set; } = null!;
    public virtual DbSet<adm_documento_secuencia> adm_documento_secuencias { get; set; } = null!;

    private void ConfigureCobrosModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<adm_pago>(entity =>
        {
            entity.HasKey(e => e.pago_id).HasName("adm_pago_pkey");
            entity.ToTable("adm_pago", "public");
            entity.Property(e => e.pago_id).UseIdentityAlwaysColumn();

            entity.HasIndex(e => new { e.company_id, e.numero_recibo },
                "uq_adm_pago_numero_recibo").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.cliente_clave, e.fecha },
                "ix_adm_pago_cliente");

            entity.Property(e => e.numero_recibo).HasMaxLength(30);
            entity.Property(e => e.cliente_clave).HasMaxLength(30);
            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.monto_total).HasColumnType("numeric(18,2)");
            entity.Property(e => e.forma_pago).HasMaxLength(20).HasDefaultValue("EFECTIVO");
            entity.Property(e => e.estado_id).HasDefaultValue((short)1);
            entity.Property(e => e.referencia_externa).HasMaxLength(100);
            entity.Property(e => e.motivo_reverso).HasMaxLength(300);
            entity.Property(e => e.usuario).HasMaxLength(100);
            entity.Property(e => e.creado_en).HasDefaultValueSql("now()");
        });

        modelBuilder.Entity<adm_pago_aplicacion>(entity =>
        {
            entity.HasKey(e => e.aplicacion_id).HasName("adm_pago_aplicacion_pkey");
            entity.ToTable("adm_pago_aplicacion", "public");
            entity.Property(e => e.aplicacion_id).UseIdentityAlwaysColumn();

            entity.HasIndex(e => new { e.company_id, e.pago_id },
                "ix_adm_pago_aplicacion_pago");

            entity.Property(e => e.monto_aplicado).HasColumnType("numeric(18,2)");

            entity.HasOne(d => d.pago)
                .WithMany(p => p.aplicaciones)
                .HasForeignKey(d => d.pago_id)
                .HasConstraintName("adm_pago_aplicacion_pago_id_fkey");
        });

        modelBuilder.Entity<adm_documento_secuencia>(entity =>
        {
            entity.HasKey(e => e.secuencia_id).HasName("adm_documento_secuencia_pkey");
            entity.ToTable("adm_documento_secuencia", "public");
            entity.Property(e => e.secuencia_id).UseIdentityAlwaysColumn();

            entity.HasIndex(e => new { e.company_id, e.tipo_documento, e.canal_id },
                "uq_adm_documento_secuencia").IsUnique();

            entity.Property(e => e.tipo_documento).HasMaxLength(30);
            entity.Property(e => e.prefijo).HasMaxLength(10).HasDefaultValue(string.Empty);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });
    }
}
