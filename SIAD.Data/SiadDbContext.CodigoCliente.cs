using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Generador del código de cliente por empresa (2026-07-16). Esquema en
// Database/2026-07-16_codigo_cliente_automatico.sql; el consumo atómico del
// correlativo vive en fn_adm_siguiente_codigo_cliente (UPDATE ... RETURNING).
public partial class SiadDbContext
{
    public virtual DbSet<adm_codigo_cliente_config> adm_codigo_cliente_configs { get; set; } = null!;

    // Llamado desde OnModelCreatingPartial en SiadDbContext.Accounting.cs
    private void ConfigureCodigoClienteModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<adm_codigo_cliente_config>(entity =>
        {
            entity.HasKey(e => e.company_id);
            entity.ToTable("adm_codigo_cliente_config", "public");

            entity.Property(e => e.company_id).ValueGeneratedNever();
            entity.Property(e => e.prefijo).HasMaxLength(5);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
