using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Libretas globales (2026-07-16): adm_libreta reemplaza al catálogo
// rutas-por-ciclo como fuente del combo Libreta del cliente. La tabla rutas
// queda sin consumo (solo rollback). Esquema en
// Database/2026-07-16_libretas_globales.sql.
public partial class SiadDbContext
{
    public virtual DbSet<adm_libreta> adm_libretas { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigureLibretasModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<adm_libreta>(entity =>
        {
            entity.HasKey(e => e.libreta_id);
            entity.ToTable("adm_libreta", "public");

            entity.HasIndex(e => new { e.company_id, e.codigo }).IsUnique();

            entity.Property(e => e.libreta_id).ValueGeneratedOnAdd();
            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.descripcion).HasMaxLength(100);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.created_by).HasMaxLength(100);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
