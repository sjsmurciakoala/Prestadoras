using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.Entities;

namespace SIAD.Data;

// Extensión parcial para el módulo Almacén (alm_) y Activo Fijo (af_),
// migrado desde MySQL bdsimafi (2026-07-01). Post-scaffold: entidades y mapeo
// se declaran aquí para no tocar SiadDbContext.cs generado.
// ConfigureAlmacenModel es llamado desde ConfigureCobranzaModel
// (SiadDbContext.Cobranza.cs) al final de la cadena OnModelCreatingPartial.
public partial class SiadDbContext
{
    public virtual DbSet<alm_articulo> alm_articulos { get; set; } = null!;
    public virtual DbSet<alm_kardex> alm_kardexs { get; set; } = null!;
    public virtual DbSet<alm_compra> alm_compras { get; set; } = null!;
    public virtual DbSet<alm_requisicion> alm_requisicions { get; set; } = null!;
    public virtual DbSet<alm_descargo> alm_descargos { get; set; } = null!;
    // Cabecera y correlativo de la requisición (Fase 6, 2026-08-01_alm_requisicion_descargo.sql).
    public virtual DbSet<alm_requisicion_hdr> alm_requisicion_hdrs { get; set; } = null!;
    public virtual DbSet<alm_requisicion_correlativo> alm_requisicion_correlativos { get; set; } = null!;
    // Cabecera y correlativo del descargo (Fase 6).
    public virtual DbSet<alm_descargo_hdr> alm_descargo_hdrs { get; set; } = null!;
    public virtual DbSet<alm_descargo_correlativo> alm_descargo_correlativos { get; set; } = null!;
    public virtual DbSet<af_activo_fijo> af_activo_fijos { get; set; } = null!;
    public virtual DbSet<af_activo_fijo_depreciacion> af_activo_fijo_depreciacions { get; set; } = null!;
    public virtual DbSet<alm_unidad_medida> alm_unidad_medidas { get; set; } = null!;
    public virtual DbSet<alm_categoria_unidad> alm_categoria_unidads { get; set; } = null!;
    public virtual DbSet<alm_termino_pago> alm_termino_pagos { get; set; } = null!;
    public virtual DbSet<alm_tipo_articulo> alm_tipo_articulos { get; set; } = null!;
    public virtual DbSet<alm_grupo> alm_grupos { get; set; } = null!;
    public virtual DbSet<alm_bodega> alm_bodegas { get; set; } = null!;
    public virtual DbSet<alm_articulo_bodega> alm_articulo_bodegas { get; set; } = null!;
    public virtual DbSet<alm_articulo_proveedor> alm_articulo_proveedors { get; set; } = null!;
    public virtual DbSet<alm_orden_compra> alm_orden_compras { get; set; } = null!;
    public virtual DbSet<alm_orden_compra_detalle> alm_orden_compra_detalles { get; set; } = null!;
    public virtual DbSet<alm_orden_compra_correlativo> alm_orden_compra_correlativos { get; set; } = null!;
    public virtual DbSet<alm_compra_hdr> alm_compra_hdrs { get; set; } = null!;
    public virtual DbSet<alm_compra_correlativo> alm_compra_correlativos { get; set; } = null!;
    public virtual DbSet<alm_compra_cxp> alm_compra_cxps { get; set; } = null!;
    public virtual DbSet<alm_compra_cxp_abono> alm_compra_cxp_abonos { get; set; } = null!;
    public virtual DbSet<alm_config_inventario> alm_config_inventarios { get; set; } = null!;
    public virtual DbSet<alm_ajuste_inventario> alm_ajuste_inventarios { get; set; } = null!;
    public virtual DbSet<alm_tipo_movimiento> alm_tipo_movimientos { get; set; } = null!;
    // Documento de movimiento de almacén (2026-08-03_alm_movimiento.sql).
    public virtual DbSet<alm_movimiento_hdr> alm_movimiento_hdrs { get; set; } = null!;
    public virtual DbSet<alm_movimiento_dtl> alm_movimiento_dtls { get; set; } = null!;
    public virtual DbSet<alm_movimiento_correlativo> alm_movimiento_correlativos { get; set; } = null!;
    // Recepciones del traslado entre bodegas (2026-08-04_alm_traslado.sql, Fase 5).
    public virtual DbSet<alm_traslado_recepcion> alm_traslado_recepcions { get; set; } = null!;
    public virtual DbSet<alm_traslado_recepcion_dtl> alm_traslado_recepcion_dtls { get; set; } = null!;
    // Política del ISV en compras por empresa (2026-07-30_cfg_compra_isv.sql). NO es de almacén
    // (es config de empresa); se mapea aquí solo por conveniencia de encadenamiento del modelo.
    public virtual DbSet<cfg_compra_isv> cfg_compra_isvs { get; set; } = null!;

