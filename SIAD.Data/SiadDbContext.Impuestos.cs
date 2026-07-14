using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para el catálogo de impuestos (cfg_impuesto + cfg_impuesto_tasa),
// DDL 2026-07-14. Catálogo GLOBAL: sin company_id, sin ICompanyScopedEntity — la ley
// fija las tasas, no la empresa. Mismo patrón que cfg_tipo_documento_fiscal.
// ConfigureImpuestosModel es llamado desde OnModelCreatingPartial
// (SiadDbContext.Accounting.cs), al final de la cadena.
public partial class SiadDbContext
{
    public virtual DbSet<cfg_impuesto> cfg_impuestos { get; set; } = null!;
    public virtual DbSet<cfg_impuesto_tasa> cfg_impuesto_tasas { get; set; } = null!;

    private void ConfigureImpuestosModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_impuesto>(entity =>
        {
            entity.HasKey(e => e.id);
            entity.ToTable("cfg_impuesto", "public");
            entity.HasIndex(e => e.codigo, "uq_cfg_impuesto_codigo").IsUnique();

            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(80);
            entity.Property(e => e.descripcion).HasMaxLength(250);
            // OJO: NO se declara HasDefaultValue(true) en `activo`. EF trataría `false`
            // como valor "no asignado" (sentinel = default(bool)) y lo omitiría del INSERT,
            // con lo que la BD aplicaría su DEFAULT true: un alta inactiva nacería activa.
            // La columna sí tiene DEFAULT true en Postgres, para otros clientes.
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cfg_impuesto_tasa>(entity =>
        {
            entity.HasKey(e => e.id);
            entity.ToTable("cfg_impuesto_tasa", "public");
            entity.HasIndex(e => e.impuesto_id, "ix_cfg_impuesto_tasa_impuesto");

            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.nombre).HasMaxLength(80);
            entity.Property(e => e.tipo).HasMaxLength(12);
            entity.Property(e => e.porcentaje).HasPrecision(5, 2);
            entity.Property(e => e.vigencia_desde).HasColumnType("date");
            entity.Property(e => e.vigencia_hasta).HasColumnType("date");
            entity.Property(e => e.descripcion).HasMaxLength(250);
            // Mismo motivo que en cfg_impuesto: sin HasDefaultValue en `activo` ni en
            // `porcentaje`, para que EF envíe SIEMPRE el valor explícito (una tasa EXENTA
            // con porcentaje 0 debe escribirse como 0, no omitirse).
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // ON DELETE RESTRICT en la BD: una tasa usada no debe poder borrarse en cascada.
            entity.HasOne(e => e.impuesto)
                .WithMany(p => p.tasas)
                .HasForeignKey(e => e.impuesto_id)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
