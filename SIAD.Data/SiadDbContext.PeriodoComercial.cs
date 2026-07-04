using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Período comercial — Fase 7 (plan 2026-07-02).
// Esquema en Database/ddl_v3/20260704_ci_fase7_periodo_cierre.sql.
// adm_periodo_comercial(_ciclo) reemplazan a historialmes como fuente de
// verdad del mes comercial; el espejo → historialmes lo mantiene un trigger
// de BD durante la transición (el WS de lectores la lee inline y no cambia).
public partial class SiadDbContext
{
    public virtual DbSet<adm_periodo_comercial> adm_periodo_comercials { get; set; } = null!;

    public virtual DbSet<adm_periodo_comercial_ciclo> adm_periodo_comercial_ciclos { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigurePeriodoComercialModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<adm_periodo_comercial>(entity =>
        {
            entity.HasKey(e => e.periodo_comercial_id);
            entity.ToTable("adm_periodo_comercial", "public");
            entity.HasIndex(e => new { e.company_id, e.anio, e.mes }).IsUnique();
            // AK para la FK compuesta tenant-safe de los ciclos
            entity.HasAlternateKey(e => new { e.company_id, e.periodo_comercial_id });

            entity.Property(e => e.periodo_comercial_id).ValueGeneratedOnAdd();
            entity.Property(e => e.status_id).HasDefaultValue((short)1);
            entity.Property(e => e.abierto_por).HasMaxLength(100);
            entity.Property(e => e.cerrado_por).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_adm_periodo_comercial_status",
                "status_id IN (1, 2)");
            entity.HasCheckConstraint(
                "ck_adm_periodo_comercial_mes",
                "mes BETWEEN 1 AND 12");
        });

        modelBuilder.Entity<adm_periodo_comercial_ciclo>(entity =>
        {
            entity.HasKey(e => e.periodo_ciclo_id);
            entity.ToTable("adm_periodo_comercial_ciclo", "public");
            entity.HasIndex(e => new { e.company_id, e.periodo_comercial_id, e.ciclo_codigo }).IsUnique();

            entity.Property(e => e.periodo_ciclo_id).ValueGeneratedOnAdd();
            entity.Property(e => e.ciclo_codigo).HasMaxLength(2);
            entity.Property(e => e.status_id).HasDefaultValue((short)1);
            entity.Property(e => e.abierto_por).HasMaxLength(100);
            entity.Property(e => e.cerrado_por).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_adm_periodo_comercial_ciclo_status",
                "status_id IN (1, 2)");

            entity.HasOne(e => e.periodo)
                .WithMany(p => p.ciclos)
                .HasForeignKey(e => new { e.company_id, e.periodo_comercial_id })
                .HasPrincipalKey(p => new { p.company_id, p.periodo_comercial_id })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
