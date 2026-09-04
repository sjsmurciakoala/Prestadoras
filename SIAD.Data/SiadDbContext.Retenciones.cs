using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para el catálogo de retenciones a proveedores, DDL 2026-08-06 (F1).
//   cfg_retencion / cfg_retencion_tasa : GLOBALES (sin company_id) — los % los fija la ley.
//   prv_retencion_cuenta               : TENANT (ICompanyScopedEntity) — la cuenta del pasivo
//                                        es de cada empresa.
// ConfigureRetencionesModel se llama desde OnModelCreatingPartial (SiadDbContext.Accounting.cs),
// junto a ConfigureImpuestosModel. El filtro tenant de prv_retencion_cuenta lo aplica
// ApplyCompanyScope automáticamente (SiadDbContext.Tenancy.cs), por implementar ICompanyScopedEntity.
public partial class SiadDbContext
{
    public virtual DbSet<cfg_retencion> cfg_retenciones { get; set; } = null!;
    public virtual DbSet<cfg_retencion_tasa> cfg_retencion_tasas { get; set; } = null!;
    public virtual DbSet<prv_retencion_cuenta> prv_retencion_cuentas { get; set; } = null!;

    // F4 (registro fiscal de retenciones aplicadas, DDL 2026-08-07).
    public virtual DbSet<prv_retencion_hdr> prv_retencion_hdrs { get; set; } = null!;
    public virtual DbSet<prv_retencion_dtl> prv_retencion_dtls { get; set; } = null!;
    public virtual DbSet<prv_retencion_correlativo> prv_retencion_correlativos { get; set; } = null!;

