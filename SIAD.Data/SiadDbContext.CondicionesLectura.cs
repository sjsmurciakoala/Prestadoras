using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Condiciones de lectura administrables por empresa (app_lectores, 2026-07-06).
// Esquema en Database/ddl_v3/20260706_al_condiciones_lectura.sql.
//   - adm_condicion_lectura_tipo: referencia GLOBAL (no tenant-scoped), acoplada
//     al motor V3. El admin no la edita.
//   - adm_condicion_lectura: catálogo POR EMPRESA (ICompanyScopedEntity), lo
//     administra el portal y lo consume GET /api/condiciones (apc.MobileApi).
public partial class SiadDbContext
{
    public virtual DbSet<adm_condicion_lectura_tipo> adm_condicion_lectura_tipos { get; set; } = null!;

    public virtual DbSet<adm_condicion_lectura> adm_condicion_lecturas { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigureCondicionesLecturaModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<adm_condicion_lectura_tipo>(entity =>
        {
            entity.HasKey(e => e.tipo);
            entity.ToTable("adm_condicion_lectura_tipo", "public");

            entity.Property(e => e.tipo).HasMaxLength(10);
            entity.Property(e => e.descripcion).HasMaxLength(60);
        });

        modelBuilder.Entity<adm_condicion_lectura>(entity =>
        {
            entity.HasKey(e => e.condicion_lectura_id);
            entity.ToTable("adm_condicion_lectura", "public");
            // Índice NO declarado único en el modelo a propósito: la unicidad real
            // es el constraint DEFERRABLE del DDL (uq_adm_condicion_lectura_company_codigo).
            // Si EF lo tratara como único, un intercambio de códigos en un solo
            // SaveChanges lanzaría "circular dependency" antes de tocar la BD.
            entity.HasIndex(e => new { e.company_id, e.codigo });

            entity.Property(e => e.condicion_lectura_id).ValueGeneratedOnAdd();
            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.descripcion).HasMaxLength(60);
            entity.Property(e => e.tipo).HasMaxLength(10);
            entity.Property(e => e.facturacion).HasMaxLength(1).IsFixedLength().HasDefaultValue("S");
            entity.Property(e => e.aplica_descuento).HasMaxLength(1).IsFixedLength().HasDefaultValue("N");
            entity.Property(e => e.orden).HasDefaultValue(0);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasCheckConstraint(
                "ck_adm_condicion_lectura_facturacion",
                "facturacion IN ('S', 'N')");
            entity.HasCheckConstraint(
                "ck_adm_condicion_lectura_aplica_descuento",
                "aplica_descuento IN ('S', 'N')");

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            // El tipo es referencia global (no tenant-scoped): FK simple al catálogo.
            entity.HasOne<adm_condicion_lectura_tipo>()
                .WithMany()
                .HasForeignKey(e => e.tipo)
                .HasPrincipalKey(t => t.tipo)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
