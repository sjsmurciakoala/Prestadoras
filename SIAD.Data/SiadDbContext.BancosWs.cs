using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Integración Contable ↔ Comercial — Fase 8: WS bancario (plan 2026-07-02).
// Esquema en Database/ddl_v3/20260704_ci_fase8_ws_bancario.sql.
public partial class SiadDbContext
{
    public virtual DbSet<ban_ws_credencial> ban_ws_credencials { get; set; } = null!;

    public virtual DbSet<ban_ws_pago> ban_ws_pagos { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigureBancosWsModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ban_ws_credencial>(entity =>
        {
            entity.HasKey(e => e.credencial_id);
            entity.ToTable("ban_ws_credencial", "public");
            entity.HasIndex(e => new { e.company_id, e.banco }).IsUnique();

            entity.Property(e => e.credencial_id).ValueGeneratedOnAdd();
            entity.Property(e => e.banco).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.llave).HasMaxLength(64);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Restrict);

            // FK compuesta tenant-safe (AK uq_ban_cuenta_company_id del script F8)
            entity.HasOne<ban_cuenta>()
                .WithMany()
                .HasForeignKey(e => new { e.company_id, e.banco_cuenta_id })
                .HasPrincipalKey(c => new { c.company_id, c.banco_cuenta_id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ban_ws_pago>(entity =>
        {
            entity.HasKey(e => e.pago_id);
            entity.ToTable("ban_ws_pago", "public");
            entity.HasIndex(e => new { e.company_id, e.referencia }).IsUnique();
            entity.HasIndex(e => new { e.company_id, e.clave });

            entity.Property(e => e.pago_id).ValueGeneratedOnAdd();
            entity.Property(e => e.banco).HasMaxLength(10);
            entity.Property(e => e.referencia).HasMaxLength(40);
            entity.Property(e => e.clave).HasMaxLength(20);
            entity.Property(e => e.tipo).HasMaxLength(1).HasDefaultValue("S");
            entity.Property(e => e.monto).HasColumnType("numeric(18,2)");
            entity.Property(e => e.sucursal).HasMaxLength(30);
            entity.Property(e => e.cajero).HasMaxLength(20);
            entity.Property(e => e.status_id).HasDefaultValue((short)1);
            entity.Property(e => e.reversado_por).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_ban_ws_pago_status",
                "status_id IN (1, 2)");
            entity.HasCheckConstraint(
                "ck_ban_ws_pago_tipo",
                "tipo IN ('S', 'O')");

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<ban_cuenta>()
                .WithMany()
                .HasForeignKey(e => new { e.company_id, e.banco_cuenta_id })
                .HasPrincipalKey(c => new { c.company_id, c.banco_cuenta_id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
