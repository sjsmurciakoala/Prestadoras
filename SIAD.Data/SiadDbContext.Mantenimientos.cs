using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para el módulo Mantenimientos (Sprint 3, 2026-05-14):
// recargo por mora y ajustes tarifarios. Estas tablas ya existían en la BD
// (motor tarifario 16-abr + cfg_recargo_mora 14-may) pero nunca se habían
// scaffoldeado. Se mapean aquí como entidades para gestionarlas vía EF Core.
// Llamado desde ConfigureSarComplianceModel (SiadDbContext.SarCompliance.cs).
public partial class SiadDbContext
{
    public virtual DbSet<cfg_recargo_mora> cfg_recargo_moras { get; set; } = null!;
    public virtual DbSet<adm_tipo_ajuste_tarifario> adm_tipo_ajuste_tarifarios { get; set; } = null!;
    public virtual DbSet<adm_cuadro_tarifario> adm_cuadro_tarifarios { get; set; } = null!;
    public virtual DbSet<adm_ajuste_tarifario> adm_ajuste_tarifarios { get; set; } = null!;

    private void ConfigureMantenimientosModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_recargo_mora>(entity =>
        {
            entity.HasKey(e => e.company_id).HasName("cfg_recargo_mora_pkey");
            entity.ToTable("cfg_recargo_mora", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
            entity.Property(e => e.tasa_mensual).HasColumnType("numeric(9,6)");
            entity.Property(e => e.descripcion).HasMaxLength(200);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<adm_tipo_ajuste_tarifario>(entity =>
        {
            entity.HasKey(e => e.tipo_ajuste_tarifario_id).HasName("adm_tipo_ajuste_tarifario_pkey");
            entity.ToTable("adm_tipo_ajuste_tarifario", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo }, "uq_adm_tipo_ajuste_tarifario_company_codigo").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.tipo_ajuste_tarifario_id }, "uq_adm_tipo_ajuste_tarifario_company_id").IsUnique();
            entity.Property(e => e.codigo).HasMaxLength(50);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.descripcion).HasMaxLength(300);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<adm_cuadro_tarifario>(entity =>
        {
            entity.HasKey(e => e.cuadro_tarifario_id).HasName("adm_cuadro_tarifario_pkey");
            entity.ToTable("adm_cuadro_tarifario", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo }, "uq_adm_cuadro_tarifario_company_codigo").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.cuadro_tarifario_id }, "uq_adm_cuadro_tarifario_company_id").IsUnique();
            entity.Property(e => e.codigo).HasMaxLength(80);
            entity.Property(e => e.nombre).HasMaxLength(200);
            entity.Property(e => e.descripcion).HasMaxLength(300);
            entity.Property(e => e.referencia_normativa).HasMaxLength(200);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);
        });

        modelBuilder.Entity<adm_ajuste_tarifario>(entity =>
        {
            entity.HasKey(e => e.ajuste_tarifario_id).HasName("adm_ajuste_tarifario_pkey");
            entity.ToTable("adm_ajuste_tarifario", "public");
            entity.Property(e => e.monto_fijo).HasColumnType("numeric(18,4)");
            entity.Property(e => e.porcentaje).HasColumnType("numeric(18,6)");
            entity.Property(e => e.tope_maximo).HasColumnType("numeric(18,4)");
            entity.Property(e => e.condicion_codigo).HasMaxLength(50);
            entity.Property(e => e.parametros).HasColumnType("jsonb");
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(e => e.cuadro_tarifario)
                .WithMany(p => p.adm_ajuste_tarifarios)
                .HasForeignKey(e => e.cuadro_tarifario_id)
                .HasPrincipalKey(p => p.cuadro_tarifario_id)
                .HasConstraintName("fk_adm_ajuste_tarifario_cuadro");

            entity.HasOne(e => e.tipo_ajuste_tarifario)
                .WithMany(p => p.adm_ajuste_tarifarios)
                .HasForeignKey(e => e.tipo_ajuste_tarifario_id)
                .HasPrincipalKey(p => p.tipo_ajuste_tarifario_id)
                .HasConstraintName("fk_adm_ajuste_tarifario_tipo");
        });
    }
}
