using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Configuración de correo y notificaciones por empresa (2026-08-13_cfg_correo_notificaciones.sql).
//   cfg_correo                     = la conexión: 1 por empresa, API key cifrada + remitente por defecto.
//   cfg_notificacion               = el área/tipo: N por empresa, remitente propio opcional.
//   cfg_notificacion_destinatario  = los destinatarios TO/CC: N por notificación.
// Las tres implementan ICompanyScopedEntity; el filtro tenant y el stamping de company_id los
// aplica SiadDbContext.Tenancy.cs de forma global. ConfigureCorreoModel se llama desde
// OnModelCreatingPartial (SiadDbContext.Accounting.cs), junto a ConfigureImpuestosModel.
public partial class SiadDbContext
{
    public virtual DbSet<cfg_correo> cfg_correos { get; set; } = null!;
    public virtual DbSet<cfg_notificacion> cfg_notificacions { get; set; } = null!;
    public virtual DbSet<cfg_notificacion_destinatario> cfg_notificacion_destinatarios { get; set; } = null!;

    private void ConfigureCorreoModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_correo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cfg_correo_pkey");
            entity.ToTable("cfg_correo", "public");
            entity.HasIndex(e => new { e.company_id, e.proveedor }, "uq_cfg_correo_company_proveedor").IsUnique();

            entity.Property(e => e.proveedor).HasMaxLength(20);
            // api_key_cifrada: TEXT (ciphertext de DataProtection), sin límite de longitud.
            entity.Property(e => e.remitente_email_default).HasMaxLength(200);
            entity.Property(e => e.remitente_nombre_default).HasMaxLength(150);
            // Sin HasDefaultValue en 'activo': el INSERT lleva SIEMPRE el valor explícito. EF trataría
            // false como sentinel (no asignado) y lo omitiría, y la BD aplicaría su DEFAULT false; aquí
            // coinciden, pero se mantiene el criterio del módulo por si el DEFAULT cambia.
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cfg_notificacion>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cfg_notificacion_pkey");
            entity.ToTable("cfg_notificacion", "public");
            entity.HasIndex(e => new { e.company_id, e.tipo }, "uq_cfg_notificacion_company_tipo").IsUnique();

            entity.Property(e => e.tipo).HasMaxLength(30);
            entity.Property(e => e.nombre).HasMaxLength(120);
            entity.Property(e => e.remitente_email).HasMaxLength(200);
            entity.Property(e => e.remitente_nombre).HasMaxLength(150);
            // Sin HasDefaultValue en 'activo' (DEFAULT true en BD): un alta inactiva debe escribirse
            // como false, no omitirse y nacer activa. Mismo criterio que cfg_impuesto.
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cfg_notificacion_destinatario>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cfg_notificacion_destinatario_pkey");
            entity.ToTable("cfg_notificacion_destinatario", "public");
            entity.HasIndex(e => e.notificacion_id, "ix_cfg_notif_dest_notificacion");

            entity.Property(e => e.correo).HasMaxLength(200);
            entity.Property(e => e.clase).HasMaxLength(4);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");

            // CASCADE: los destinatarios son hijos del área; al borrarla se van con ella. La FK
            // NO lleva company_id (cfg_notificacion tiene PK simple); el aislamiento entre empresas
            // lo garantiza el query filter global de EF. Mismo patrón que prv_proveedor_contacto.
            entity.HasOne(e => e.notificacion)
                .WithMany(p => p.destinatarios)
                .HasForeignKey(e => e.notificacion_id)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
