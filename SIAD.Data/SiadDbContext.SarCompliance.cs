using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para los catálogos y entidades nuevas del paquete
// SAR-Compliance (2026-05-07). Vive aquí para no contaminar el cuerpo
// scaffolded de SiadDbContext.cs cada vez que se regeneran entidades.
public partial class SiadDbContext
{
    public virtual DbSet<adm_cai_facturacion> adm_cai_facturacions { get; set; } = null!;
    public virtual DbSet<cfg_tipo_documento_fiscal> cfg_tipo_documento_fiscals { get; set; } = null!;
    public virtual DbSet<cfg_motivo_anulacion> cfg_motivo_anulacions { get; set; } = null!;
    public virtual DbSet<cfg_estado_documento_fiscal> cfg_estado_documento_fiscals { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigureSarComplianceModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_tipo_documento_fiscal>(entity =>
        {
            entity.HasKey(e => e.tipo_documento_fiscal_id);
            entity.ToTable("cfg_tipo_documento_fiscal", "public");
            entity.HasIndex(e => e.codigo).IsUnique();
            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.descripcion).HasMaxLength(80);
        });

        modelBuilder.Entity<cfg_motivo_anulacion>(entity =>
        {
            entity.HasKey(e => e.motivo_anulacion_id);
            entity.ToTable("cfg_motivo_anulacion", "public");
            entity.HasIndex(e => e.codigo).IsUnique();
            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(150);
        });

        modelBuilder.Entity<cfg_estado_documento_fiscal>(entity =>
        {
            entity.HasKey(e => e.estado_id);
            entity.ToTable("cfg_estado_documento_fiscal", "public");
            entity.HasIndex(e => e.codigo).IsUnique();
            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(80);
        });

        modelBuilder.Entity<adm_cai_facturacion>(entity =>
        {
            entity.HasKey(e => e.cai_id).HasName("adm_cai_facturacion_pkey");
            entity.ToTable("adm_cai_facturacion", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo_cai }, "uq_adm_cai_facturacion_company_codigo").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.cai_id }, "uq_adm_cai_facturacion_company_cai_id").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_adm_cai_facturacion_company");
            entity.HasIndex(e => new { e.company_id, e.tipo_documento_fiscal_id, e.status_id }, "ix_adm_cai_facturacion_tipo_doc");

            entity.Property(e => e.codigo_cai).HasMaxLength(100);
            entity.Property(e => e.prefijo_documento).HasMaxLength(30);
            entity.Property(e => e.observaciones).HasMaxLength(300);
            entity.Property(e => e.leyenda_rango).HasMaxLength(200);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(e => e.tipo_documento_fiscal)
                .WithMany(p => p.adm_cai_facturacions)
                .HasForeignKey(e => e.tipo_documento_fiscal_id)
                .HasConstraintName("fk_adm_cai_facturacion_tipo_doc");
        });

        // Las entidades alteradas (factura, factura_detalle, transaccion_abonado,
        // historicomedicion, maestro_medidor) ya tienen su mapeo en SiadDbContext.cs
        // generado por scaffold; solo agregamos la propiedad company_id en la
        // entidad partial. EF la detecta por convención al ser tipo bigint con
        // mismo nombre que la columna.

        // NC/ND V3 (Sprint 3, 2026-05-14): modelo de Notas de Crédito y Débito.
        ConfigureNotasCreditoDebitoModel(modelBuilder);

        // Mantenimientos (Sprint 3, 2026-05-14): recargo mora + ajustes tarifarios.
        ConfigureMantenimientosModel(modelBuilder);
    }
}