    private void ConfigureRetencionesModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<cfg_retencion>(entity =>
        {
            entity.HasKey(e => e.id);
            entity.ToTable("cfg_retencion", "public");
            entity.HasIndex(e => e.codigo, "uq_cfg_retencion_codigo").IsUnique();

            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.nombre).HasMaxLength(80);
            entity.Property(e => e.descripcion).HasMaxLength(250);
            entity.Property(e => e.base_calculo).HasMaxLength(10);
            entity.Property(e => e.tipo_impuesto).HasMaxLength(4);
            // OJO: NO se declara HasDefaultValue(true) en `activo` (mismo motivo que cfg_impuesto):
            // EF trataría `false` como sentinel y lo omitiría del INSERT, y la BD aplicaría su
            // DEFAULT true — un alta inactiva nacería activa. La columna sí tiene DEFAULT true en PG.
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<cfg_retencion_tasa>(entity =>
        {
            entity.HasKey(e => e.id);
            entity.ToTable("cfg_retencion_tasa", "public");
            entity.HasIndex(e => e.retencion_id, "ix_cfg_retencion_tasa_retencion");

            entity.Property(e => e.porcentaje).HasPrecision(5, 2);
            entity.Property(e => e.vigencia_desde).HasColumnType("date");
            entity.Property(e => e.vigencia_hasta).HasColumnType("date");
            entity.Property(e => e.descripcion).HasMaxLength(250);
            // Sin HasDefaultValue en `activo` ni `porcentaje`: EF envía siempre el valor explícito.
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // ON DELETE RESTRICT en la BD: una tasa usada no debe borrarse en cascada.
            entity.HasOne(e => e.retencion)
                .WithMany(p => p.tasas)
                .HasForeignKey(e => e.retencion_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<prv_retencion_cuenta>(entity =>
        {
            entity.HasKey(e => e.id);
            entity.ToTable("prv_retencion_cuenta", "public");
            entity.HasIndex(e => new { e.company_id, e.retencion_id }, "uq_prv_retencion_cuenta_company_retencion").IsUnique();

            entity.Property(e => e.id).ValueGeneratedOnAdd();
            // Sin HasDefaultValue en `activo` (mismo motivo que arriba).
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // FK tenant → cfg_company, CASCADE (molde con_integracion_cuenta).
            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .OnDelete(DeleteBehavior.Cascade);

            // FK al concepto de retención (global), RESTRICT.
            entity.HasOne(e => e.retencion)
                .WithMany()
                .HasForeignKey(e => e.retencion_id)
                .OnDelete(DeleteBehavior.Restrict);

            // FK a la cuenta contable, RESTRICT.
            entity.HasOne(e => e.account)
                .WithMany()
                .HasForeignKey(e => e.account_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureRetencionesRegistroModel(modelBuilder);
    }

    // F4 (DDL 2026-08-07): registro fiscal estructurado de retenciones aplicadas.
    //   prv_retencion_hdr / prv_retencion_dtl : TENANT (ICompanyScopedEntity); hdr→dtl con FK
    //       compuesta tenant-safe (company_id, retencion_hdr_id) — molde alm_movimiento_dtl.
    //   prv_retencion_correlativo             : contador de folio por empresa (PK = company_id).
    // El filtro tenant y el stamping los aplica ApplyCompanyScope automáticamente
    // (SiadDbContext.Tenancy.cs), por implementar ICompanyScopedEntity.
    private void ConfigureRetencionesRegistroModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<prv_retencion_hdr>(entity =>
        {
            entity.HasKey(e => e.retencion_hdr_id).HasName("prv_retencion_hdr_pkey");
            entity.ToTable("prv_retencion_hdr", "public");
            entity.HasIndex(e => new { e.company_id, e.folio }, "uq_prv_retencion_hdr_folio").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.numero_orden, e.numero_abono }, "uq_prv_retencion_hdr_pago").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.retencion_hdr_id }, "uq_prv_retencion_hdr_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.fecha_emision }, "ix_prv_retencion_hdr_company_fecha");
            entity.HasIndex(e => new { e.company_id, e.cod_proveedor }, "ix_prv_retencion_hdr_proveedor");
            // Unicidad del pago para compras (origen=2): la UNIQUE por numero_orden no cubre los NULL.
            entity.HasIndex(e => new { e.company_id, e.cxp_id, e.numero_abono }, "uq_prv_retencion_hdr_cxp_pago")
                  .IsUnique().HasFilter("origen = 2");

            entity.Property(e => e.retencion_hdr_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.fecha_emision).HasColumnType("date");
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.rtn_proveedor).HasMaxLength(20);
            entity.Property(e => e.base_total).HasPrecision(15, 2);
            entity.Property(e => e.total_retenido).HasPrecision(15, 2);
            entity.Property(e => e.poliza_number).HasMaxLength(40);
            entity.Property(e => e.cai_proveedor).HasMaxLength(50);
            entity.Property(e => e.motivo_anulacion).HasMaxLength(250);
            entity.Property(e => e.usuario_creo).HasMaxLength(100);
            entity.Property(e => e.fecha_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuario_modifica).HasMaxLength(100);
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuario_anulacion).HasMaxLength(100);
            entity.Property(e => e.fecha_anulacion).HasColumnType("timestamp without time zone");
            // Sin HasDefaultValue en estado_id ni origen: el registro fiscal se escribe por raw SQL en
            // el servicio (no por EF SaveChanges), así que el sentinel de EF no aplica y el CHECK/DEFAULT
            // viven en la BD. Las FKs (company_id, numero_orden) → prv_compromiso_hdr y
            // (company_id, cxp_id) → alm_compra_cxp viven en la BD (MATCH SIMPLE, se eximen mutuamente
            // por el NULL); no se modelan como navegación (el hdr no navega al origen desde EF).
        });

        modelBuilder.Entity<prv_retencion_dtl>(entity =>
        {
            entity.HasKey(e => e.retencion_dtl_id).HasName("prv_retencion_dtl_pkey");
            entity.ToTable("prv_retencion_dtl", "public");
            entity.HasIndex(e => new { e.company_id, e.retencion_hdr_id }, "ix_prv_retencion_dtl_hdr");

            entity.Property(e => e.retencion_dtl_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.porcentaje).HasPrecision(5, 2);
            entity.Property(e => e.base_linea).HasPrecision(15, 2);
            entity.Property(e => e.monto_retenido).HasPrecision(15, 2);

            // FK compuesta tenant-safe hdr→dtl (molde alm_movimiento_dtl): la relación viaja por
            // (company_id, retencion_hdr_id), no por el id a secas.
            entity.HasOne(d => d.cabecera)
                  .WithMany(h => h.lineas)
                  .HasForeignKey(d => new { d.company_id, d.retencion_hdr_id })
                  .HasPrincipalKey(h => new { h.company_id, h.retencion_hdr_id })
                  .HasConstraintName("fk_prv_retencion_dtl_hdr")
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<prv_retencion_correlativo>(entity =>
        {
            entity.HasKey(e => e.company_id).HasName("prv_retencion_correlativo_pkey");
            entity.ToTable("prv_retencion_correlativo", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
        });
    }
}
