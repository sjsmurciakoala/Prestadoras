using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para la bitácora de maestros (bitacora_maestros +
// bitacora_maestro_config), Fase 1 del plan docs/plans/2026-07-17-bitacora-maestros.md.
// Ambas tablas son tenant-scoped (ICompanyScopedEntity); el estampado/filtro de
// company_id lo hace SiadDbContext.Tenancy.cs, no se toca aquí.
// ConfigureAuditoriaModel es llamado desde OnModelCreatingPartial
// (SiadDbContext.Accounting.cs), al final de la cadena. Mismo patrón que
// ConfigureImpuestosModel.
public partial class SiadDbContext
{
    public virtual DbSet<bitacora_maestros> bitacora_maestros { get; set; } = null!;
    public virtual DbSet<bitacora_maestro_config> bitacora_maestro_configs { get; set; } = null!;

    private void ConfigureAuditoriaModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<bitacora_maestros>(entity =>
        {
            entity.HasKey(e => e.bitacora_maestro_id).HasName("bitacora_maestro_pkey");
            entity.ToTable("bitacora_maestros", "public");
            entity.Property(e => e.bitacora_maestro_id).ValueGeneratedOnAdd();
            entity.Property(e => e.modulo).HasMaxLength(80);
            entity.Property(e => e.tabla).HasMaxLength(128);
            entity.Property(e => e.entidad).HasMaxLength(256);
            entity.Property(e => e.registro_id).HasMaxLength(500);
            entity.Property(e => e.accion).HasMaxLength(30);
            entity.Property(e => e.descripcion).HasMaxLength(500);
            entity.Property(e => e.valores_anteriores).HasColumnType("jsonb");
            entity.Property(e => e.valores_nuevos).HasColumnType("jsonb");
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuario).HasMaxLength(256);
        });

        modelBuilder.Entity<bitacora_maestro_config>(entity =>
        {
            entity.HasKey(e => e.id).HasName("bitacora_maestro_config_pkey");
            entity.ToTable("bitacora_maestro_config", "public");
            entity.HasIndex(e => new { e.company_id, e.entidad }, "uq_bitacora_maestro_config_company_entidad").IsUnique();
            entity.Property(e => e.entidad).HasMaxLength(100);
            entity.Property(e => e.habilitado).HasDefaultValue(true);
            entity.Property(e => e.audita_crear).HasDefaultValue(true);
            entity.Property(e => e.audita_editar).HasDefaultValue(true);
            entity.Property(e => e.audita_eliminar).HasDefaultValue(true);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });
    }
}
