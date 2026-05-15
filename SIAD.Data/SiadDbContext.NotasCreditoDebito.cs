using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para el modelo NC/ND V3 (2026-05-14, Sprint 3).
// Vive aquí para no contaminar el cuerpo scaffolded de SiadDbContext.cs.
// Llamado desde ConfigureSarComplianceModel (SiadDbContext.SarCompliance.cs).
public partial class SiadDbContext
{
    public virtual DbSet<cfg_motivo_aumento> cfg_motivo_aumentos { get; set; } = null!;
    public virtual DbSet<adm_nota_credito> adm_nota_creditos { get; set; } = null!;
    public virtual DbSet<adm_nota_credito_detalle> adm_nota_credito_detalles { get; set; } = null!;
    public virtual DbSet<adm_nota_debito> adm_nota_debitos { get; set; } = null!;
    public virtual DbSet<adm_nota_debito_detalle> adm_nota_debito_detalles { get; set; } = null!;

    private void ConfigureNotasCreditoDebitoModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_motivo_aumento>(entity =>
        {
            entity.HasKey(e => e.motivo_aumento_id);
            entity.ToTable("cfg_motivo_aumento", "public");
            entity.HasIndex(e => e.codigo).IsUnique();
            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(150);
            entity.Property(e => e.activo).HasDefaultValue(true);
        });

        modelBuilder.Entity<adm_nota_credito>(entity =>
        {
            entity.HasKey(e => e.nota_credito_id);
            entity.ToTable("adm_nota_credito", "public");
            entity.HasIndex(e => new { e.company_id, e.numero_documento }, "uq_adm_nota_credito_company_numero").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.factura_origen_id }, "ix_adm_nota_credito_factura_origen");

            entity.Property(e => e.establecimiento_codigo).HasMaxLength(3).HasDefaultValue("000");
            entity.Property(e => e.numero_documento).HasMaxLength(80);
            entity.Property(e => e.leyenda_cai_rango).HasMaxLength(200);
            entity.Property(e => e.rtn_emisor).HasMaxLength(20);
            entity.Property(e => e.razon_social_emisor).HasMaxLength(200);
            entity.Property(e => e.direccion_emisor).HasMaxLength(300);
            entity.Property(e => e.rtn_receptor).HasMaxLength(20);
            entity.Property(e => e.razon_social_receptor).HasMaxLength(200);
            entity.Property(e => e.direccion_receptor).HasMaxLength(300);
            entity.Property(e => e.factura_origen_numero).HasMaxLength(80);
            entity.Property(e => e.factura_origen_cai).HasMaxLength(100);
            entity.Property(e => e.motivo_detalle).HasMaxLength(500);
            entity.Property(e => e.usuario_emisor).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
            entity.Property(e => e.monto_disminuir).HasColumnType("numeric(18,4)");
            entity.Property(e => e.isv_disminuir).HasColumnType("numeric(18,4)");
            entity.Property(e => e.total_nota).HasColumnType("numeric(18,4)");

            entity.HasMany(e => e.detalles)
                .WithOne(d => d.nota_credito)
                .HasForeignKey(d => d.nota_credito_id)
                .HasConstraintName("fk_adm_nc_detalle_nc")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<adm_nota_credito_detalle>(entity =>
        {
            entity.HasKey(e => e.nota_credito_detalle_id);
            entity.ToTable("adm_nota_credito_detalle", "public");
            entity.HasIndex(e => e.nota_credito_id, "ix_adm_nc_detalle_nc");
            entity.Property(e => e.servicio_codigo).HasMaxLength(50);
            entity.Property(e => e.descripcion).HasMaxLength(300);
            entity.Property(e => e.cuenta_contable_codigo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasColumnType("numeric(18,4)");
            entity.Property(e => e.monto_unitario).HasColumnType("numeric(18,4)");
            entity.Property(e => e.monto_total).HasColumnType("numeric(18,4)");
            entity.Property(e => e.isv_monto).HasColumnType("numeric(18,4)");
        });

        modelBuilder.Entity<adm_nota_debito>(entity =>
        {
            entity.HasKey(e => e.nota_debito_id);
            entity.ToTable("adm_nota_debito", "public");
            entity.HasIndex(e => new { e.company_id, e.numero_documento }, "uq_adm_nota_debito_company_numero").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.factura_origen_id }, "ix_adm_nota_debito_factura_origen");

            entity.Property(e => e.establecimiento_codigo).HasMaxLength(3).HasDefaultValue("000");
            entity.Property(e => e.numero_documento).HasMaxLength(80);
            entity.Property(e => e.leyenda_cai_rango).HasMaxLength(200);
            entity.Property(e => e.rtn_emisor).HasMaxLength(20);
            entity.Property(e => e.razon_social_emisor).HasMaxLength(200);
            entity.Property(e => e.direccion_emisor).HasMaxLength(300);
            entity.Property(e => e.rtn_receptor).HasMaxLength(20);
            entity.Property(e => e.razon_social_receptor).HasMaxLength(200);
            entity.Property(e => e.direccion_receptor).HasMaxLength(300);
            entity.Property(e => e.factura_origen_numero).HasMaxLength(80);
            entity.Property(e => e.factura_origen_cai).HasMaxLength(100);
            entity.Property(e => e.motivo_detalle).HasMaxLength(500);
            entity.Property(e => e.usuario_emisor).HasMaxLength(100);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
            entity.Property(e => e.monto_aumentar).HasColumnType("numeric(18,4)");
            entity.Property(e => e.isv_aumentar).HasColumnType("numeric(18,4)");
            entity.Property(e => e.total_nota).HasColumnType("numeric(18,4)");

            entity.HasOne(e => e.motivo_aumento)
                .WithMany(p => p.adm_nota_debitos)
                .HasForeignKey(e => e.motivo_aumento_id)
                .HasConstraintName("fk_adm_nota_debito_motivo");

            entity.HasMany(e => e.detalles)
                .WithOne(d => d.nota_debito)
                .HasForeignKey(d => d.nota_debito_id)
                .HasConstraintName("fk_adm_nd_detalle_nd")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<adm_nota_debito_detalle>(entity =>
        {
            entity.HasKey(e => e.nota_debito_detalle_id);
            entity.ToTable("adm_nota_debito_detalle", "public");
            entity.HasIndex(e => e.nota_debito_id, "ix_adm_nd_detalle_nd");
            entity.Property(e => e.servicio_codigo).HasMaxLength(50);
            entity.Property(e => e.descripcion).HasMaxLength(300);
            entity.Property(e => e.cuenta_contable_codigo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasColumnType("numeric(18,4)");
            entity.Property(e => e.monto_unitario).HasColumnType("numeric(18,4)");
            entity.Property(e => e.monto_total).HasColumnType("numeric(18,4)");
            entity.Property(e => e.isv_monto).HasColumnType("numeric(18,4)");
        });
    }
}