    private void ConfigureAlmacenModel(ModelBuilder modelBuilder)
    {
        // ── Carga inicial de inventario (2026-07-30_alm_carga_inicial.sql) ──────
        modelBuilder.Entity<alm_config_inventario>(entity =>
        {
            // La PK ES el company_id: una sola configuración por empresa.
            entity.HasKey(e => e.company_id).HasName("alm_config_inventario_pkey");
            entity.ToTable("alm_config_inventario", "public");

            entity.Property(e => e.company_id).ValueGeneratedNever();
            entity.Property(e => e.base_costo_apertura).HasMaxLength(20);
            entity.Property(e => e.fecha_corte_apertura).HasColumnType("date");
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // Siguiendo el precedente de cfg_impuesto_tasa: los booleanos y los campos con
            // CHECK NO llevan HasDefaultValue, para que el INSERT lleve el valor explícito
            // y no dependa del default de la BD.
        });

        modelBuilder.Entity<alm_ajuste_inventario>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_ajuste_inventario_pkey");
            entity.ToTable("alm_ajuste_inventario", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_ajuste_inventario_company");
            entity.HasIndex(e => new { e.company_id, e.articulo_id, e.bodega_id }, "ix_alm_ajuste_inventario_par");

            entity.Property(e => e.clase).HasMaxLength(10);
            entity.Property(e => e.cantidad).HasPrecision(15, 2);
            entity.Property(e => e.costo_unitario).HasPrecision(12, 4);
            entity.Property(e => e.motivo).HasMaxLength(120);
            entity.Property(e => e.observacion).HasMaxLength(254);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");

            // Las FK son COMPUESTAS por tenant en la BD ((company_id, articulo_id) y
            // (company_id, bodega_id) contra las claves alternas). EF las mapea simples,
            // igual que el resto del módulo: un bug de tenant saldría como 23503 desde la
            // BD, no lo atrapa EF. Por eso el servicio valida el tenant antes de insertar.
            entity.HasOne(e => e.articulo).WithMany()
                .HasForeignKey(e => e.articulo_id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.bodega).WithMany()
                .HasForeignKey(e => e.bodega_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Catálogo de tipos de movimiento (2026-08-01_alm_tipo_movimiento.sql) ─────────
        modelBuilder.Entity<alm_tipo_movimiento>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_tipo_movimiento_pkey");
            entity.ToTable("alm_tipo_movimiento", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo }, "uq_alm_tipo_movimiento_codigo").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_tipo_movimiento_tenant").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_tipo_movimiento_company");

            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.nombre).HasMaxLength(80);
            entity.Property(e => e.clase).HasMaxLength(10);
            entity.Property(e => e.requiere_autorizacion).HasDefaultValue(false);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.orden).HasDefaultValue((short)0);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        // ── Documento de movimiento de almacén (2026-08-03_alm_movimiento.sql) ───────────
        modelBuilder.Entity<alm_movimiento_correlativo>(entity =>
        {
            // La PK ES el company_id: un contador por empresa.
            entity.HasKey(e => e.company_id).HasName("alm_movimiento_correlativo_pkey");
            entity.ToTable("alm_movimiento_correlativo", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
        });

        modelBuilder.Entity<alm_movimiento_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_movimiento_hdr_pkey");
            entity.ToTable("alm_movimiento_hdr", "public");
            entity.HasIndex(e => new { e.company_id, e.numero }, "uq_alm_movimiento_hdr_numero").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_movimiento_hdr_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.fecha }, "ix_alm_movimiento_hdr_company_fecha");
            entity.HasIndex(e => new { e.company_id, e.tipo_movimiento_id }, "ix_alm_movimiento_hdr_tipo");
            entity.HasIndex(e => new { e.company_id, e.bodega_id }, "ix_alm_movimiento_hdr_bodega");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.motivo).HasMaxLength(120);
            entity.Property(e => e.documento_referencia).HasMaxLength(30);
            entity.Property(e => e.observaciones).HasMaxLength(1000);
            entity.Property(e => e.total).HasPrecision(14, 2);
            entity.Property(e => e.anulado_por).HasMaxLength(100);
            entity.Property(e => e.motivo_anulacion).HasMaxLength(500);
            entity.Property(e => e.fecha_posteo).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_anulacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // Traslado entre bodegas (2026-08-04_alm_traslado.sql).
            entity.Property(e => e.requiere_recepcion).HasDefaultValue(true);
            entity.Property(e => e.recibido_por).HasMaxLength(100);
            entity.Property(e => e.fecha_recepcion).HasColumnType("timestamp without time zone");
            // bodega_destino_id es escalar (int?): la FK vive en la BD; no se modela como navegación
            // (el servicio resuelve la bodega por id, como en bodega_id).

            // Sin HasDefaultValue en estado/posteado: los campos con CHECK van explícitos en
            // el INSERT y no dependen del default de la BD (mismo criterio que alm_compra_hdr).
        });

        modelBuilder.Entity<alm_movimiento_dtl>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_movimiento_dtl_pkey");
            entity.ToTable("alm_movimiento_dtl", "public");
            entity.HasIndex(e => new { e.company_id, e.uuid }, "uq_alm_movimiento_dtl_uuid").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_movimiento_dtl_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.movimiento_hdr_id }, "ix_alm_movimiento_dtl_hdr");
            entity.HasIndex(e => new { e.company_id, e.articulo_id }, "ix_alm_movimiento_dtl_articulo");

            entity.Property(e => e.id).ValueGeneratedOnAdd();
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasPrecision(15, 2);
            entity.Property(e => e.cantidad_recibida).HasPrecision(15, 2);
            entity.Property(e => e.costo_unitario).HasPrecision(12, 4);
            entity.Property(e => e.costo_real).HasPrecision(12, 4);
            entity.Property(e => e.total).HasPrecision(14, 2);

            // FK compuesta tenant-safe: la relación viaja por (company_id, hdr_id), no por el
            // id a secas, para que EF no pueda enlazar con una cabecera de otra empresa.
            entity.HasOne(d => d.cabecera)
                  .WithMany(h => h.lineas)
                  .HasForeignKey(d => new { d.company_id, d.movimiento_hdr_id })
                  .HasPrincipalKey(h => new { h.company_id, h.id })
                  .HasConstraintName("fk_alm_movimiento_dtl_hdr")
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── Recepciones del traslado entre bodegas (2026-08-04_alm_traslado.sql, Fase 5) ────
        modelBuilder.Entity<alm_traslado_recepcion>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_traslado_recepcion_pkey");
            entity.ToTable("alm_traslado_recepcion", "public");
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_traslado_recepcion_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.uuid }, "uq_alm_traslado_recepcion_uuid").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.movimiento_hdr_id }, "ix_alm_traslado_recepcion_hdr");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.observaciones).HasMaxLength(500);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.cabecera)
                  .WithMany(h => h.recepciones)
                  .HasForeignKey(d => new { d.company_id, d.movimiento_hdr_id })
                  .HasPrincipalKey(h => new { h.company_id, h.id })
                  .HasConstraintName("fk_alm_traslado_recepcion_hdr")
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<alm_traslado_recepcion_dtl>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_traslado_recepcion_dtl_pkey");
            entity.ToTable("alm_traslado_recepcion_dtl", "public");
            entity.HasIndex(e => new { e.company_id, e.uuid }, "uq_alm_traslado_recepcion_dtl_uuid").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_traslado_recepcion_dtl_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.recepcion_id }, "ix_alm_traslado_recepcion_dtl_rec");
            entity.HasIndex(e => new { e.company_id, e.movimiento_dtl_id }, "ix_alm_traslado_recepcion_dtl_dtl");
            entity.HasIndex(e => new { e.company_id, e.articulo_id }, "ix_alm_traslado_recepcion_dtl_articulo");

            entity.Property(e => e.id).ValueGeneratedOnAdd();
            entity.Property(e => e.cantidad).HasPrecision(15, 2);
            entity.Property(e => e.costo_real).HasPrecision(12, 4);
            entity.Property(e => e.total).HasPrecision(14, 2);

            entity.HasOne(d => d.recepcion)
                  .WithMany(r => r.lineas)
                  .HasForeignKey(d => new { d.company_id, d.recepcion_id })
                  .HasPrincipalKey(r => new { r.company_id, r.id })
                  .HasConstraintName("fk_alm_traslado_recepcion_dtl_rec")
                  .OnDelete(DeleteBehavior.Cascade);

            // FK compuesta tenant-safe al renglón del traslado. Sin cascada: el renglón no se borra.
            entity.HasOne(d => d.renglonTraslado)
                  .WithMany()
                  .HasForeignKey(d => new { d.company_id, d.movimiento_dtl_id })
                  .HasPrincipalKey(l => new { l.company_id, l.id })
                  .HasConstraintName("fk_alm_traslado_recepcion_dtl_dtl")
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Política del ISV en compras, por empresa (2026-07-30_cfg_compra_isv.sql) ──────
        modelBuilder.Entity<cfg_compra_isv>(entity =>
        {
            // La PK ES el company_id: una sola configuración por empresa.
            entity.HasKey(e => e.company_id).HasName("cfg_compra_isv_pkey");
            entity.ToTable("cfg_compra_isv", "public");

            entity.Property(e => e.company_id).ValueGeneratedNever();
            entity.Property(e => e.tratamiento).HasMaxLength(10);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // Sin HasDefaultValue en 'tratamiento': el INSERT lleva SIEMPRE el valor explícito
            // (COSTO/FISCAL), mismo criterio que alm_config_inventario y cfg_impuesto_tasa.
        });

        modelBuilder.Entity<alm_articulo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_articulo_pkey");
            entity.ToTable("alm_articulo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo_articulo },
                "uq_alm_articulo_company_codigo").IsUnique()
                .HasFilter("codigo_articulo IS NOT NULL AND codigo_articulo <> ''");
            entity.HasIndex(e => e.company_id, "ix_alm_articulo_company");
            // Índice parcial del soft-delete (2026-07-29_alm_articulo_activo.sql).
            entity.HasIndex(e => e.company_id, "ix_alm_articulo_company_activo").HasFilter("activo");

            // Concurrencia optimista sobre la columna de SISTEMA xmin de Postgres: no
            // necesita DDL (toda tabla la tiene). Se declara como propiedad SOMBRA para no
            // ensuciar la entidad scaffolded, y EF la incluye en el WHERE del UPDATE: si
            // otro usuario guardó primero, el segundo recibe DbUpdateConcurrencyException
            // (el controlador la traduce a 409) en vez de pisarlo en silencio. El token
            // viaja al cliente en ArticuloEditDto.RowVersion.
            // El azúcar UseXminAsConcurrencyToken() se removió en Npgsql 9; esta es la
            // configuración explícita equivalente. ValueGeneratedOnAddOrUpdate mantiene la
            // columna fuera del INSERT/UPDATE (la genera Postgres).
            entity.Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();

            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(120).HasDefaultValue("");
            entity.Property(e => e.fecha_registro).HasColumnType("date");
            entity.Property(e => e.cantidad).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia_minima).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_unitario).HasPrecision(12, 4).HasDefaultValue(0m);
            entity.Property(e => e.linea).HasMaxLength(2);
            entity.Property(e => e.grupo).HasMaxLength(6);
            entity.Property(e => e.unidad_medida).HasMaxLength(40);
            entity.Property(e => e.diametro).HasMaxLength(80);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasIndex(e => e.unidad_medida_id, "ix_alm_articulo_unidad_medida");
            entity.HasOne(e => e.unidad_medida_ref).WithMany()
                .HasForeignKey(e => e.unidad_medida_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.unidad_almacenaje_id, "ix_alm_articulo_unidad_almacenaje");
            entity.HasOne(e => e.unidad_almacenaje_ref).WithMany()
                .HasForeignKey(e => e.unidad_almacenaje_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.unidad_salida_id, "ix_alm_articulo_unidad_salida");
            entity.HasOne(e => e.unidad_salida_ref).WithMany()
                .HasForeignKey(e => e.unidad_salida_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.tipo_articulo_id, "ix_alm_articulo_tipo_articulo");
            entity.HasOne(e => e.tipo_articulo_ref).WithMany()
                .HasForeignKey(e => e.tipo_articulo_id)
                .OnDelete(DeleteBehavior.SetNull);

            // linea_id (FK legacy a alm_linea) sigue en la BD pero ya no se mapea:
            // se elimina en la Fase 3 de la unificación línea→tipo (2026-07-16).
            entity.HasIndex(e => e.grupo_id, "ix_alm_articulo_grupo_id");
            entity.HasOne(e => e.grupo_ref).WithMany()
                .HasForeignKey(e => e.grupo_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<alm_kardex>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_kardex_pkey");
            entity.ToTable("alm_kardex", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_kardex_company");
            entity.HasIndex(e => e.codigo_articulo, "ix_alm_kardex_codigo_articulo");
            entity.HasIndex(e => e.fecha, "ix_alm_kardex_fecha");
            entity.HasIndex(e => e.bodega_id, "ix_alm_kardex_bodega");
            entity.HasIndex(e => e.articulo_id, "ix_alm_kardex_articulo");
            entity.HasIndex(e => new { e.company_id, e.uuid },
                "uq_alm_kardex_company_uuid").IsUnique()
                .HasFilter("uuid IS NOT NULL");
            entity.HasIndex(e => new { e.company_id, e.documento_tipo, e.documento_id },
                "ix_alm_kardex_documento");
            entity.HasIndex(e => new { e.company_id, e.articulo_id, e.bodega_id, e.fecha, e.id },
                "ix_alm_kardex_saldo");
            entity.HasIndex(e => e.bodega_destino_id, "ix_alm_kardex_bodega_destino");

            entity.Property(e => e.numero_documento).HasPrecision(11, 0);
            entity.Property(e => e.tipo_transaccion).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.bodega).HasMaxLength(2);
            entity.Property(e => e.ingresos).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.salidas).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_unitario).HasPrecision(14, 4).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(17, 4).HasDefaultValue(0m);
            entity.Property(e => e.debe).HasPrecision(17, 4).HasDefaultValue(0m);
            entity.Property(e => e.haber).HasPrecision(17, 4).HasDefaultValue(0m);
            entity.Property(e => e.cuenta_contable).HasMaxLength(25);
            entity.Property(e => e.departamento).HasMaxLength(3);
            entity.Property(e => e.departamento_desc).HasMaxLength(100);
            entity.Property(e => e.linea).HasMaxLength(2);
            entity.Property(e => e.linea_desc).HasMaxLength(150);
            entity.Property(e => e.barrio).HasMaxLength(3);
            entity.Property(e => e.es_ajuste).HasDefaultValue(false);
            entity.Property(e => e.descripcion).HasMaxLength(120);
            entity.Property(e => e.observacion).HasMaxLength(254);
            entity.Property(e => e.documento_tipo).HasMaxLength(20);
            entity.Property(e => e.existencia_resultante).HasPrecision(15, 2);
            entity.Property(e => e.costo_promedio_resultante).HasPrecision(12, 4);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.bodega_ref).WithMany()
                .HasForeignKey(e => e.bodega_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.bodega_destino_ref).WithMany()
                .HasForeignKey(e => e.bodega_destino_id)
                .OnDelete(DeleteBehavior.Restrict);

            // RESTRICT, no SetNull: alm_kardex es un libro mayor. Borrar un artículo con
            // asientos debe ser imposible, no anular la referencia (ver
            // Database/2026-07-14_alm_kardex_fk_articulo_restrict.sql). Con SetNull, EF
            // intentaría un UPDATE sobre el kardex que además choca con trg_alm_kardex_inmutable.
            entity.HasOne(e => e.articulo_ref).WithMany()
                .HasForeignKey(e => e.articulo_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<alm_compra>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_compra_pkey");
            entity.ToTable("alm_compra", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_compra_company");
            entity.HasIndex(e => e.codigo_articulo, "ix_alm_compra_codigo_articulo");
            entity.HasIndex(e => e.fecha, "ix_alm_compra_fecha");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.fecha_factura).HasColumnType("date");
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.precio_unitario).HasPrecision(11, 4).HasDefaultValue(0m);
            entity.Property(e => e.precio_unitario_anterior).HasPrecision(11, 4).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.impuesto).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.descuento).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.oficina).HasMaxLength(5);
            entity.Property(e => e.proveedor).HasMaxLength(100);
            entity.Property(e => e.numero_factura).HasPrecision(11, 0);
            entity.Property(e => e.numero).HasPrecision(11, 0);
            entity.Property(e => e.orden_compra).HasMaxLength(20);
            entity.Property(e => e.plazo_dias).HasPrecision(11, 0);
            entity.Property(e => e.tipo_compra).HasDefaultValue((short)0);
            entity.Property(e => e.traslado).HasMaxLength(1);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.cuenta_contable_anterior).HasMaxLength(30);
            entity.Property(e => e.cuenta_por_pagar).HasMaxLength(30);
            entity.Property(e => e.cuenta_por_pagar_anterior).HasMaxLength(30);
            entity.Property(e => e.codigo_compra).HasMaxLength(20);
            entity.Property(e => e.concepto).HasMaxLength(254);
            entity.Property(e => e.posteado).HasDefaultValue(false);
            entity.Property(e => e.fecha_posteo).HasColumnType("timestamp without time zone");
            // origen NO lleva HasDefaultValue: la columna no tiene DEFAULT en la BD (a
            // propósito, ver Database/2026-07-14_alm_documentos_bodega_posteo.sql). El
            // inicializador de la entidad (= OrigenDocumento.Siad) hace que EF siempre
            // lo mande explícito en el INSERT.
            entity.Property(e => e.origen).HasMaxLength(10);

            entity.HasIndex(e => e.articulo_id, "ix_alm_compra_articulo");
            entity.HasOne(e => e.articulo_ref).WithMany()
                .HasForeignKey(e => e.articulo_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.company_id, e.uuid },
                "uq_alm_compra_company_uuid").IsUnique()
                .HasFilter("uuid IS NOT NULL");
            entity.HasIndex(e => e.company_id, "ix_alm_compra_pendiente")
                .HasFilter("origen = 'SIAD' AND posteado = false");
            entity.HasIndex(e => e.bodega_id, "ix_alm_compra_bodega");
            entity.HasOne(e => e.bodega_ref).WithMany()
                .HasForeignKey(e => e.bodega_id)
                .OnDelete(DeleteBehavior.Restrict);

            // Enlaces de la recepción (2026-07-30_alm_orden_compra.sql y
            // 2026-07-31_alm_compra_recepcion.sql). Ambas FK son compuestas tenant-safe en la
            // BD (company_id, …); EF las mapea simples, igual que el resto del módulo — el
            // servicio valida el tenant antes de insertar y la BD es la red de seguridad.
            entity.HasIndex(e => new { e.company_id, e.orden_compra_detalle_id }, "ix_alm_compra_oc_detalle");
            entity.HasOne(e => e.orden_detalle).WithMany()
                .HasForeignKey(e => e.orden_compra_detalle_id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.company_id, e.compra_hdr_id }, "ix_alm_compra_hdr_id");
            // La relación con la cabecera se declara desde alm_compra_hdr (HasMany → WithOne).
        });

        modelBuilder.Entity<alm_requisicion>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_requisicion_pkey");
            entity.ToTable("alm_requisicion", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_requisicion_company");
            entity.HasIndex(e => e.numero, "ix_alm_requisicion_numero");
            entity.HasIndex(e => e.codigo_articulo, "ix_alm_requisicion_codigo_articulo");

            entity.Property(e => e.numero).HasPrecision(11, 0);
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(200);
            entity.Property(e => e.aplicacion).HasMaxLength(254);
            entity.Property(e => e.cantidad).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.precio_unitario).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.impuesto_aplica).HasDefaultValue(false);
            entity.Property(e => e.impuesto).HasPrecision(13, 2).HasDefaultValue(0m);
            entity.Property(e => e.descuento_aplica).HasDefaultValue(false);
            entity.Property(e => e.valor_descuento).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(13, 2).HasDefaultValue(0m);
            entity.Property(e => e.tipo_requisicion).HasDefaultValue((short)1);
            entity.Property(e => e.oficina).HasMaxLength(20);
            entity.Property(e => e.departamento).HasMaxLength(3);
            entity.Property(e => e.solicitante).HasMaxLength(120);
            entity.Property(e => e.cargo_solicitante).HasMaxLength(80);
            entity.Property(e => e.diametro).HasMaxLength(80);
            entity.Property(e => e.cuenta_contable).HasMaxLength(30);
            entity.Property(e => e.cuenta_contable_anterior).HasMaxLength(30);
            entity.Property(e => e.cuenta_por_pagar).HasMaxLength(30);
            entity.Property(e => e.fecha_requisicion).HasColumnType("date");
            entity.Property(e => e.fecha_presupuesto).HasColumnType("date");
            entity.Property(e => e.fecha_aprobacion).HasColumnType("date");
            entity.Property(e => e.fecha_rechazo).HasColumnType("date");
            entity.Property(e => e.fecha_entrega).HasColumnType("date");
            entity.Property(e => e.aprobado).HasDefaultValue(false);
            entity.Property(e => e.rechazado).HasDefaultValue(false);
            entity.Property(e => e.descargado).HasDefaultValue(false);
            entity.Property(e => e.estatus).HasMaxLength(1);
            entity.Property(e => e.observacion).HasMaxLength(300);
            entity.Property(e => e.posteado).HasDefaultValue(false);
            entity.Property(e => e.fecha_posteo).HasColumnType("timestamp without time zone");
            // origen sin HasDefaultValue: la columna no tiene DEFAULT en la BD (ver script).
            entity.Property(e => e.origen).HasMaxLength(10);

            // Columnas de la Fase 6 (2026-08-01_alm_requisicion_descargo.sql).
            entity.Property(e => e.cantidad_despachada).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.aplicado_en_oc).HasDefaultValue(false);
            entity.HasOne(e => e.cabecera).WithMany(h => h.lineas)
                .HasForeignKey(e => new { e.company_id, e.requisicion_hdr_id })
                .HasPrincipalKey(h => new { h.company_id, h.id })
                .HasConstraintName("fk_alm_requisicion_hdr")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.articulo_id, "ix_alm_requisicion_articulo");
            entity.HasOne(e => e.articulo_ref).WithMany()
                .HasForeignKey(e => e.articulo_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.company_id, e.uuid },
                "uq_alm_requisicion_company_uuid").IsUnique()
                .HasFilter("uuid IS NOT NULL");
            // ix_alm_requisicion_pendiente lo DROPEA a propósito 2026-08-01_alm_requisicion_descargo.sql
            // (la requisición nunca postea): no se declara aquí para no divergir del esquema real.
            entity.HasIndex(e => e.bodega_id, "ix_alm_requisicion_bodega");
            entity.HasOne(e => e.bodega_ref).WithMany()
                .HasForeignKey(e => e.bodega_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Cabecera de requisición (Fase 6, 2026-08-01_alm_requisicion_descargo.sql) ──────
        modelBuilder.Entity<alm_requisicion_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_requisicion_hdr_pkey");
            entity.ToTable("alm_requisicion_hdr", "public");
            entity.HasIndex(e => new { e.company_id, e.numero }, "uq_alm_requisicion_hdr_numero").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_requisicion_hdr_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.uuid }, "uq_alm_requisicion_hdr_company_uuid")
                .IsUnique().HasFilter("uuid IS NOT NULL");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.fecha_requerida).HasColumnType("date");
            entity.Property(e => e.departamento).HasMaxLength(3);
            entity.Property(e => e.solicitante).HasMaxLength(120);
            entity.Property(e => e.cargo_solicitante).HasMaxLength(80);
            entity.Property(e => e.usuario_solicita).HasMaxLength(100);
            entity.Property(e => e.aplicacion).HasMaxLength(254);
            entity.Property(e => e.observacion).HasMaxLength(1000);
            entity.Property(e => e.aprobado_por).HasMaxLength(100);
            entity.Property(e => e.rechazado_por).HasMaxLength(100);
            entity.Property(e => e.motivo_rechazo).HasMaxLength(500);
            entity.Property(e => e.anulado_por).HasMaxLength(100);
            entity.Property(e => e.motivo_anulacion).HasMaxLength(500);
            entity.Property(e => e.total).HasPrecision(14, 2);
            entity.Property(e => e.fecha_aprobacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_rechazo).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_anulacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_requisicion_correlativo>(entity =>
        {
            entity.HasKey(e => e.company_id).HasName("alm_requisicion_correlativo_pkey");
            entity.ToTable("alm_requisicion_correlativo", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
        });

        modelBuilder.Entity<alm_descargo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_descargo_pkey");
            entity.ToTable("alm_descargo", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_descargo_company");
            entity.HasIndex(e => e.codigo_articulo, "ix_alm_descargo_codigo_articulo");
            entity.HasIndex(e => e.numero_requisicion, "ix_alm_descargo_numero_requisicion");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.precio_unitario).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.oficina).HasMaxLength(5);
            entity.Property(e => e.departamento).HasMaxLength(2);
            entity.Property(e => e.numero_requisicion).HasPrecision(14, 0);
            entity.Property(e => e.numero_documento).HasPrecision(11, 0);
            entity.Property(e => e.traslado).HasMaxLength(1);
            entity.Property(e => e.cuenta_contable_1).HasMaxLength(30);
            entity.Property(e => e.cuenta_contable_1_detalle).HasMaxLength(30);
            entity.Property(e => e.cuenta_contable_2).HasMaxLength(30);
            entity.Property(e => e.cuenta_contable_2_detalle).HasMaxLength(30);
            entity.Property(e => e.comentario).HasMaxLength(254);
            entity.Property(e => e.posteado).HasDefaultValue(false);
            entity.Property(e => e.fecha_posteo).HasColumnType("timestamp without time zone");
            // origen sin HasDefaultValue: la columna no tiene DEFAULT en la BD (ver script).
            entity.Property(e => e.origen).HasMaxLength(10);

            entity.HasIndex(e => e.articulo_id, "ix_alm_descargo_articulo");
            entity.HasOne(e => e.articulo_ref).WithMany()
                .HasForeignKey(e => e.articulo_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => new { e.company_id, e.uuid },
                "uq_alm_descargo_company_uuid").IsUnique()
                .HasFilter("uuid IS NOT NULL");
            entity.HasIndex(e => e.company_id, "ix_alm_descargo_pendiente")
                .HasFilter("origen = 'SIAD' AND posteado = false");
            entity.HasIndex(e => e.bodega_id, "ix_alm_descargo_bodega");
            entity.HasOne(e => e.bodega_ref).WithMany()
                .HasForeignKey(e => e.bodega_id)
                .OnDelete(DeleteBehavior.Restrict);

            // Cabecera del documento nuevo (Fase 6). El renglón es la unidad de posteo.
            entity.HasOne(e => e.cabecera).WithMany(h => h.lineas)
                .HasForeignKey(e => new { e.company_id, e.descargo_hdr_id })
                .HasPrincipalKey(h => new { h.company_id, h.id })
                .HasConstraintName("fk_alm_descargo_hdr")
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Cabecera de descargo (Fase 6) ─────────────────────────────────────────────────
        modelBuilder.Entity<alm_descargo_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_descargo_hdr_pkey");
            entity.ToTable("alm_descargo_hdr", "public");
            entity.HasIndex(e => new { e.company_id, e.numero }, "uq_alm_descargo_hdr_numero").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_descargo_hdr_tenant").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.uuid }, "uq_alm_descargo_hdr_company_uuid")
                .IsUnique().HasFilter("uuid IS NOT NULL");
            entity.HasIndex(e => new { e.company_id, e.requisicion_hdr_id }, "ix_alm_descargo_hdr_requisicion");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.departamento).HasMaxLength(3);
            entity.Property(e => e.entregado_por).HasMaxLength(100);
            entity.Property(e => e.recibido_por).HasMaxLength(120);
            entity.Property(e => e.motivo).HasMaxLength(120);
            entity.Property(e => e.observaciones).HasMaxLength(1000);
            entity.Property(e => e.total).HasPrecision(14, 2);
            entity.Property(e => e.motivo_anulacion).HasMaxLength(500);
            entity.Property(e => e.anulado_por).HasMaxLength(100);
            entity.Property(e => e.fecha_anulacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_posteo).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_descargo_correlativo>(entity =>
        {
            entity.HasKey(e => e.company_id).HasName("alm_descargo_correlativo_pkey");
            entity.ToTable("alm_descargo_correlativo", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
        });

        modelBuilder.Entity<af_activo_fijo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("af_activo_fijo_pkey");
            entity.ToTable("af_activo_fijo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo_activo },
                "uq_af_activo_fijo_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_af_activo_fijo_company");
            entity.HasIndex(e => e.cuenta_contable, "ix_af_activo_fijo_cuenta_contable");

            entity.Property(e => e.codigo_activo).HasMaxLength(50);
            entity.Property(e => e.descripcion).HasMaxLength(254).HasDefaultValue("");
            entity.Property(e => e.tipo).HasMaxLength(15);
            entity.Property(e => e.clase).HasMaxLength(55);
            entity.Property(e => e.modelo).HasMaxLength(30);
            entity.Property(e => e.serie).HasMaxLength(30);
            entity.Property(e => e.propiedades_especiales).HasMaxLength(254);
            entity.Property(e => e.ubicacion).HasMaxLength(50);
            entity.Property(e => e.direccion_foto).HasMaxLength(160);
            entity.Property(e => e.codigo_empleado).HasMaxLength(15);
            entity.Property(e => e.responsable).HasMaxLength(80);
            entity.Property(e => e.cargo_responsable).HasMaxLength(50);
            entity.Property(e => e.origen).HasMaxLength(2);
            entity.Property(e => e.origen_desc).HasMaxLength(60);
            entity.Property(e => e.proveedor).HasMaxLength(80);
            entity.Property(e => e.numero_factura).HasMaxLength(20);
            entity.Property(e => e.numero_cheque).HasPrecision(12, 0);
            entity.Property(e => e.fecha_compra).HasColumnType("date");
            entity.Property(e => e.fecha_cheque).HasColumnType("date");
            entity.Property(e => e.fecha_asignacion).HasColumnType("date");
            entity.Property(e => e.valor_compra).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_rescate).HasPrecision(7, 2).HasDefaultValue(0m);
            entity.Property(e => e.vida_util_anios).HasPrecision(3, 0);
            entity.Property(e => e.vida_util_periodos).HasPrecision(3, 0);
            entity.Property(e => e.meses_depreciados).HasPrecision(2, 0);
            entity.Property(e => e.depreciar).HasDefaultValue(false);
            entity.Property(e => e.fecha_ultima_depreciacion).HasColumnType("date");
            entity.Property(e => e.valor_a_depreciar).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_depreciado).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.depreciacion_acumulada).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.depreciacion_mensual).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.depreciacion_diaria).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_libros).HasPrecision(12, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_libros_alterno).HasPrecision(11, 2);
            entity.Property(e => e.cuenta_contable).HasMaxLength(25);
            entity.Property(e => e.cuenta_contable_anterior).HasMaxLength(30);
            entity.Property(e => e.cuenta_depreciacion).HasMaxLength(25);
            entity.Property(e => e.cuenta_gasto).HasMaxLength(25);
            entity.Property(e => e.descargado).HasDefaultValue(false);
            entity.Property(e => e.fecha_descargo).HasColumnType("date");
            entity.Property(e => e.vendido).HasDefaultValue(false);
            entity.Property(e => e.valor_venta).HasPrecision(11, 2);
            entity.Property(e => e.observacion).HasMaxLength(254);
        });

        modelBuilder.Entity<af_activo_fijo_depreciacion>(entity =>
        {
            entity.HasKey(e => e.id).HasName("af_activo_fijo_depreciacion_pkey");
            entity.ToTable("af_activo_fijo_depreciacion", "public");
            entity.HasIndex(e => e.company_id, "ix_af_activo_fijo_depreciacion_company");
            entity.HasIndex(e => e.activo_fijo_id, "ix_af_activo_fijo_depreciacion_activo");
            entity.HasIndex(e => e.codigo_activo, "ix_af_activo_fijo_depreciacion_codigo");

            entity.Property(e => e.codigo_activo).HasMaxLength(30);
            entity.Property(e => e.fecha_depreciacion).HasColumnType("date");
            entity.Property(e => e.valor_depreciado).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_neto_libros).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.cuenta_depreciacion).HasMaxLength(25);
            entity.Property(e => e.cuenta_gasto).HasMaxLength(25);
            entity.Property(e => e.traslado).HasMaxLength(1);
            entity.Property(e => e.descripcion).HasMaxLength(150);

            entity.HasOne(d => d.activo_fijo).WithMany(p => p.af_activo_fijo_depreciacions)
                .HasForeignKey(d => d.activo_fijo_id)
                .HasConstraintName("af_activo_fijo_depreciacion_activo_fijo_id_fkey");
        });

        modelBuilder.Entity<alm_unidad_medida>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_unidad_medida_pkey");
            entity.ToTable("alm_unidad_medida", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo },
                "uq_alm_unidad_medida_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_unidad_medida_company");

            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(60);
            entity.Property(e => e.abreviatura).HasMaxLength(10);
            entity.Property(e => e.permite_decimales).HasDefaultValue(true);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.factor_conversion).HasPrecision(18, 6).HasDefaultValue(1m);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasIndex(e => e.categoria_id, "ix_alm_unidad_medida_categoria");

            entity.HasOne(e => e.categoria_ref).WithMany(p => p.unidades)
                .HasForeignKey(e => e.categoria_id)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.unidad_base).WithMany(p => p.unidades_derivadas)
                .HasForeignKey(e => e.unidad_base_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<alm_categoria_unidad>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_categoria_unidad_pkey");
            entity.ToTable("alm_categoria_unidad", "public");
            entity.HasIndex(e => new { e.company_id, e.nombre }, "uq_alm_categoria_unidad_company_nombre").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_categoria_unidad_company");
            entity.Property(e => e.nombre).HasMaxLength(30);
            entity.Property(e => e.descripcion).HasMaxLength(100);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_termino_pago>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_termino_pago_pkey");
            entity.ToTable("alm_termino_pago", "public");
            entity.HasIndex(e => new { e.company_id, e.nombre }, "uq_alm_termino_pago_company_nombre").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_termino_pago_company");
            // A lo sumo un término predeterminado por empresa.
            entity.HasIndex(e => e.company_id, "uq_alm_termino_pago_default").IsUnique().HasFilter("es_default");
            entity.Property(e => e.nombre).HasMaxLength(60);
            entity.Property(e => e.dias).HasDefaultValue(0);
            entity.Property(e => e.es_default).HasDefaultValue(false);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_tipo_articulo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_tipo_articulo_pkey");
            entity.ToTable("alm_tipo_articulo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo },
                "uq_alm_tipo_articulo_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_tipo_articulo_company");
            // Índice parcial: solo los tipos con tasa de ISV asignada (2026-07-30).
            entity.HasIndex(e => e.impuesto_tasa_id, "ix_alm_tipo_articulo_impuesto_tasa")
                .HasFilter("impuesto_tasa_id IS NOT NULL");

            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.descripcion).HasMaxLength(200);
            entity.Property(e => e.cuenta_inventario).HasMaxLength(25);
            entity.Property(e => e.cuenta_costo_ventas).HasMaxLength(25);
            entity.Property(e => e.cuenta_ventas).HasMaxLength(25);
            entity.Property(e => e.cuenta_ajustes).HasMaxLength(25);
            entity.Property(e => e.cuenta_devoluciones).HasMaxLength(25);
            entity.Property(e => e.maneja_inventario).HasDefaultValue(true);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // ISV de compras por tipo: FK al catálogo GLOBAL cfg_impuesto_tasa (2026-07-30).
            // ON DELETE RESTRICT, igual criterio que cfg_impuesto_tasa -> cfg_impuesto: una
            // tasa referida por un tipo no puede borrarse en cascada. La tabla destino es
            // global (sin company_id), así que el join no lo filtra el tenant.
            entity.HasOne(e => e.impuesto_tasa)
                .WithMany()
                .HasForeignKey(e => e.impuesto_tasa_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<alm_grupo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_grupo_pkey");
            entity.ToTable("alm_grupo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo },
                "uq_alm_grupo_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_grupo_company");
            entity.HasIndex(e => e.tipo_articulo_id, "ix_alm_grupo_tipo_articulo");

            entity.Property(e => e.codigo).HasMaxLength(6);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // Unificación 2026-07-16: la categoría cuelga del tipo de artículo.
            // linea_id/linea_codigo (legacy) siguen en la BD sin mapear hasta la Fase 3.
            entity.HasOne(e => e.tipo_articulo).WithMany(p => p.grupos)
                .HasForeignKey(e => e.tipo_articulo_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<alm_bodega>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_bodega_pkey");
            entity.ToTable("alm_bodega", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo }, "uq_alm_bodega_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_bodega_company");
            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.direccion).HasMaxLength(200);
            entity.Property(e => e.responsable).HasMaxLength(100);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_articulo_bodega>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_articulo_bodega_pkey");
            entity.ToTable("alm_articulo_bodega", "public");
            entity.HasIndex(e => new { e.company_id, e.articulo_id, e.bodega_id }, "uq_alm_articulo_bodega").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_articulo_bodega_company");
            entity.HasIndex(e => e.articulo_id, "ix_alm_articulo_bodega_articulo");
            entity.HasIndex(e => e.bodega_id, "ix_alm_articulo_bodega_bodega");
            entity.Property(e => e.ubicacion1).HasMaxLength(20);
            entity.Property(e => e.ubicacion2).HasMaxLength(20);
            entity.Property(e => e.ubicacion3).HasMaxLength(20);
            entity.Property(e => e.ubicacion4).HasMaxLength(20);
            entity.Property(e => e.ubicacion5).HasMaxLength(20);
            entity.Property(e => e.existencia).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia_minima).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia_maxima).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.punto_reorden).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia_comprometida).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia_transito).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.costo_promedio).HasPrecision(12, 4).HasDefaultValue(0m);
            entity.Property(e => e.ultimo_costo).HasPrecision(12, 4).HasDefaultValue(0m);
            entity.Property(e => e.principal).HasDefaultValue(false);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.articulo).WithMany(p => p.ubicaciones)
                .HasForeignKey(e => e.articulo_id).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.bodega).WithMany()
                .HasForeignKey(e => e.bodega_id).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<alm_articulo_proveedor>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_articulo_proveedor_pkey");
            entity.ToTable("alm_articulo_proveedor", "public");
            entity.HasIndex(e => new { e.company_id, e.articulo_id, e.cod_proveedor }, "uq_alm_articulo_proveedor").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_articulo_proveedor_company");
            entity.HasIndex(e => e.articulo_id, "ix_alm_articulo_proveedor_articulo");
            entity.HasIndex(e => e.cod_proveedor, "ix_alm_articulo_proveedor_proveedor");
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.codigo_upc).HasMaxLength(40);
            entity.Property(e => e.costo).HasPrecision(12, 4).HasDefaultValue(0m);
            entity.Property(e => e.principal).HasDefaultValue(false);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.articulo).WithMany(p => p.proveedores)
                .HasForeignKey(e => e.articulo_id).OnDelete(DeleteBehavior.Cascade);
        });

        // ── Órdenes de compra (2026-07-30_alm_orden_compra.sql) ──────────────────
        modelBuilder.Entity<alm_orden_compra>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_orden_compra_pkey");
            entity.ToTable("alm_orden_compra", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_orden_compra_company");
            entity.HasIndex(e => new { e.company_id, e.cod_proveedor }, "ix_alm_orden_compra_proveedor");
            entity.HasIndex(e => new { e.company_id, e.estado }, "ix_alm_orden_compra_estado");
            entity.HasIndex(e => new { e.company_id, e.numero }, "uq_alm_orden_compra_numero").IsUnique();
            // Clave alterna por tenant: respaldo de las FK compuestas (detalle -> cabecera).
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_orden_compra_tenant").IsUnique();

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.fecha_emision).HasColumnType("date");
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.terminos_pago).HasMaxLength(100);
            entity.Property(e => e.destino_uso).HasMaxLength(250);
            entity.Property(e => e.calcula_isv).HasDefaultValue(true);
            entity.Property(e => e.descuento).HasPrecision(12, 4).HasDefaultValue(0m);
            entity.Property(e => e.otros_gastos).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.sub_total).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.impuesto).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.estado).HasDefaultValue((short)1);
            entity.Property(e => e.observaciones).HasMaxLength(1000);
            entity.Property(e => e.aprobado_por).HasMaxLength(100);
            entity.Property(e => e.fecha_aprobacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            // FK compuesta tenant en la BD (company_id, orden_compra_id); EF la mapea simple,
            // igual que el resto del módulo — el servicio valida el tenant antes de insertar.
            entity.HasMany(e => e.detalles).WithOne(d => d.orden)
                .HasForeignKey(d => d.orden_compra_id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<alm_orden_compra_detalle>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_orden_compra_detalle_pkey");
            entity.ToTable("alm_orden_compra_detalle", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_orden_compra_detalle_company");
            entity.HasIndex(e => new { e.company_id, e.orden_compra_id }, "ix_alm_orden_compra_detalle_oc");
            entity.HasIndex(e => e.articulo_id, "ix_alm_orden_compra_detalle_articulo");
            // Clave alterna por tenant: respaldo de la FK compuesta desde alm_compra (recepción).
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_orden_compra_detalle_tenant").IsUnique();

            entity.Property(e => e.codigo_upc).HasMaxLength(40);
            entity.Property(e => e.centro_costo).HasMaxLength(40);
            entity.Property(e => e.descripcion).HasMaxLength(250);
            entity.Property(e => e.cantidad_pedida).HasPrecision(14, 4).HasDefaultValue(0m);
            entity.Property(e => e.costo_unitario).HasPrecision(14, 4).HasDefaultValue(0m);
            entity.Property(e => e.impuesto).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.cantidad_aplicada).HasPrecision(14, 4).HasDefaultValue(0m);

            entity.HasOne(e => e.articulo).WithMany()
                .HasForeignKey(e => e.articulo_id)
                .OnDelete(DeleteBehavior.Restrict);
            // La relación con la cabecera se declara desde alm_orden_compra (HasMany → WithOne).
        });

        modelBuilder.Entity<alm_orden_compra_correlativo>(entity =>
        {
            // La PK ES el company_id: un solo contador por empresa.
            entity.HasKey(e => e.company_id).HasName("alm_orden_compra_correlativo_pkey");
            entity.ToTable("alm_orden_compra_correlativo", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
        });

        // ── Recepción de compra (2026-07-31_alm_compra_recepcion.sql) ────────────
        modelBuilder.Entity<alm_compra_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_compra_hdr_pkey");
            entity.ToTable("alm_compra_hdr", "public");
            entity.HasIndex(e => e.company_id, "ix_alm_compra_hdr_company");
            entity.HasIndex(e => new { e.company_id, e.cod_proveedor }, "ix_alm_compra_hdr_proveedor");
            entity.HasIndex(e => new { e.company_id, e.fecha }, "ix_alm_compra_hdr_fecha");
            entity.HasIndex(e => new { e.company_id, e.orden_compra_id }, "ix_alm_compra_hdr_oc");
            entity.HasIndex(e => e.bodega_id, "ix_alm_compra_hdr_bodega");
            entity.HasIndex(e => e.termino_pago_id, "ix_alm_compra_hdr_termino_pago");
            entity.HasIndex(e => new { e.company_id, e.numero }, "uq_alm_compra_hdr_numero").IsUnique();
            // Clave alterna por tenant: respaldo de la FK compuesta desde alm_compra (las líneas).
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_compra_hdr_tenant").IsUnique();
            // Idempotencia del alta.
            entity.HasIndex(e => new { e.company_id, e.uuid }, "uq_alm_compra_hdr_company_uuid").IsUnique()
                .HasFilter("uuid IS NOT NULL");
            // La misma factura del mismo proveedor no se captura dos veces; anular la libera.
            entity.HasIndex(e => new { e.company_id, e.cod_proveedor, e.numero_factura_sar },
                    "uq_alm_compra_hdr_factura_prov").IsUnique()
                .HasFilter("numero_factura_sar IS NOT NULL AND estado <> 9");
            entity.HasIndex(e => e.company_id, "ix_alm_compra_hdr_pendiente")
                .HasFilter("posteado = false AND estado = 1");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.fecha_factura).HasColumnType("date");
            entity.Property(e => e.fecha_vencimiento).HasColumnType("date");
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.proveedor).HasMaxLength(100);
            entity.Property(e => e.numero_factura_sar).HasMaxLength(30);
            entity.Property(e => e.cai).HasMaxLength(50);
            entity.Property(e => e.terminos_pago).HasMaxLength(100);
            entity.Property(e => e.tipo_compra).HasDefaultValue((short)0);
            entity.Property(e => e.condicion_pago).HasDefaultValue(CondicionPagoCompra.Contado);
            entity.Property(e => e.moneda).HasMaxLength(3).HasDefaultValue(MonedaCompra.Lempira);
            entity.Property(e => e.tasa_cambio).HasPrecision(14, 6).HasDefaultValue(1m);
            entity.Property(e => e.consumo_interno).HasDefaultValue(false);
            entity.Property(e => e.descuento).HasPrecision(12, 4).HasDefaultValue(0m);
            entity.Property(e => e.otros_gastos).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.flete_seguro).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.sub_total).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.impuesto).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(14, 2).HasDefaultValue(0m);
            entity.Property(e => e.observaciones).HasMaxLength(1000);
            entity.Property(e => e.estado).HasDefaultValue(EstadoRecepcionCompra.Registrada);
            entity.Property(e => e.posteado).HasDefaultValue(false);
            entity.Property(e => e.fecha_posteo).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.bodega_ref).WithMany()
                .HasForeignKey(e => e.bodega_id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.orden).WithMany()
                .HasForeignKey(e => e.orden_compra_id)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.termino_pago_ref).WithMany()
                .HasForeignKey(e => e.termino_pago_id)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(e => e.lineas).WithOne(l => l.cabecera)
                .HasForeignKey(l => l.compra_hdr_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<alm_compra_cxp>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_compra_cxp_pkey");
            entity.ToTable("alm_compra_cxp", "public");
            entity.HasIndex(e => new { e.company_id, e.compra_hdr_id }, "uq_alm_compra_cxp_factura").IsUnique();
            entity.HasIndex(e => new { e.company_id, e.id }, "uq_alm_compra_cxp_tenant").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_compra_cxp_company");
            entity.HasIndex(e => new { e.company_id, e.cod_proveedor }, "ix_alm_compra_cxp_proveedor");
            entity.HasIndex(e => new { e.company_id, e.fecha_vencimiento }, "ix_alm_compra_cxp_vencimiento");
            entity.HasIndex(e => new { e.company_id, e.estado_id }, "ix_alm_compra_cxp_estado");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.fecha_vencimiento).HasColumnType("date");
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.proveedor).HasMaxLength(100);
            entity.Property(e => e.numero_factura_sar).HasMaxLength(30);
            entity.Property(e => e.condicion_pago).HasDefaultValue(CondicionPagoCompra.Contado);
            entity.Property(e => e.monto).HasPrecision(14, 2);
            entity.Property(e => e.saldo).HasPrecision(14, 2);
            entity.Property(e => e.estado_id).HasDefaultValue(EstadoCompraCxp.Pendiente);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.compra).WithMany()
                .HasForeignKey(e => e.compra_hdr_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<alm_compra_cxp_abono>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_compra_cxp_abono_pkey");
            entity.ToTable("alm_compra_cxp_abono", "public");
            entity.HasIndex(e => new { e.company_id, e.cxp_id, e.numero_abono }, "uq_alm_compra_cxp_abono_num").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_compra_cxp_abono_company");
            entity.HasIndex(e => new { e.company_id, e.cxp_id }, "ix_alm_compra_cxp_abono_cxp");

            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.monto).HasPrecision(14, 2);
            entity.Property(e => e.metodo_pago).HasMaxLength(20);
            entity.Property(e => e.num_cheque).HasMaxLength(20);
            entity.Property(e => e.estado).HasMaxLength(1).HasDefaultValue("V");
            entity.Property(e => e.motivo_anulacion).HasMaxLength(300);
            entity.Property(e => e.observaciones).HasMaxLength(300);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuarioanulacion).HasMaxLength(100);
            entity.Property(e => e.fechaanulacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.cxp).WithMany(c => c.abonos)
                .HasForeignKey(e => e.cxp_id)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<alm_compra_correlativo>(entity =>
        {
            // La PK ES el company_id: un solo contador por empresa.
            entity.HasKey(e => e.company_id).HasName("alm_compra_correlativo_pkey");
            entity.ToTable("alm_compra_correlativo", "public");
            entity.Property(e => e.company_id).ValueGeneratedNever();
        });
    }
}
