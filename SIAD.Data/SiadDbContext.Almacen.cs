using Microsoft.EntityFrameworkCore;
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
    public virtual DbSet<af_activo_fijo> af_activo_fijos { get; set; } = null!;
    public virtual DbSet<af_activo_fijo_depreciacion> af_activo_fijo_depreciacions { get; set; } = null!;
    public virtual DbSet<alm_unidad_medida> alm_unidad_medidas { get; set; } = null!;
    public virtual DbSet<alm_tipo_articulo> alm_tipo_articulos { get; set; } = null!;
    public virtual DbSet<alm_linea> alm_lineas { get; set; } = null!;
    public virtual DbSet<alm_grupo> alm_grupos { get; set; } = null!;
    public virtual DbSet<alm_bodega> alm_bodegas { get; set; } = null!;
    public virtual DbSet<alm_estanteria> alm_estanterias { get; set; } = null!;
    public virtual DbSet<alm_estante> alm_estantes { get; set; } = null!;
    public virtual DbSet<alm_articulo_bodega> alm_articulo_bodegas { get; set; } = null!;

    private void ConfigureAlmacenModel(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<alm_articulo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_articulo_pkey");
            entity.ToTable("alm_articulo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo_articulo },
                "uq_alm_articulo_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_articulo_company");

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

            entity.HasIndex(e => e.linea_id, "ix_alm_articulo_linea_id");
            entity.HasOne(e => e.linea_ref).WithMany()
                .HasForeignKey(e => e.linea_id)
                .OnDelete(DeleteBehavior.SetNull);

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

            entity.Property(e => e.numero_documento).HasPrecision(11, 0);
            entity.Property(e => e.tipo_transaccion).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("date");
            entity.Property(e => e.codigo_articulo).HasMaxLength(20);
            entity.Property(e => e.cantidad).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.bodega).HasMaxLength(2);
            entity.Property(e => e.ingresos).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.salidas).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.valor_unitario).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.total).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.debe).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.haber).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.cuenta_contable).HasMaxLength(25);
            entity.Property(e => e.departamento).HasMaxLength(3);
            entity.Property(e => e.departamento_desc).HasMaxLength(100);
            entity.Property(e => e.linea).HasMaxLength(2);
            entity.Property(e => e.linea_desc).HasMaxLength(150);
            entity.Property(e => e.barrio).HasMaxLength(3);
            entity.Property(e => e.es_ajuste).HasDefaultValue(false);
            entity.Property(e => e.descripcion).HasMaxLength(120);
            entity.Property(e => e.observacion).HasMaxLength(254);

            entity.HasOne(e => e.bodega_ref).WithMany()
                .HasForeignKey(e => e.bodega_id)
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
            entity.Property(e => e.categoria).HasMaxLength(30);
            entity.Property(e => e.factor_conversion).HasPrecision(18, 6).HasDefaultValue(1m);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.unidad_base).WithMany(p => p.unidades_derivadas)
                .HasForeignKey(e => e.unidad_base_id)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<alm_tipo_articulo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_tipo_articulo_pkey");
            entity.ToTable("alm_tipo_articulo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo },
                "uq_alm_tipo_articulo_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_tipo_articulo_company");

            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(60);
            entity.Property(e => e.descripcion).HasMaxLength(200);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_linea>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_linea_pkey");
            entity.ToTable("alm_linea", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo },
                "uq_alm_linea_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_linea_company");

            entity.Property(e => e.codigo).HasMaxLength(2);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.cuenta_contable).HasMaxLength(25);
            entity.Property(e => e.cuenta_contable_anterior).HasMaxLength(30);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<alm_grupo>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_grupo_pkey");
            entity.ToTable("alm_grupo", "public");
            entity.HasIndex(e => new { e.company_id, e.codigo },
                "uq_alm_grupo_company_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_grupo_company");
            entity.HasIndex(e => e.linea_id, "ix_alm_grupo_linea");

            entity.Property(e => e.codigo).HasMaxLength(6);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.linea_codigo).HasMaxLength(2);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.linea).WithMany(p => p.grupos)
                .HasForeignKey(e => e.linea_id)
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

        modelBuilder.Entity<alm_estanteria>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_estanteria_pkey");
            entity.ToTable("alm_estanteria", "public");
            entity.HasIndex(e => new { e.company_id, e.bodega_id, e.codigo }, "uq_alm_estanteria_company_bodega_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_estanteria_company");
            entity.HasIndex(e => e.bodega_id, "ix_alm_estanteria_bodega");
            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.HasOne(e => e.bodega).WithMany(p => p.estanterias)
                .HasForeignKey(e => e.bodega_id).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<alm_estante>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_estante_pkey");
            entity.ToTable("alm_estante", "public");
            entity.HasIndex(e => new { e.company_id, e.estanteria_id, e.codigo }, "uq_alm_estante_company_estanteria_codigo").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_estante_company");
            entity.HasIndex(e => e.estanteria_id, "ix_alm_estante_estanteria");
            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.descripcion).HasMaxLength(150);
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.HasOne(e => e.estanteria).WithMany(p => p.estantes)
                .HasForeignKey(e => e.estanteria_id).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<alm_articulo_bodega>(entity =>
        {
            entity.HasKey(e => e.id).HasName("alm_articulo_bodega_pkey");
            entity.ToTable("alm_articulo_bodega", "public");
            entity.HasIndex(e => new { e.company_id, e.articulo_id, e.bodega_id }, "uq_alm_articulo_bodega").IsUnique();
            entity.HasIndex(e => e.company_id, "ix_alm_articulo_bodega_company");
            entity.HasIndex(e => e.articulo_id, "ix_alm_articulo_bodega_articulo");
            entity.HasIndex(e => e.bodega_id, "ix_alm_articulo_bodega_bodega");
            entity.HasIndex(e => e.estante_id, "ix_alm_articulo_bodega_estante");
            entity.Property(e => e.existencia).HasPrecision(15, 2).HasDefaultValue(0m);
            entity.Property(e => e.existencia_minima).HasPrecision(11, 2).HasDefaultValue(0m);
            entity.Property(e => e.principal).HasDefaultValue(false);
            entity.Property(e => e.usuariocreacion).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariomodificacion).HasMaxLength(100);
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");

            entity.HasOne(e => e.articulo).WithMany(p => p.ubicaciones)
                .HasForeignKey(e => e.articulo_id).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.bodega).WithMany()
                .HasForeignKey(e => e.bodega_id).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.estante).WithMany()
                .HasForeignKey(e => e.estante_id).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
