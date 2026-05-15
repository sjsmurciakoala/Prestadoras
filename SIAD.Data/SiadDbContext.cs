using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;

namespace SIAD.Data;

public partial class SiadDbContext : DbContext
{

    public SiadDbContext()
    {
    }

    public SiadDbContext(DbContextOptions<SiadDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<abogado> abogados { get; set; }

    public virtual DbSet<ajuste> ajustes { get; set; }

    public virtual DbSet<ajustes_detalle> ajustes_detalles { get; set; }

    public virtual DbSet<axl_accion_cobranza> axl_accion_cobranzas { get; set; }

    public virtual DbSet<axl_observacion_cobranza> axl_observacion_cobranzas { get; set; }

    public virtual DbSet<barrio> barrios { get; set; }
    public virtual DbSet<ban_banco> ban_banco { get; set; }

    public virtual DbSet<ban_config> ban_config { get; set;}
 
    public virtual DbSet<ban_cta> ban_cta { get; set;}

    public virtual DbSet<ban_cuenta> ban_cuenta { get;set; }

    public virtual DbSet<ban_moneda> ban_moneda { get;set; }

    public virtual DbSet<ban_movimiento> ban_movimiento { get; set;}

    public virtual DbSet<ban_kardex> ban_kardex { get; set; }

    public virtual DbSet<ban_movimiento_detalle> ban_movimiento_detalle { get; set;}

    public virtual DbSet<ban_movimiento_transito> ban_movimiento_transito { get; set; }

    public virtual DbSet<ban_tipos_transacciones> ban_tipos_transacciones { get; set; }

    public virtual DbSet<bnc_banco> bnc_bancos { get; set; }

    public virtual DbSet<bnc_cuenta> bnc_cuentas { get; set; }

    public virtual DbSet<bnc_kardex_cuenta> bnc_kardex_cuentas { get; set; }

    public virtual DbSet<bnc_tipotransaccione> bnc_tipotransacciones { get; set; }

    public virtual DbSet<cai> cais { get; set; }

    public virtual DbSet<calendariopro> calendariopros { get; set; }

    public virtual DbSet<categoria_servicio> categoria_servicios { get; set; }

    public virtual DbSet<catalogo_caja> catalogo_cajas { get; set; }

    public virtual DbSet<cfg_company> cfg_companies { get; set; }

    public virtual DbSet<causa_refacturacion> causa_refacturacions { get; set; }

    public virtual DbSet<ciclo> ciclos { get; set; }

    public virtual DbSet<cliente_detalle> cliente_detalles { get; set; }

    public virtual DbSet<cliente_maestro> cliente_maestros { get; set; }

    public virtual DbSet<cln_accion_cobranza> cln_accion_cobranzas { get; set; }

    public virtual DbSet<cln_plan_pago_dtl> cln_plan_pago_dtls { get; set; }

    public virtual DbSet<cln_plan_pago_hdr> cln_plan_pago_hdrs { get; set; }

    public virtual DbSet<cnt_balance> cnt_balances { get; set; }

    public virtual DbSet<cnt_catalogo> cnt_catalogos { get; set; }

    public virtual DbSet<cnt_centro_costos_grupo> cnt_centro_costos_grupos { get; set; }

    public virtual DbSet<cnt_centro_costos_subgrupo> cnt_centro_costos_subgrupos { get; set; }

    public virtual DbSet<cnt_centrocostos_dtl> cnt_centrocostos_dtls { get; set; }

    public virtual DbSet<cnt_centrocostos_hdr> cnt_centrocostos_hdrs { get; set; }

    public virtual DbSet<cnt_centroscosto> cnt_centroscostos { get; set; }

    public virtual DbSet<cnt_grupo_ctum> cnt_grupo_cta { get; set; }

    public virtual DbSet<cnt_mayore> cnt_mayores { get; set; }

    public virtual DbSet<cnt_partidas_dtl> cnt_partidas_dtls { get; set; }

    public virtual DbSet<cnt_partidas_hdr> cnt_partidas_hdrs { get; set; }

    public virtual DbSet<cnt_rubro> cnt_rubros { get; set; }

    public virtual DbSet<cnt_saldo> cnt_saldos { get; set; }

    public virtual DbSet<cnt_sub_cuentum> cnt_sub_cuenta { get; set; }

    public virtual DbSet<cnt_sub_grupo> cnt_sub_grupos { get; set; }

    public virtual DbSet<cnt_tipopartidum> cnt_tipopartida { get; set; }

    public virtual DbSet<concepto_cobro_adicional> concepto_cobro_adicionals { get; set; }

    // [Sprint1/FaseC 2026-05-05] Removidos DbSets legacy:
    //   condicion_lectura, configuracion_app_lectura_medidore,
    //   configuracion_tasa, configuracion_tasas_detalle.

    public virtual DbSet<configuracion_cobros_adicionale> configuracion_cobros_adicionales { get; set; }

    public virtual DbSet<configuracion_cobros_adicionales_detalle> configuracion_cobros_adicionales_detalles { get; set; }

    public virtual DbSet<coordenadas_empleado> coordenadas_empleados { get; set; }

    public virtual DbSet<factura> facturas { get; set; }

    public virtual DbSet<factura_detalle> factura_detalles { get; set; }

    public virtual DbSet<grupo_estado_detalle> grupo_estado_detalles { get; set; }

    public virtual DbSet<grupoestado> grupoestados { get; set; }

    public virtual DbSet<grupoestadodetalle> grupoestadodetalles { get; set; }

    public virtual DbSet<historialme> historialmes { get; set; }

    public virtual DbSet<historicomedicion> historicomedicions { get; set; }

    public virtual DbSet<historicosinmedidor> historicosinmedidors { get; set; }

    public virtual DbSet<identityrole> identityroles { get; set; }

    public virtual DbSet<identityroleclaim_string_> identityroleclaim_string_s { get; set; }

    public virtual DbSet<identityuser> identityusers { get; set; }

    public virtual DbSet<identityuserclaim_string_> identityuserclaim_string_s { get; set; }

    public virtual DbSet<identityuserlogin_string_> identityuserlogin_string_s { get; set; }

    public virtual DbSet<identityusertoken_string_> identityusertoken_string_s { get; set; }

    // [Sprint1/FaseC 2026-05-05] Removidos DbSets legacy: informativo, letra.

    public virtual DbSet<log_cliclo_descarga_app> log_cliclo_descarga_apps { get; set; }

    public virtual DbSet<maestro_medidor> maestro_medidors { get; set; }

    public virtual DbSet<materialesapp> materialesapps { get; set; }

    public virtual DbSet<miscelaneos_catalogo> miscelaneos_catalogos { get; set; }

    public virtual DbSet<ops_compromiso> ops_compromisos { get; set; }

    public virtual DbSet<orden_trabajo> orden_trabajos { get; set; }

    public virtual DbSet<orden_trabajo_adjunto> orden_trabajo_adjuntos { get; set; }

    public virtual DbSet<orden_trabajo_estado> orden_trabajo_estados { get; set; }

    public virtual DbSet<ordent_mate> ordent_mates { get; set; }

    public virtual DbSet<pagos_banco> pagos_bancos { get; set; }

    public virtual DbSet<pagos_dtl> pagos_dtls { get; set; }

    public virtual DbSet<pagos_hdr> pagos_hdrs { get; set; }

    public virtual DbSet<pagos_miscelaneo> pagos_miscelaneos { get; set; }

    public virtual DbSet<pagos_miscelaneos_dtl> pagos_miscelaneos_dtl { get; set; }

    public virtual DbSet<pagovariostemp> pagovariostemps { get; set; }

    public virtual DbSet<presupuesto> presupuestos { get; set; }

    public virtual DbSet<presupuesto_fondo> presupuesto_fondos { get; set; }

    public virtual DbSet<proyecto> proyectos { get; set; }

    public virtual DbSet<pruebainsert> pruebainserts { get; set; }

    public virtual DbSet<prv_compromiso_dtl> prv_compromiso_dtls { get; set; }

    public virtual DbSet<prv_compromiso_hdr> prv_compromiso_hdrs { get; set; }

    public virtual DbSet<prv_kardex> prv_kardices { get; set; }

    public virtual DbSet<prv_proveedore> prv_proveedores { get; set; }

    public virtual DbSet<prv_tipoproveedor> prv_tipoproveedors { get; set; }

    public virtual DbSet<prv_tipostransacc> prv_tipostransaccs { get; set; }

    public virtual DbSet<recolectora> recolectoras { get; set; }

    public virtual DbSet<ruta> rutas { get; set; }

    public virtual DbSet<servicio> servicios { get; set; }

    // [Sprint1/FaseC 2026-05-05] Removidos DbSets legacy:
    //   servicios_roles_ws, tarifa, tarifas_contador.

    public virtual DbSet<solicitud_servicio> solicitud_servicios { get; set; }

    public virtual DbSet<tipo_d> tipo_ds { get; set; }

    public virtual DbSet<tipo_transaccion> tipo_transaccions { get; set; }

    public virtual DbSet<tipo_uso_servicio> tipo_uso_servicios { get; set; }

    public virtual DbSet<transaccion_abonado> transaccion_abonados { get; set; }

    public virtual DbSet<transaccion_presupuesto> transaccion_presupuestos { get; set; }

    public virtual DbSet<usuarioapc> usuarioapcs { get; set; }

    public virtual DbSet<usuarios_miorden> usuarios_miordens { get; set; }

    public virtual DbSet<usuarios_tipotransaccion_dtl> usuarios_tipotransaccion_dtls { get; set; }

    public virtual DbSet<usuarios_tipotransaccion_hdr> usuarios_tipotransaccion_hdrs { get; set; }

    public virtual DbSet<view_bus_codigo> view_bus_codigos { get; set; }

    public virtual DbSet<view_centro_costo> view_centro_costos { get; set; }

    public virtual DbSet<view_get_compromisos_dtl> view_get_compromisos_dtls { get; set; }

    public virtual DbSet<view_get_compromisos_hdr> view_get_compromisos_hdrs { get; set; }

    public virtual DbSet<vw_listaplanespago> vw_listaplanespagos { get; set; }
    public virtual DbSet<portal_branding> portal_brandings { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<abogado>(entity =>
        {
            entity.HasKey(e => e.abogado_id).HasName("abogado_id_pkey");

            entity.ToTable("abogado");

            entity.Property(e => e.abogado_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.abogado_codigo).HasMaxLength(50);
            entity.Property(e => e.abogado_nombrecorto).HasMaxLength(100);
            entity.Property(e => e.abogado_nombrelargo).HasMaxLength(300);
            entity.Property(e => e.abogado_telefono).HasMaxLength(11);
            entity.Property(e => e.codcuenta).HasMaxLength(100);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<ajuste>(entity =>
        {
            entity.HasKey(e => e.documento).HasName("ajustes_pkey");

            entity.Property(e => e.documento).UseIdentityAlwaysColumn();
            entity.Property(e => e.cliente_clave).HasMaxLength(20);
            entity.Property(e => e.correlativo).HasMaxLength(6);
            entity.Property(e => e.estado).HasColumnType("character varying");
            entity.Property(e => e.lectura).HasPrecision(18, 2);
            entity.Property(e => e.observacion).HasColumnType("character varying");
            entity.Property(e => e.periodo).HasColumnType("character varying");
            entity.Property(e => e.saldo).HasPrecision(18, 2);
            entity.Property(e => e.total).HasPrecision(18, 2);
            entity.Property(e => e.usuario).HasColumnType("character varying");
        });

        modelBuilder.Entity<ajustes_detalle>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("ajustes_detalle_pkey");

            entity.ToTable("ajustes_detalle");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.monto).HasPrecision(18, 2);
            entity.Property(e => e.tipo_servicio).HasColumnType("character varying");
        });

        modelBuilder.Entity<axl_accion_cobranza>(entity =>
        {
            entity.HasKey(e => e.cod_accion).HasName("axl_accion_cobranza_pkey");

            entity.ToTable("axl_accion_cobranza");

            entity.Property(e => e.nombre).HasMaxLength(150);
        });

        modelBuilder.Entity<axl_observacion_cobranza>(entity =>
        {
            entity.HasKey(e => e.id).HasName("axl_observacion_cobranza_pkey");

            entity.ToTable("axl_observacion_cobranza");

            entity.Property(e => e.observacion).HasMaxLength(50);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<barrio>(entity =>
        {
            entity.HasKey(e => e.barrio_codigo).HasName("barrio_pkey");

            entity.ToTable("barrio");

            entity.Property(e => e.barrio_codigo).HasMaxLength(7);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<bnc_banco>(entity =>
        {
            entity.HasKey(e => e.cod_banco).HasName("bnc_bancos_pkey");

            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.observaciones).HasMaxLength(200);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<bnc_cuenta>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.codigo).HasMaxLength(10);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(150);
            entity.Property(e => e.numero_cheque).HasMaxLength(20);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ruta_transito).HasMaxLength(20);
            entity.Property(e => e.tipo_cuenta).HasMaxLength(150);
        });

        modelBuilder.Entity<bnc_kardex_cuenta>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.conciliada).HasMaxLength(1);
            entity.Property(e => e.correlativo).HasMaxLength(10);
            entity.Property(e => e.fecha_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_transaccion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.num_cheque).HasMaxLength(12);
            entity.Property(e => e.observaciones).HasMaxLength(250);
            entity.Property(e => e.pda).HasMaxLength(1);
            entity.Property(e => e.referencia1).HasMaxLength(100);
            entity.Property(e => e.referencia2).HasMaxLength(100);
            entity.Property(e => e.referencia_afecta).HasMaxLength(100);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.tipo_transacion).HasMaxLength(10);
            entity.Property(e => e.tipo_transacion2).HasMaxLength(10);
            entity.Property(e => e.ultima_trn).HasMaxLength(1);
            entity.Property(e => e.usuario_creo).HasMaxLength(100);
        });

        modelBuilder.Entity<bnc_tipotransaccione>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.cod_centrocosto).HasMaxLength(10);
            entity.Property(e => e.correlativo).HasMaxLength(10);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.del_sistema).HasMaxLength(1);
            entity.Property(e => e.destino).HasMaxLength(9);
            entity.Property(e => e.emite_cheque).HasMaxLength(1);
            entity.Property(e => e.entra_sale).HasMaxLength(1);
            entity.Property(e => e.fecha_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.pad).HasMaxLength(1);
            entity.Property(e => e.pda).HasMaxLength(1);
            entity.Property(e => e.rel_empleados).HasMaxLength(1);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.tipo_transaccion).HasMaxLength(3);
            entity.Property(e => e.trn_prestamo).HasMaxLength(1);
            entity.Property(e => e.usuario_creo).HasMaxLength(100);
            entity.Property(e => e.usuario_modifica).HasMaxLength(100);
        });

        modelBuilder.Entity<cai>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("cai_pkey");

            entity.ToTable("cai");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.cai1)
                .HasMaxLength(60)
                .HasColumnName("cai");
            entity.Property(e => e.codigo_base).HasMaxLength(50);
            entity.Property(e => e.ruta).HasMaxLength(10);
        });

        modelBuilder.Entity<calendariopro>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("calendariopro_pkey");

            entity.ToTable("calendariopro");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.ciclo).HasColumnType("character varying");
        });

        modelBuilder.Entity<categoria_servicio>(entity =>
        {
            entity.HasKey(e => e.categoria_servicio_id).HasName("categoria_servicio_pkey");

            entity.ToTable("categoria_servicio");

            entity.Property(e => e.categoria_servicio_id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(9L, null, null, null, null, null);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<causa_refacturacion>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("causa_refacturacion_pkey");

            entity.ToTable("causa_refacturacion");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.codigo).HasColumnType("character varying");
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.tipo).HasColumnType("character varying");
        });

        modelBuilder.Entity<ciclo>(entity =>
        {
            entity.HasKey(e => e.ciclos_id).HasName("ciclos_id_pkey");

            entity.Property(e => e.ciclos_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ciclos_codigo).HasMaxLength(50);
            entity.Property(e => e.ciclos_descripcioncorta).HasMaxLength(100);
            entity.Property(e => e.ciclos_descripcionlarga).HasMaxLength(300);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<cliente_detalle>(entity =>
        {
            entity.HasKey(e => e.detalle_cliente_id).HasName("cliente_detalle_pkey");

            entity.ToTable("cliente_detalle");

            entity.Property(e => e.detalle_cliente_id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(46839L, null, null, null, null, null);
            entity.Property(e => e.clave).HasMaxLength(32767);
            entity.Property(e => e.descuento_valor).HasPrecision(18, 2);
            entity.Property(e => e.detalle_cliente_color_casa).HasMaxLength(100);
            entity.Property(e => e.detalle_cliente_direccion).HasMaxLength(200);
            entity.Property(e => e.detalle_cliente_email).HasMaxLength(100);
            entity.Property(e => e.detalle_cliente_inquilino).HasMaxLength(100);
            entity.Property(e => e.detalle_cliente_movil).HasMaxLength(15);
            entity.Property(e => e.detalle_cliente_telefono).HasMaxLength(15);
            entity.Property(e => e.empresa_direccion).HasMaxLength(200);
            entity.Property(e => e.empresa_nombre).HasMaxLength(100);
            entity.Property(e => e.empresa_telefono).HasMaxLength(50);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.negocio_clave_catastral).HasMaxLength(50);
            entity.Property(e => e.negocio_nombre).HasMaxLength(100);
            entity.Property(e => e.negocio_telefono).HasMaxLength(15);
            entity.Property(e => e.numero_contrato).HasColumnType("character varying");
            entity.Property(e => e.observaciones).HasColumnType("character varying");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);

            entity.HasOne(d => d.maestro_cliente).WithMany(p => p.cliente_detalles)
                .HasForeignKey(d => d.maestro_cliente_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("maestro_cliente_id_cliente_detalle_fkey");

            entity.HasOne(d => d.maestro_medidor).WithMany(p => p.cliente_detalles)
                .HasForeignKey(d => d.maestro_medidor_id)
                .HasConstraintName("maestro_medidor_id_cliente_detalle_fkey");
        });

        modelBuilder.Entity<cliente_maestro>(entity =>
        {
            entity.HasKey(e => e.maestro_cliente_id).HasName("cliente_maestro_pkey");

            entity.ToTable("cliente_maestro");

            entity.HasIndex(e => e.maestro_cliente_clave, "cliente_maestro_unique").IsUnique();

            entity.Property(e => e.maestro_cliente_id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(102789L, null, null, null, null, null);
            entity.Property(e => e.barrio_codigo).HasMaxLength(7);
            entity.Property(e => e.bloqueado_cobranza).HasDefaultValue(false);
            entity.Property(e => e.clave_sure).HasMaxLength(40);
            entity.Property(e => e.cliente_fecha_nac).HasColumnType("timestamp without time zone");
            entity.Property(e => e.contador).HasMaxLength(50);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.letracodigo).HasMaxLength(10);
            entity.Property(e => e.maestro_cliente_clave).HasMaxLength(20);
            entity.Property(e => e.maestro_cliente_fecha_baja).HasColumnType("timestamp without time zone");
            entity.Property(e => e.maestro_cliente_indicativo_ruta).HasMaxLength(25);
            entity.Property(e => e.maestro_cliente_secuencia).HasMaxLength(6);
            entity.Property(e => e.maestro_cliente_tiene_medidor).HasDefaultValue(false);
            entity.Property(e => e.tipo_uso_codigo).HasMaxLength(2);
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);

            entity.HasOne(d => d.barrio_codigoNavigation).WithMany(p => p.cliente_maestros)
                .HasForeignKey(d => d.barrio_codigo)
                .HasConstraintName("barrio_codigo_cliente_maestro_fkey");

            entity.HasOne(d => d.categoria_servicio).WithMany(p => p.cliente_maestros)
                .HasForeignKey(d => d.categoria_servicio_id)
                .HasConstraintName("categoria_servicio_id_cliente_maestro_fkey");

            entity.HasOne(d => d.ciclos).WithMany(p => p.cliente_maestros)
                .HasForeignKey(d => d.ciclos_id)
                .HasConstraintName("ciclos_id_cliente_maestro_fkey");

            entity.HasOne(d => d.tipo_uso_codigoNavigation).WithMany(p => p.cliente_maestros)
                .HasForeignKey(d => d.tipo_uso_codigo)
                .HasConstraintName("tipo_uso_codigo_cliente_maestro_fkey");
        });

        modelBuilder.Entity<cln_accion_cobranza>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_accion_cobranza_pkey");

            entity.ToTable("cln_accion_cobranza");

            entity.Property(e => e.accion).HasMaxLength(200);
            entity.Property(e => e.codigocliente).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.observacion).HasMaxLength(200);
        });

        modelBuilder.Entity<cln_plan_pago_dtl>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_plan_pago_dtl_pkey");

            entity.ToTable("cln_plan_pago_dtl");

            entity.Property(e => e.estadopago).HasMaxLength(20);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechacuota).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(50);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(50);
            entity.Property(e => e.valorcuota).HasPrecision(10, 2);

            entity.HasOne(d => d.idhdrNavigation).WithMany(p => p.cln_plan_pago_dtls)
                .HasForeignKey(d => d.idhdr)
                .HasConstraintName("cln_plan_pago_dtl_idhdr_fkey");
        });

        modelBuilder.Entity<cln_plan_pago_hdr>(entity =>
        {
            entity.HasKey(e => e.id).HasName("cln_plan_pago_hdr_pkey");

            entity.ToTable("cln_plan_pago_hdr");

            entity.Property(e => e.correlativo).HasMaxLength(6);
            entity.Property(e => e.direccion).HasMaxLength(300);
            entity.Property(e => e.docrepresentante).HasMaxLength(20);
            entity.Property(e => e.estadopago).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechappago).HasColumnType("timestamp without time zone");
            entity.Property(e => e.monto).HasPrecision(10, 2);
            entity.Property(e => e.montofinanc).HasPrecision(10, 2);
            entity.Property(e => e.numrepresentante).HasMaxLength(11);
            entity.Property(e => e.porcprima).HasPrecision(10, 2);
            entity.Property(e => e.representante).HasMaxLength(200);
            entity.Property(e => e.usuariocreacion).HasMaxLength(50);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(50);
            entity.Property(e => e.vprima).HasPrecision(10, 2);

            entity.HasOne(d => d.cliente).WithMany(p => p.cln_plan_pago_hdrs)
                .HasForeignKey(d => d.clienteid)
                .HasConstraintName("cln_plan_pago_hdr_clienteid_fkey");
        });

        modelBuilder.Entity<cnt_balance>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("cnt_balance");

            entity.Property(e => e.cod_cuenta).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(100);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_catalogo>(entity =>
        {
            entity.HasKey(e => e.cod_cuenta).HasName("cnt_catalogo_pkey");

            entity.ToTable("cnt_catalogo");

            entity.Property(e => e.cod_cuenta).HasMaxLength(20);
            entity.Property(e => e.cod_grupo_cta).HasMaxLength(1);
            entity.Property(e => e.cod_mayor).HasMaxLength(15);
            entity.Property(e => e.cod_sub_grupo).HasMaxLength(15);
            entity.Property(e => e.cscuenta).HasMaxLength(15);
            entity.Property(e => e.cuenta_ext).HasMaxLength(1);
            entity.Property(e => e.flag_budget).HasMaxLength(1);
            entity.Property(e => e.flag_fijovariable).HasMaxLength(1);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.status).HasMaxLength(1);
            entity.Property(e => e.ult_usuario).HasMaxLength(8);
            entity.Property(e => e.usuario_creo).HasMaxLength(8);
        });

        modelBuilder.Entity<cnt_centro_costos_grupo>(entity =>
        {
            entity.HasKey(e => e.codccg).HasName("cnt_centro_costos_grupo_pkey");

            entity.ToTable("cnt_centro_costos_grupo");

            entity.Property(e => e.codccg).HasMaxLength(2);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_centro_costos_subgrupo>(entity =>
        {
            entity.HasKey(e => new { e.codccg, e.codsccg }).HasName("cnt_centro_costos_subgrupo_pkey");

            entity.ToTable("cnt_centro_costos_subgrupo");

            entity.Property(e => e.codccg).HasMaxLength(2);
            entity.Property(e => e.codsccg).HasMaxLength(2);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_centrocostos_dtl>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("cnt_centrocostos_dtl");

            entity.Property(e => e.ampl).HasColumnType("money");
            entity.Property(e => e.aprobado).HasColumnType("money");
            entity.Property(e => e.compro).HasColumnType("money");
            entity.Property(e => e.cuenta).HasMaxLength(9);
            entity.Property(e => e.fondo).HasColumnType("money");
            entity.Property(e => e.mov).HasColumnType("money");
            entity.Property(e => e.nuevoaprobado).HasColumnType("money");
            entity.Property(e => e.obs).HasColumnType("money");
            entity.Property(e => e.pagado).HasColumnType("money");
            entity.Property(e => e.proyeccion).HasColumnType("money");
            entity.Property(e => e.saldo).HasColumnType("money");
            entity.Property(e => e.tipo).HasMaxLength(10);
            entity.Property(e => e.transfe).HasColumnType("money");
            entity.Property(e => e.valor).HasColumnType("money");
        });

        modelBuilder.Entity<cnt_centrocostos_hdr>(entity =>
        {
            entity.HasKey(e => e.cuenta).HasName("cnt_centrocostos_hdr_pkey");

            entity.ToTable("cnt_centrocostos_hdr");

            entity.Property(e => e.cuenta).HasMaxLength(9);
            entity.Property(e => e.codccg).HasMaxLength(2);
            entity.Property(e => e.codsccg).HasMaxLength(3);
            entity.Property(e => e.contable).HasMaxLength(20);
            entity.Property(e => e.nombre).HasMaxLength(150);
        });

        modelBuilder.Entity<cnt_centroscosto>(entity =>
        {
            entity.HasKey(e => e.cod_centrocosto).HasName("cnt_centroscosto_pkey");

            entity.ToTable("cnt_centroscosto");

            entity.Property(e => e.nom_centrocosto).HasMaxLength(250);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.status).HasMaxLength(1);
        });

        modelBuilder.Entity<cnt_grupo_ctum>(entity =>
        {
            entity.HasKey(e => e.cod_grupo_cta).HasName("cnt_grupo_cta_pkey");

            entity.Property(e => e.cod_grupo_cta).HasMaxLength(1);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_mayore>(entity =>
        {
            entity.HasKey(e => new { e.cod_grupo_cta, e.cod_sub_grupo, e.cod_mayor }).HasName("cnt_mayores_pkey");

            entity.Property(e => e.cod_grupo_cta).HasMaxLength(15);
            entity.Property(e => e.cod_sub_grupo).HasMaxLength(15);
            entity.Property(e => e.cod_mayor).HasMaxLength(15);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.partida_resumen).HasMaxLength(1);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_partidas_dtl>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("cnt_partidas_dtl");

            entity.Property(e => e.cargos).HasPrecision(15, 4);
            entity.Property(e => e.cod_centrocosto).HasMaxLength(10);
            entity.Property(e => e.cod_cliente).HasMaxLength(6);
            entity.Property(e => e.cod_cuenta).HasMaxLength(20);
            entity.Property(e => e.cod_marcagrupo).HasMaxLength(4);
            entity.Property(e => e.comprobante).HasMaxLength(1000);
            entity.Property(e => e.concepto).HasMaxLength(1000);
            entity.Property(e => e.creditos).HasPrecision(15, 4);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_partidas_hdr>(entity =>
        {
            entity.HasKey(e => e.cod_partida).HasName("cnt_partidas_hdr_pkey");

            entity.ToTable("cnt_partidas_hdr");

            entity.Property(e => e.correlativo).HasMaxLength(15);
            entity.Property(e => e.hora_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.maestro).HasMaxLength(1);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.sinopsis).HasMaxLength(1000);
            entity.Property(e => e.tipo_transaccion)
                .HasMaxLength(3)
                .IsFixedLength();
            entity.Property(e => e.usuario_creacion).HasMaxLength(100);
        });

        modelBuilder.Entity<cnt_rubro>(entity =>
        {
            entity.HasNoKey();

            entity.HasIndex(e => e.nombre, "nombreunico").IsUnique();

            entity.Property(e => e.cod_reporte).ValueGeneratedOnAdd();
            entity.Property(e => e.nombre).HasMaxLength(50);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_saldo>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.cod_cuenta).HasMaxLength(25);
            entity.Property(e => e.hora_cierre).HasColumnType("timestamp without time zone");
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.ult_usuario).HasMaxLength(100);
        });

        modelBuilder.Entity<cnt_sub_cuentum>(entity =>
        {
            entity.HasKey(e => new { e.cod_grupo, e.csgrupo, e.cod_mayor, e.cscuenta }).HasName("cnt_sub_cuenta_pkey");

            entity.Property(e => e.cod_grupo).HasMaxLength(15);
            entity.Property(e => e.csgrupo).HasMaxLength(15);
            entity.Property(e => e.cod_mayor).HasMaxLength(15);
            entity.Property(e => e.cscuenta).HasMaxLength(15);
            entity.Property(e => e.descripcion).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_sub_grupo>(entity =>
        {
            entity.HasKey(e => new { e.cod_grupo, e.cod_sub_grupo }).HasName("cnt_sub_grupo_pkey");

            entity.ToTable("cnt_sub_grupo");

            entity.Property(e => e.cod_grupo).HasMaxLength(1);
            entity.Property(e => e.cod_sub_grupo).HasMaxLength(2);
            entity.Property(e => e.descripcion).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<cnt_tipopartidum>(entity =>
        {
            entity.HasKey(e => e.cod_tipopartida).HasName("cnt_tipopartida_pkey");

            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.observaciones).HasMaxLength(100);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<concepto_cobro_adicional>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("concepto_cobro_adicional_pkey");

            entity.ToTable("concepto_cobro_adicional");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.concepto).HasColumnType("character varying");
        });



        modelBuilder.Entity<configuracion_cobros_adicionale>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("configuracion_cobros_adicionales_pkey");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
        });

        modelBuilder.Entity<configuracion_cobros_adicionales_detalle>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("configuracion_cobros_adicionales_detalle_pkey");

            entity.ToTable("configuracion_cobros_adicionales_detalle");

            entity.HasIndex(e => e.configuracion_cobro_adicional_ide, "fki_configuracion_cobros_fkey");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.porcentaje).HasPrecision(12, 2);

            entity.HasOne(d => d.configuracion_cobro_adicional_ideNavigation).WithMany(p => p.configuracion_cobros_adicionales_detalles)
                .HasForeignKey(d => d.configuracion_cobro_adicional_ide)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("configuracion_cobros_fkey");
        });



        modelBuilder.Entity<coordenadas_empleado>(entity =>
        {
            entity.HasKey(e => e.id).HasName("coordenadas_empleado_pkey");

            entity.ToTable("coordenadas_empleado");

            entity.Property(e => e.id).UseIdentityAlwaysColumn();
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.latitud).HasMaxLength(25);
            entity.Property(e => e.longitud).HasMaxLength(25);
            entity.Property(e => e.nombre).HasMaxLength(50);
        });

        modelBuilder.Entity<factura>(entity =>
        {
            entity.HasKey(e => e.id).HasName("factura_pkey");

            entity.ToTable("factura");

            entity.Property(e => e.id).UseIdentityAlwaysColumn();
            entity.Property(e => e.ano).HasColumnType("character varying");
            entity.Property(e => e.clientecodigo).HasColumnType("character varying");
            entity.Property(e => e.estado).HasColumnType("character varying");
            entity.Property(e => e.identidad).HasColumnType("character varying");
            entity.Property(e => e.mes).HasColumnType("character varying");
            entity.Property(e => e.numdei).HasColumnType("character varying");
            entity.Property(e => e.numfactura).HasColumnType("character varying");
            entity.Property(e => e.numrecibo)
                .ValueGeneratedOnAdd()
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(3075052L, null, null, null, null, null);
            entity.Property(e => e.periodo).HasColumnType("character varying");
            entity.Property(e => e.recolectora).HasColumnType("character varying");
            entity.Property(e => e.referencia).HasColumnType("character varying");
            entity.Property(e => e.rtn).HasColumnType("character varying");
            entity.Property(e => e.saldototal).HasPrecision(18, 2);
            entity.Property(e => e.tipofactura).HasColumnType("character varying");
            entity.Property(e => e.tipofacturacion).HasColumnType("character varying");
            entity.Property(e => e.usuario).HasColumnType("character varying");
        });

        modelBuilder.Entity<factura_detalle>(entity =>
        {
            entity.HasKey(e => e.id).HasName("factura_detalle_pkey");

            entity.ToTable("factura_detalle");

            entity.Property(e => e.id).UseIdentityAlwaysColumn();
            entity.Property(e => e.codigo).HasColumnType("character varying");
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.montovalor).HasPrecision(18, 2);
            entity.Property(e => e.montovalor_saldo).HasPrecision(18, 2);
            entity.Property(e => e.tiposervicio).HasColumnType("character varying");
        });

        modelBuilder.Entity<grupo_estado_detalle>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("grupo_estado_detalle_pkey");

            entity.ToTable("grupo_estado_detalle");
        });

        modelBuilder.Entity<grupoestado>(entity =>
        {
            entity.HasKey(e => e.idgrupo).HasName("grupoestado_pkey");

            entity.ToTable("grupoestado");
        });

        modelBuilder.Entity<grupoestadodetalle>(entity =>
        {
            entity.HasKey(e => e.idgrupodetalle).HasName("grupoestadodetalle_pkey");

            entity.ToTable("grupoestadodetalle");

            entity.HasOne(d => d.idgrupoNavigation).WithMany(p => p.grupoestadodetalles)
                .HasForeignKey(d => d.idgrupo)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_idgrupo");
        });

        modelBuilder.Entity<historialme>(entity =>
        {
            entity.HasKey(e => new { e.ano, e.mes, e.ciclo }).HasName("historialmes_pkey");

            entity.Property(e => e.ano).HasPrecision(4);
            entity.Property(e => e.mes).HasPrecision(2);
            entity.Property(e => e.ciclo)
                .HasMaxLength(10)
                .IsFixedLength();
            entity.Property(e => e._2_Sep)
                .HasMaxLength(32767)
                .HasColumnName("2-Sep");
            entity.Property(e => e.cerrado)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.cerrarperiodo)
                .HasMaxLength(1)
                .HasDefaultValueSql("'P'::bpchar");
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechaperiodo).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ruta)
                .HasMaxLength(10)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.sep)
                .HasPrecision(1)
                .HasDefaultValueSql("NULL::numeric");
            entity.Property(e => e.sep2)
                .HasPrecision(1)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.usuarioapertura)
                .HasMaxLength(150)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.usuariocierre)
                .HasMaxLength(20)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
        });

        modelBuilder.Entity<historicomedicion>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("historicomedicion_pkey");

            entity.ToTable("historicomedicion");

            entity.Property(e => e.ide)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(1119616L, null, null, null, null, null);
            entity.Property(e => e.ajuste)
                .HasMaxLength(2)
                .HasDefaultValueSql("''::bpchar")
                .IsFixedLength();
            entity.Property(e => e.ano).HasPrecision(4);
            entity.Property(e => e.categoria)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'::bpchar");
            entity.Property(e => e.categoriacliente)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.cerrado)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'::bpchar");
            entity.Property(e => e.ciclo)
                .HasMaxLength(2)
                .HasDefaultValueSql("'0'::bpchar")
                .IsFixedLength();
            entity.Property(e => e.clave)
                .HasMaxLength(15)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.codinfo)
                .HasMaxLength(2)
                .HasDefaultValueSql("''::bpchar")
                .IsFixedLength();
            entity.Property(e => e.comentario)
                .HasMaxLength(145)
                .HasDefaultValueSql("NULL::character varying");
            entity.Property(e => e.condicion)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.consumo)
                .HasPrecision(12)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.consumoant)
                .HasPrecision(12)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.contador)
                .HasMaxLength(50)
                .HasDefaultValueSql("NULL::character varying");
            entity.Property(e => e.descuentoapp)
                .HasPrecision(14, 2)
                .HasDefaultValueSql("NULL::numeric");
            entity.Property(e => e.lec_prom)
                .HasPrecision(12)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.lect_act)
                .HasPrecision(12)
                .HasDefaultValueSql("NULL::numeric");
            entity.Property(e => e.lect_ant)
                .HasPrecision(12)
                .HasDefaultValueSql("NULL::numeric");
            entity.Property(e => e.mes).HasPrecision(2);
            entity.Property(e => e.mes2)
                .HasMaxLength(2)
                .HasDefaultValueSql("''::bpchar")
                .IsFixedLength();
            entity.Property(e => e.numerofactura)
                .HasMaxLength(50)
                .HasDefaultValueSql("NULL::character varying");
            entity.Property(e => e.numeroord)
                .HasPrecision(12)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.observacion)
                .HasMaxLength(300)
                .HasDefaultValueSql("NULL::character varying");
            entity.Property(e => e.orden)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'::bpchar");
            entity.Property(e => e.otros)
                .HasMaxLength(20)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.propietario)
                .HasMaxLength(100)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.revision)
                .HasPrecision(1)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.revision2)
                .HasMaxLength(1)
                .HasDefaultValueSql("'0'::bpchar");
            entity.Property(e => e.ruta)
                .HasMaxLength(25)
                .HasDefaultValueSql("''::bpchar")
                .IsFixedLength();
            entity.Property(e => e.secuencia)
                .HasMaxLength(9)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.sel)
                .HasPrecision(1)
                .HasDefaultValueSql("'0'::numeric");
            entity.Property(e => e.ser1)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser10)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser2)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser3)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser4)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser5)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser6)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser7)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser8)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.ser9)
                .HasMaxLength(1)
                .HasDefaultValueSql("NULL::bpchar");
            entity.Property(e => e.taservi1)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi10)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi2)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi3)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi4)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi5)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi6)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi7)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi8)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.taservi9)
                .HasPrecision(12, 2)
                .HasDefaultValueSql("0.00");
            entity.Property(e => e.tipolectura)
                .HasMaxLength(1)
                .HasDefaultValueSql("''::bpchar");
            entity.Property(e => e.tservi1)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi10)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi2)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi3)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi4)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi5)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi6)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi7)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi8)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.tservi9)
                .HasMaxLength(3)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.ubicacion)
                .HasMaxLength(100)
                .HasDefaultValueSql("NULL::bpchar")
                .IsFixedLength();
            entity.Property(e => e.usuario)
                .HasMaxLength(50)
                .HasDefaultValueSql("NULL::character varying");
        });

        modelBuilder.Entity<historicosinmedidor>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("historicosinmedidor_pkey");

            entity.ToTable("historicosinmedidor");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.cuenta).HasMaxLength(15);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.numerofactura).HasMaxLength(50);
            entity.Property(e => e.usuario).HasMaxLength(50);
        });

        modelBuilder.Entity<identityrole>(entity =>
        {
            entity.HasKey(e => e.id).HasName("pk_roles");

            entity.ToTable("identityrole");

            entity.HasIndex(e => e.normalized_name, "RoleNameIndex").IsUnique();

            entity.Property(e => e.name).HasMaxLength(256);
            entity.Property(e => e.normalized_name).HasMaxLength(256);
        });

        modelBuilder.Entity<identityroleclaim_string_>(entity =>
        {
            entity.HasKey(e => e.id).HasName("pk_role_claims");

            entity.ToTable("identityroleclaim<string>");

            entity.HasIndex(e => e.role_id, "ix_role_claims_role_id");

            entity.HasOne(d => d.role).WithMany(p => p.identityroleclaim_string_s)
                .HasForeignKey(d => d.role_id)
                .HasConstraintName("fk_role_claims_asp_net_roles_identity_role_id");
        });

        modelBuilder.Entity<identityuser>(entity =>
        {
            entity.HasKey(e => e.id).HasName("pk_users");

            entity.ToTable("identityuser");

            entity.HasIndex(e => e.normalized_email, "EmailIndex");

            entity.HasIndex(e => e.normalized_user_name, "UserNameIndex").IsUnique();

            entity.Property(e => e.email).HasMaxLength(256);
            entity.Property(e => e.normalized_email).HasMaxLength(256);
            entity.Property(e => e.normalized_user_name).HasMaxLength(256);
            entity.Property(e => e.user_name).HasMaxLength(256);

            entity.HasMany(d => d.roles).WithMany(p => p.users)
                .UsingEntity<Dictionary<string, object>>(
                    "identityuserrole_string_",
                    r => r.HasOne<identityrole>().WithMany()
                        .HasForeignKey("role_id")
                        .HasConstraintName("fk_user_roles_asp_net_roles_identity_role_id"),
                    l => l.HasOne<identityuser>().WithMany()
                        .HasForeignKey("user_id")
                        .HasConstraintName("fk_user_roles_asp_net_users_identity_user_id"),
                    j =>
                    {
                        j.HasKey("user_id", "role_id").HasName("pk_user_roles");
                        j.ToTable("identityuserrole<string>");
                        j.HasIndex(new[] { "role_id" }, "ix_user_roles_role_id");
                    });
        });

        modelBuilder.Entity<identityuserclaim_string_>(entity =>
        {
            entity.HasKey(e => e.id).HasName("pk_user_claims");

            entity.ToTable("identityuserclaim<string>");

            entity.HasIndex(e => e.user_id, "ix_user_claims_user_id");

            entity.HasOne(d => d.user).WithMany(p => p.identityuserclaim_string_s)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_user_claims_asp_net_users_identity_user_id");
        });

        modelBuilder.Entity<identityuserlogin_string_>(entity =>
        {
            entity.HasKey(e => new { e.login_provider, e.provider_key }).HasName("pk_user_logins");

            entity.ToTable("identityuserlogin<string>");

            entity.HasIndex(e => e.user_id, "ix_user_logins_user_id");

            entity.HasOne(d => d.user).WithMany(p => p.identityuserlogin_string_s)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_user_logins_asp_net_users_identity_user_id");
        });

        modelBuilder.Entity<identityusertoken_string_>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.login_provider, e.name }).HasName("pk_user_tokens");

            entity.ToTable("identityusertoken<string>");

            entity.HasOne(d => d.user).WithMany(p => p.identityusertoken_string_s)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_user_tokens_asp_net_users_identity_user_id");
        });



        modelBuilder.Entity<log_cliclo_descarga_app>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("log_cliclo_descarga_app");

            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.ide)
                .ValueGeneratedOnAdd()
                .UseIdentityAlwaysColumn();
            entity.Property(e => e.usuario).HasColumnType("character varying");
        });

        modelBuilder.Entity<maestro_medidor>(entity =>
        {
            entity.HasKey(e => e.maestro_medidor_id).HasName("maestro_medidor_id_pkey");

            entity.ToTable("maestro_medidor");

            entity.Property(e => e.maestro_medidor_id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(23718L, null, null, null, null, null);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.maestro_medidor_acueducto).HasMaxLength(20);
            entity.Property(e => e.maestro_medidor_diametro).HasPrecision(4, 2);
            entity.Property(e => e.maestro_medidor_empleado).HasMaxLength(50);
            entity.Property(e => e.maestro_medidor_fecha_instala).HasColumnType("timestamp without time zone");
            entity.Property(e => e.maestro_medidor_marca).HasMaxLength(50);
            entity.Property(e => e.maestro_medidor_numero).HasMaxLength(50);
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<materialesapp>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("materialesapp");

            entity.Property(e => e.codproduc).HasMaxLength(20);
        });

        modelBuilder.Entity<miscelaneos_catalogo>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("miscelaneos_catalogo_pkey");

            entity.ToTable("miscelaneos_catalogo");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.codigo).HasColumnType("character varying");
            entity.Property(e => e.nombre).HasColumnType("character varying");
            entity.Property(e => e.valor).HasPrecision(18, 2);

            entity.HasOne(e => e.cont_account)
                .WithMany()
                .HasForeignKey(e => e.cont_account_id)
                .HasConstraintName("fk_miscelaneos_catalogo_cont_account")
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.cont_account_id)
                .HasDatabaseName("ix_miscelaneos_catalogo_cont_account_id");
        });

        modelBuilder.Entity<ops_compromiso>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.aplicado).HasMaxLength(1);
            entity.Property(e => e.beneficiario).HasMaxLength(150);
            entity.Property(e => e.bor).HasMaxLength(1);
            entity.Property(e => e.cod_actvidad).HasMaxLength(4);
            entity.Property(e => e.cod_gastos).HasMaxLength(4);
            entity.Property(e => e.cod_programa).HasMaxLength(4);
            entity.Property(e => e.cod_proveedor).HasMaxLength(4);
            entity.Property(e => e.codigo).HasMaxLength(20);
            entity.Property(e => e.codproy).HasMaxLength(20);
            entity.Property(e => e.compromiso).HasPrecision(18, 2);
            entity.Property(e => e.concepto).HasMaxLength(250);
            entity.Property(e => e.cta_contable).HasMaxLength(25);
            entity.Property(e => e.ctacobrar).HasMaxLength(25);
            entity.Property(e => e.docu).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechap).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechavence).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fondo).HasMaxLength(1);
            entity.Property(e => e.numero).HasPrecision(18, 2);
            entity.Property(e => e.orden).HasMaxLength(20);
            entity.Property(e => e.paga).HasPrecision(18, 2);
            entity.Property(e => e.pagos).HasPrecision(18, 2);
            entity.Property(e => e.rtn).HasMaxLength(30);
        });

        modelBuilder.Entity<orden_trabajo>(entity =>
        {
            entity.HasKey(e => e.orden_id).HasName("orden_trabajo_pkey");

            entity.ToTable("orden_trabajo");

            entity.Property(e => e.orden_id)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(227878L, null, null, null, null, null);
            entity.Property(e => e.concepto).HasColumnType("character varying");
            entity.Property(e => e.empleado).HasColumnType("character varying");
            entity.Property(e => e.estado).HasMaxLength(1);
            entity.Property(e => e.fecha_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.informe).HasColumnType("character varying");
            entity.Property(e => e.maestro_cliente_clave).HasMaxLength(20);
            entity.Property(e => e.personas).HasColumnType("character varying");
            entity.Property(e => e.saldo).HasPrecision(18, 2);
            entity.Property(e => e.tipo).HasColumnType("character varying");
            entity.Property(e => e.usuario).HasColumnType("character varying");
        });

        modelBuilder.Entity<orden_trabajo_adjunto>(entity =>
        {
            entity.HasKey(e => e.id).HasName("orden_trabajo_adjunto_pkey");

            entity.ToTable("orden_trabajo_adjunto");

            entity.Property(e => e.fechafin).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechainicio).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechaobtenerordenes).HasColumnType("timestamp without time zone");
            entity.Property(e => e.latitud).HasMaxLength(100);
            entity.Property(e => e.longitud).HasMaxLength(100);
            entity.Property(e => e.nombre).HasMaxLength(100);
            entity.Property(e => e.numeroorden).HasMaxLength(100);
            entity.Property(e => e.tipo).HasMaxLength(20);
        });

        modelBuilder.Entity<orden_trabajo_estado>(entity =>
        {
            entity.HasKey(e => e.codigo).HasName("orden_trabajo_estado_pkey");

            entity.ToTable("orden_trabajo_estado");

            entity.Property(e => e.codigo).HasMaxLength(3);
            entity.Property(e => e.nombre).HasMaxLength(120);
        });

        modelBuilder.Entity<ordent_mate>(entity =>
        {
            entity.HasKey(e => e.id).HasName("ordent_mate_pkey");

            entity.ToTable("ordent_mate");

            entity.Property(e => e.codproduc).HasMaxLength(20);
            entity.Property(e => e.cuenta).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(200);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
        });

        modelBuilder.Entity<pagos_banco>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.agencia).HasColumnType("character varying");
            entity.Property(e => e.banco).HasColumnType("character varying");
            entity.Property(e => e.cajero).HasColumnType("character varying");
            entity.Property(e => e.cliente_clave).HasColumnType("character varying");
            entity.Property(e => e.idreg).HasMaxLength(1);
            entity.Property(e => e.montop).HasPrecision(12, 2);
            entity.Property(e => e.recibo).HasPrecision(12);
            entity.Property(e => e.referencia).HasColumnType("character varying");
            entity.Property(e => e.rtn).HasColumnType("character varying");
            entity.Property(e => e.sucursal).HasColumnType("character varying");
            entity.Property(e => e.terminal).HasColumnType("character varying");
        });

        modelBuilder.Entity<pagovariostemp>(entity =>
        {
            entity.HasKey(e => e.id).HasName("pagovariostemp_pkey");

            entity.ToTable("pagovariostemp");

            entity.Property(e => e.id).UseIdentityAlwaysColumn();
            entity.Property(e => e.cajero).HasColumnType("character varying");
            entity.Property(e => e.cliente_clave).HasColumnType("character varying");
            entity.Property(e => e.cod_banco).HasColumnType("character varying");
            entity.Property(e => e.codigo).HasColumnType("character varying");
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.estado).HasColumnType("character varying");
            entity.Property(e => e.expe).HasColumnType("character varying");
            entity.Property(e => e.identidad).HasColumnType("character varying");
            entity.Property(e => e.nombre).HasColumnType("character varying");
            entity.Property(e => e.tipo_factura).HasColumnType("character varying");
            entity.Property(e => e.tipo_servicio).HasColumnType("character varying");
            entity.Property(e => e.usuario).HasColumnType("character varying");
            entity.Property(e => e.valor_)
                .HasPrecision(18, 2)
                .HasColumnName("valor ");
        });

        modelBuilder.Entity<presupuesto>(entity =>
        {
            entity.HasKey(e => e.id_presupuesto).HasName("presupuestos_pkey");

            entity.Property(e => e.id_presupuesto).UseIdentityAlwaysColumn();
            entity.Property(e => e.centro_costo).HasMaxLength(10);
            entity.Property(e => e.fondo).HasMaxLength(50);
            entity.Property(e => e.usuario_creo).HasMaxLength(50);
            entity.Property(e => e.usuario_modifico).HasMaxLength(50);
        });

        modelBuilder.Entity<presupuesto_fondo>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("presupuesto_fondos_pkey");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.fondo_descripcion).HasColumnType("character varying");
        });

        modelBuilder.Entity<proyecto>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("proyectos_pkey");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.ampliado).HasPrecision(18, 2);
            entity.Property(e => e.aprobado).HasPrecision(18, 2);
            entity.Property(e => e.codigo).HasColumnType("character varying");
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.ejectutor).HasColumnType("character varying");
            entity.Property(e => e.empre).HasColumnType("character varying");
            entity.Property(e => e.fecha1).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha2).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fondo).HasColumnType("character varying");
            entity.Property(e => e.fuente_financiamiento).HasColumnType("character varying");
            entity.Property(e => e.lugar).HasColumnType("character varying");
            entity.Property(e => e.pagado).HasPrecision(18, 2);
            entity.Property(e => e.presupuesto).HasColumnType("character varying");
            entity.Property(e => e.supervisor).HasColumnType("character varying");
            entity.Property(e => e.ubicacion).HasColumnType("character varying");
        });

        modelBuilder.Entity<pruebainsert>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("pruebainsert");

            entity.Property(e => e.ejemplo).HasMaxLength(500);
            entity.Property(e => e.id).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<prv_compromiso_dtl>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("prv_compromiso_dtl");

            entity.Property(e => e.actividad).HasMaxLength(2);
            entity.Property(e => e.cod_presupuestario).HasMaxLength(20);
            entity.Property(e => e.conceptodtl).HasMaxLength(100);
            entity.Property(e => e.cuenta_gasto).HasMaxLength(20);
            entity.Property(e => e.descripcion).HasMaxLength(150);
            entity.Property(e => e.monto).HasPrecision(18, 2);
            entity.Property(e => e.objeto_gasto).HasMaxLength(100);
            entity.Property(e => e.programa).HasMaxLength(2);
        });

        modelBuilder.Entity<prv_compromiso_hdr>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("prv_compromiso_hdr");

            entity.Property(e => e.cod_proveedor).HasMaxLength(7);
            entity.Property(e => e.cod_proyecto).HasMaxLength(20);
            entity.Property(e => e.concepto).HasMaxLength(150);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.monto).HasPrecision(18, 2);
            entity.Property(e => e.pagar_a).HasMaxLength(100);
            entity.Property(e => e.rtn).HasMaxLength(20);
        });

        modelBuilder.Entity<prv_kardex>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("prv_kardex");

            entity.Property(e => e.cai).HasMaxLength(8);
            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.correlativo).HasMaxLength(20);
            entity.Property(e => e.correlativo_dei).HasMaxLength(8);
            entity.Property(e => e.cuenta_acreditar).HasMaxLength(13);
            entity.Property(e => e.cuenta_debitar).HasMaxLength(13);
            entity.Property(e => e.fec_vencimiento).HasColumnType("timestamp without time zone");
            entity.Property(e => e.nombre_proveedor_p).HasMaxLength(50);
            entity.Property(e => e.num_cheque).HasMaxLength(12);
            entity.Property(e => e.observaciones).HasMaxLength(254);
            entity.Property(e => e.pda).HasMaxLength(1);
            entity.Property(e => e.referencia1).HasMaxLength(25);
            entity.Property(e => e.referencia2).HasMaxLength(25);
            entity.Property(e => e.referencia_afecta).HasMaxLength(30);
            entity.Property(e => e.rowid).HasMaxLength(100);
            entity.Property(e => e.status_pago).HasColumnType("bit(1)");
            entity.Property(e => e.tipo_transaccion).HasMaxLength(3);
            entity.Property(e => e.tipo_transaccion2).HasMaxLength(3);
            entity.Property(e => e.ultima_trn).HasMaxLength(1);
            entity.Property(e => e.usuario_creo).HasMaxLength(50);
        });

        modelBuilder.Entity<prv_proveedore>(entity =>
        {
            entity.HasNoKey();

            entity.Property(e => e.cod_proveedor).HasMaxLength(20);
            entity.Property(e => e.cuenta_bancaria).HasMaxLength(50);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.direccion).HasMaxLength(100);
            entity.Property(e => e.email).HasMaxLength(150);
            entity.Property(e => e.fax).HasMaxLength(50);
            entity.Property(e => e.fecha_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.nombrebanco1).HasMaxLength(80);
            entity.Property(e => e.nombrebanco2).HasMaxLength(80);
            entity.Property(e => e.nombre_contacto).HasMaxLength(150);
            entity.Property(e => e.pagina_web).HasMaxLength(150);
            entity.Property(e => e.razon_social).HasMaxLength(150);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.rtn).HasMaxLength(20);
            entity.Property(e => e.telefono).HasMaxLength(20);
        });

        modelBuilder.Entity<prv_tipoproveedor>(entity =>
        {
            entity.HasKey(e => e.cod_tipoproveedor);

            entity.ToTable("prv_tipoproveedor");

            entity.Property(e => e.cod_tipoproveedor).ValueGeneratedOnAdd();
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.observaciones).HasMaxLength(500);
            entity.Property(e => e.rowid).HasDefaultValueSql("gen_random_uuid()");
        });

        modelBuilder.Entity<prv_tipostransacc>(entity =>
        {
            entity.HasKey(e => e.tipo_transaccion).HasName("PRV_TIPOSTRANSACC_pkey");

            entity.ToTable("prv_tipostransacc");

            entity.Property(e => e.tipo_transaccion).HasMaxLength(3);
            entity.Property(e => e.cod_tipopartida).HasMaxLength(10);
            entity.Property(e => e.correlativo).HasMaxLength(6);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.del_sistema).HasMaxLength(1);
            entity.Property(e => e.entra_sale).HasMaxLength(1);
            entity.Property(e => e.fecha_creacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fecha_modificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.nombre).HasMaxLength(40);
            entity.Property(e => e.pda).HasMaxLength(1);
            entity.Property(e => e.rowid).HasMaxLength(100);
            entity.Property(e => e.usuario_creo).HasMaxLength(8);
            entity.Property(e => e.usuario_modifica).HasMaxLength(8);
        });

        modelBuilder.Entity<recolectora>(entity =>
        {
            entity.HasKey(e => e.codigo).HasName("recolectora_pkey");

            entity.ToTable("recolectora");

            entity.Property(e => e.codigo).HasMaxLength(3);
            entity.Property(e => e.aplica).HasPrecision(11, 4);
            entity.Property(e => e.contable)
                .HasMaxLength(20)
                .IsFixedLength();
            entity.Property(e => e.ctabanco)
                .HasMaxLength(3)
                .IsFixedLength();
            entity.Property(e => e.descripcion)
                .HasMaxLength(40)
                .IsFixedLength();
            entity.Property(e => e.idbancows).HasMaxLength(3);
            entity.Property(e => e.llave).HasMaxLength(45);
        });

        modelBuilder.Entity<ruta>(entity =>
        {
            entity.HasKey(e => e.id);

            entity.Property(e => e.codruta).HasColumnType("character varying");
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.estado);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.id).ValueGeneratedOnAdd();
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);

            entity.HasOne(d => d.codcicloNavigation).WithMany()
                .HasForeignKey(d => d.codciclo)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("rutas_fk");
        });

        modelBuilder.Entity<servicio>(entity =>
        {
            entity.HasKey(e => e.servicios_id).HasName("servicios_id_pkey");

            entity.HasIndex(e => new { e.company_id, e.servicios_codigo })
                .IsUnique()
                .HasDatabaseName("ix_servicios_company_codigo");

            entity.HasIndex(e => e.company_id)
                .HasDatabaseName("ix_servicios_company");

            entity.HasIndex(e => e.cont_account_id)
                .HasDatabaseName("ix_servicios_cont_account");

            entity.Property(e => e.servicios_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.app_grupo).HasMaxLength(20);
            entity.Property(e => e.company_id).HasColumnName("company_id");
            entity.Property(e => e.cont_account_id).HasColumnName("cont_account_id");
            entity.Property(e => e.es_servicio_base).HasColumnName("es_servicio_base");
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.facturable_app).HasDefaultValue(false);
            entity.Property(e => e.servicios_codigo).HasMaxLength(50);
            entity.Property(e => e.servicios_descripcioncorta).HasMaxLength(100);
            entity.Property(e => e.servicios_descripcionlarga).HasMaxLength(300);
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);

            entity.HasOne<cfg_company>()
                .WithMany()
                .HasForeignKey(e => e.company_id)
                .HasConstraintName("servicios_company_id_fkey");

            entity.HasOne(e => e.cont_account)
                .WithMany()
                .HasForeignKey(e => e.cont_account_id)
                .HasConstraintName("servicios_cont_account_id_fkey");
        });


        modelBuilder.Entity<solicitud_servicio>(entity =>
        {
            entity.HasKey(e => e.solicitud_servicio_id).HasName("solicitud_servicio_pkey");

            entity.ToTable("solicitud_servicio");

            entity.Property(e => e.solicitud_servicio_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.clave_sure).HasMaxLength(50);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechanacimiento).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);

            entity.HasOne(d => d.categoria_servicio).WithMany(p => p.solicitud_servicios)
                .HasForeignKey(d => d.categoria_servicio_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("solicitud_servicio_categoria_servicio_id_fkey");
        });



        modelBuilder.Entity<tipo_d>(entity =>
        {
            entity.HasKey(e => e.tipo_id).HasName("tipo_d_pkey");

            entity.ToTable("tipo_d");

            entity.Property(e => e.tipo_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.concepto).HasColumnType("character varying");
            entity.Property(e => e.depto).HasMaxLength(2);
            entity.Property(e => e.depto_appmitrabajo).HasMaxLength(2);
            entity.Property(e => e.descripcion).HasMaxLength(80);
            entity.Property(e => e.estado);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.tipo).HasMaxLength(2);
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<tipo_transaccion>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("tipo_transaccion_pkey");

            entity.ToTable("tipo_transaccion");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.codigo).HasColumnType("character varying");
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.fecha_actualizacion).HasColumnType("character varying");
            entity.Property(e => e.usuario_actualizacion).HasColumnType("character varying");
        });

        modelBuilder.Entity<tipo_uso_servicio>(entity =>
        {
            entity.HasKey(e => e.tipo_uso_codigo).HasName("tipo_uso_codigo_pkey");

            entity.ToTable("tipo_uso_servicio");

            entity.Property(e => e.tipo_uso_codigo).HasMaxLength(2);
            entity.Property(e => e.fechacreacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.fechamodificacion).HasColumnType("timestamp without time zone");
            entity.Property(e => e.usuariocreacion).HasMaxLength(256);
            entity.Property(e => e.usuariomodificacion).HasMaxLength(256);
        });

        modelBuilder.Entity<transaccion_abonado>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("transaccion_abonado_pkey");

            entity.ToTable("transaccion_abonado");

            entity.Property(e => e.ide)
                .UseIdentityAlwaysColumn()
                .HasIdentityOptions(3075446L, null, null, null, null, null);
            entity.Property(e => e.aplicar_alca).HasColumnType("character varying");
            entity.Property(e => e.banco).HasColumnType("character varying");
            entity.Property(e => e.ciclo).HasColumnType("character varying");
            entity.Property(e => e.cliente_clave).HasColumnType("character varying");
            entity.Property(e => e.codigoplan).HasColumnType("character varying");
            entity.Property(e => e.creditos).HasPrecision(12, 2);
            entity.Property(e => e.debitos).HasPrecision(12, 2);
            entity.Property(e => e.descripcion).HasColumnType("character varying");
            entity.Property(e => e.docufuente2).HasColumnType("character varying");
            entity.Property(e => e.estado).HasColumnType("character varying");
            entity.Property(e => e.motivo).HasColumnType("character varying");
            entity.Property(e => e.periodo).HasColumnType("character varying");
            entity.Property(e => e.ruta).HasColumnType("character varying");
            entity.Property(e => e.saldo).HasPrecision(12, 2);
            entity.Property(e => e.saldo_detalle).HasPrecision(18, 2);
            entity.Property(e => e.secuencia).HasColumnType("character varying");
            entity.Property(e => e.tasa).HasColumnType("character varying");
            entity.Property(e => e.tiene_med).HasColumnType("character varying");
            entity.Property(e => e.tipo_partida).HasColumnType("character varying");
            entity.Property(e => e.tipo_servicio).HasColumnType("character varying");
            entity.Property(e => e.tipotransaccion).HasColumnType("character varying");
            entity.Property(e => e.trans_aplicar).HasColumnType("character varying");
            entity.Property(e => e.usuario).HasColumnType("character varying");
        });

        modelBuilder.Entity<transaccion_presupuesto>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("transaccion_presupuesto_pkey");

            entity.ToTable("transaccion_presupuesto");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.trpr_codigoproyecto).HasColumnType("character varying");
            entity.Property(e => e.trpr_descripcion).HasColumnType("character varying");
            entity.Property(e => e.trpr_destino).HasColumnType("character varying");
            entity.Property(e => e.trpr_fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.trpr_monto).HasPrecision(18, 2);
            entity.Property(e => e.trpr_presupuesto_origen).HasColumnType("character varying");
            entity.Property(e => e.trpr_tipo_transaccion).HasColumnType("character varying");
            entity.Property(e => e.trpr_tipodestino).HasColumnType("character varying");
        });

        modelBuilder.Entity<usuarioapc>(entity =>
        {
            entity.HasKey(e => e.ide).HasName("usuarioapc_pkey");

            entity.ToTable("usuarioapc");

            entity.Property(e => e.ide).UseIdentityAlwaysColumn();
            entity.Property(e => e.clave).HasMaxLength(30);
            entity.Property(e => e.estado).HasMaxLength(1);
            entity.Property(e => e.nombre).HasMaxLength(50);
            entity.Property(e => e.ruta).HasMaxLength(6);
            entity.Property(e => e.usuario).HasMaxLength(25);
        });

        modelBuilder.Entity<usuarios_miorden>(entity =>
        {
            entity.HasKey(e => e.id).HasName("usuarios_miorden_pkey");

            entity.ToTable("usuarios_miorden");

            entity.Property(e => e.id).UseIdentityAlwaysColumn();
            entity.Property(e => e.clave).HasColumnType("character varying");
            entity.Property(e => e.estado).HasColumnType("bit(1)");
            entity.Property(e => e.nombre).HasColumnType("character varying");
            entity.Property(e => e.usuario).HasColumnType("character varying");
        });

        modelBuilder.Entity<usuarios_tipotransaccion_dtl>(entity =>
        {
            entity.HasKey(e => e.id_usertransacc_dtl).HasName("usuarios_tipotransaccion_dtl_pkey");

            entity.ToTable("usuarios_tipotransaccion_dtl");

            entity.Property(e => e.id_usertransacc_dtl).UseIdentityAlwaysColumn();
            entity.Property(e => e.cod_tipotransaccion).HasMaxLength(3);
        });

        modelBuilder.Entity<usuarios_tipotransaccion_hdr>(entity =>
        {
            entity.HasKey(e => e.id_usertransacc).HasName("usuarios_tipotransaccion_hdr_pkey");

            entity.ToTable("usuarios_tipotransaccion_hdr");

            entity.Property(e => e.id_usertransacc).UseIdentityAlwaysColumn();
            entity.Property(e => e.usuario).HasMaxLength(60);
            entity.Property(e => e.usuario_creo).HasMaxLength(60);
        });

        modelBuilder.Entity<view_bus_codigo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("view_bus_codigo");

            entity.Property(e => e.centro).HasMaxLength(9);
            entity.Property(e => e.codcuenta).HasMaxLength(20);
            entity.Property(e => e.nombres).HasMaxLength(150);
        });

        modelBuilder.Entity<view_centro_costo>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("view_centro_costo");

            entity.Property(e => e.actividad).HasMaxLength(150);
            entity.Property(e => e.codigo_costo).HasMaxLength(9);
            entity.Property(e => e.contable).HasMaxLength(20);
            entity.Property(e => e.objeto_gasto).HasMaxLength(150);
            entity.Property(e => e.programa).HasMaxLength(150);
        });

        modelBuilder.Entity<view_get_compromisos_dtl>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("view_get_compromisos_dtl");

            entity.Property(e => e.actividad).HasMaxLength(150);
            entity.Property(e => e.cod_presupuestario).HasMaxLength(20);
            entity.Property(e => e.contable).HasMaxLength(20);
            entity.Property(e => e.descripciondtl).HasMaxLength(150);
            entity.Property(e => e.montodtl).HasPrecision(18, 2);
            entity.Property(e => e.objeto_gasto).HasMaxLength(150);
            entity.Property(e => e.programa).HasMaxLength(150);
        });

        modelBuilder.Entity<view_get_compromisos_hdr>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("view_get_compromisos_hdr");

            entity.Property(e => e.cod_proveedor).HasColumnType("character varying");
            entity.Property(e => e.concepto).HasMaxLength(150);
            entity.Property(e => e.cuenta_contable).HasMaxLength(20);
            entity.Property(e => e.monto).HasPrecision(18, 2);
            entity.Property(e => e.proveedor).HasColumnType("character varying");
            entity.Property(e => e.rtn).HasMaxLength(20);
        });

        modelBuilder.Entity<vw_listaplanespago>(entity =>
        {
            entity
                .HasNoKey()
                .ToView("vw_listaplanespago");

            entity.Property(e => e.codcliente).HasMaxLength(20);
            entity.Property(e => e.correlativo).HasMaxLength(6);
            entity.Property(e => e.estado).HasMaxLength(20);
            entity.Property(e => e.fecha).HasColumnType("timestamp without time zone");
            entity.Property(e => e.total).HasPrecision(10, 2);
        });

        modelBuilder.Entity<portal_branding>(entity =>
        {
            entity.ToTable("portal_branding");
            entity.HasKey(e => e.branding_id);
        });

        modelBuilder.Entity<catalogo_caja>(entity =>
        {
            entity.ToTable("catalogo_cajas");
            entity.HasKey(e => e.id);

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.nombre).HasMaxLength(120);
            entity.Property(e => e.estado).HasMaxLength(2);
            entity.Property(e => e.usuario).HasMaxLength(60);
        });

        modelBuilder.Entity<pagos_hdr>(entity =>
        {
            entity.ToTable("pagos_hdr");
            entity.HasKey(e => e.numfactura);

            entity.Property(e => e.numfactura).HasMaxLength(30);
            entity.Property(e => e.clienteclave)
                .HasMaxLength(30)
                .HasColumnName("cliente_clave");
            entity.Property(e => e.estado).HasMaxLength(2);
            entity.Property(e => e.banco).HasMaxLength(80);
            entity.Property(e => e.usuario).HasMaxLength(60);

            entity.HasOne(d => d.caja)
                .WithMany(p => p.pagos_hdrs)
                .HasForeignKey(d => d.caja_id)
                .HasConstraintName("fk_pagos_hdr_caja");
        });

        modelBuilder.Entity<pagos_dtl>(entity =>
        {
            entity.ToTable("pagos_dtl");
            entity.HasKey(e => new { e.numfactura, e.linea });

            entity.Property(e => e.numfactura).HasMaxLength(30);
            entity.Property(e => e.servicio).HasMaxLength(160);
            entity.Property(e => e.monto).HasColumnType("numeric(18,2)");
            entity.Property(e => e.montovalor).HasColumnType("numeric(18,2)");

            entity.HasOne(d => d.pago)
                .WithMany(p => p.detalles)
                .HasForeignKey(d => d.numfactura)
                .HasConstraintName("fk_pagos_dtl_hdr")
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<pagos_miscelaneo>(entity =>
        {
            entity.ToTable("pagos_miscelaneos");
            entity.HasKey(e => e.recibo);

            entity.Property(e => e.recibo).ValueGeneratedNever();
            entity.Property(e => e.cliente).HasMaxLength(30);
            entity.Property(e => e.estado).HasMaxLength(2);
            entity.Property(e => e.total).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<ban_banco>(entity =>
        {
            entity.HasKey(e => e.ban_banco_id).HasName("ban_banco_pkey");

            entity.HasIndex(e => new { e.company_id, e.code }, "ban_banco_company_id_code_key").IsUnique();

            entity.HasIndex(e => e.company_id, "ix_ban_banco_company");

            entity.Property(e => e.ban_banco_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.ciudad_id).HasDefaultValue(0);
            entity.Property(e => e.code).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.direccion1).HasMaxLength(40);
            entity.Property(e => e.direccion2).HasMaxLength(40);
            entity.Property(e => e.email).HasMaxLength(25);
            entity.Property(e => e.estado_id).HasDefaultValue(0);
            entity.Property(e => e.fax).HasMaxLength(25);
            entity.Property(e => e.gerente).HasMaxLength(30);
            entity.Property(e => e.municipio_id).HasDefaultValue(0);
            entity.Property(e => e.nombre).HasMaxLength(60);
            entity.Property(e => e.nombre_sucursal).HasMaxLength(40);
            entity.Property(e => e.pais_id).HasDefaultValue(0);
            entity.Property(e => e.sucursal).HasMaxLength(50);
            entity.Property(e => e.telefonos).HasMaxLength(25);
            entity.Property(e => e.updated_by).HasMaxLength(100);
            entity.Property(e => e.zipcode).HasMaxLength(20);

            entity.HasOne(d => d.company).WithMany(p => p.ban_banco)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_banco_company_id_fkey");
        });

        modelBuilder.Entity<ban_config>(entity =>
        {
            entity.HasKey(e => e.ban_config_id).HasName("ban_config_pkey");

            entity.HasIndex(e => e.company_id, "ban_config_company_id_key").IsUnique();

            entity.HasIndex(e => e.company_id, "ix_ban_config_company");

            entity.Property(e => e.ban_config_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.a_ctas0).HasDefaultValue(0);
            entity.Property(e => e.a_ctas1).HasDefaultValue(0);
            entity.Property(e => e.a_ctas2).HasDefaultValue(0);
            entity.Property(e => e.a_ctas3).HasDefaultValue(0);
            entity.Property(e => e.a_ctas4).HasDefaultValue(0);
            entity.Property(e => e.a_ctas5).HasDefaultValue(0);
            entity.Property(e => e.alertar_nd).HasDefaultValue(0);
            entity.Property(e => e.cc_db).HasMaxLength(70);
            entity.Property(e => e.cc_descrip).HasMaxLength(40);
            entity.Property(e => e.cc_prefix).HasDefaultValue(0);
            entity.Property(e => e.cc_pwd).HasMaxLength(70);
            entity.Property(e => e.cc_server).HasMaxLength(70);
            entity.Property(e => e.cc_ssw).HasDefaultValue(0);
            entity.Property(e => e.cc_tipo).HasDefaultValue(0);
            entity.Property(e => e.cc_user).HasMaxLength(70);
            entity.Property(e => e.cod_sucu).HasMaxLength(5);
            entity.Property(e => e.consolidado).HasDefaultValue(false);
            entity.Property(e => e.cuenta_mayor).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.cta_aux1).HasMaxLength(30);
            entity.Property(e => e.cta_aux2).HasMaxLength(30);
            entity.Property(e => e.cta_aux3).HasMaxLength(30);
            entity.Property(e => e.dias_d1).HasDefaultValue(0);
            entity.Property(e => e.dias_d2).HasDefaultValue(0);
            entity.Property(e => e.dias_d3).HasDefaultValue(0);
            entity.Property(e => e.dir_contab).HasMaxLength(70);
            entity.Property(e => e.dir_dta_cont).HasMaxLength(70);
            entity.Property(e => e.i_c_egreso).HasDefaultValue(0);
            entity.Property(e => e.i_cheque_ne).HasDefaultValue(0);
            entity.Property(e => e.m_ope_conc).HasDefaultValue(0);
            entity.Property(e => e.max_cheque).HasPrecision(28, 4);
            entity.Property(e => e.meses_h).HasDefaultValue(0);
            entity.Property(e => e.n_ope1).HasDefaultValue(0);
            entity.Property(e => e.n_ope10).HasDefaultValue(0);
            entity.Property(e => e.n_ope2).HasDefaultValue(0);
            entity.Property(e => e.n_ope3).HasDefaultValue(0);
            entity.Property(e => e.n_ope4).HasDefaultValue(0);
            entity.Property(e => e.n_ope5).HasDefaultValue(0);
            entity.Property(e => e.n_ope6).HasDefaultValue(0);
            entity.Property(e => e.n_ope7).HasDefaultValue(0);
            entity.Property(e => e.n_ope8).HasDefaultValue(0);
            entity.Property(e => e.n_ope9).HasDefaultValue(0);
            entity.Property(e => e.nro_cxb).HasDefaultValue(0);
            entity.Property(e => e.p_deb_ban).HasPrecision(28, 4);
            entity.Property(e => e.prx_c_egreso).HasDefaultValue(0);
            entity.Property(e => e.prx_deposito).HasDefaultValue(0);
            entity.Property(e => e.prx_n_credito).HasDefaultValue(0);
            entity.Property(e => e.prx_n_debito).HasDefaultValue(0);
            entity.Property(e => e.st_dta).HasMaxLength(90);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.company).WithOne(p => p.ban_config)
                .HasForeignKey<ban_config>(d => d.company_id)
                .HasConstraintName("ban_config_company_id_fkey");
        });

        modelBuilder.Entity<ban_tipos_transacciones>(entity =>
        {
            entity.HasKey(e => e.ban_tipo_transaccion_id)
                .HasName("pk_ban_tipos_transacciones");

            entity.ToTable("ban_tipos_transacciones");

            entity.HasIndex(e => e.company_id, "ix_ban_tipos_transacciones_company");

            entity.HasIndex(
                    e => new
                    {
                        e.company_id,
                        e.tipo_transaccion,
                        e.cod_tipopartida,
                        e.correlativo,
                        e.entra_sale
                    },
                    "ux_ban_tipos_transacciones_company_tipo")
                .IsUnique();

            entity.HasIndex(e => e.cod_centrocosto, "ix_ban_tipos_transacciones_centrocosto");

            entity.HasIndex(e => e.cod_tipopartida, "ix_ban_tipos_transacciones_tipopartida");

            entity.Property(e => e.ban_tipo_transaccion_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.tipo_transaccion).HasMaxLength(3);
            entity.Property(e => e.cod_tipopartida)
                .HasMaxLength(3)
                .IsFixedLength();
            entity.Property(e => e.correlativo).HasMaxLength(6);
            entity.Property(e => e.cuenta_contable).HasMaxLength(13);
            entity.Property(e => e.destino).HasMaxLength(9);
            entity.Property(e => e.nombre).HasMaxLength(40);
            entity.Property(e => e.entra_sale)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.del_sistema)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.emite_cheque)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.pad)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.pda)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.rel_empleados)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.trn_prestamo)
                .HasMaxLength(1)
                .IsFixedLength();
            entity.Property(e => e.cuenta_alterna).HasDefaultValue(false);
            entity.Property(e => e.estado)
                .HasMaxLength(20)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.cost_center).WithMany()
                .HasForeignKey(d => d.cod_centrocosto)
                .HasConstraintName("fk_ban_tipos_transacciones_centrocosto");
        });

        modelBuilder.Entity<ban_cta>(entity =>
        {
            entity.HasKey(e => e.ban_cta_id).HasName("ban_cta_pkey");

            entity.HasIndex(e => new { e.company_id, e.codigo }, "ban_cta_company_id_codigo_key").IsUnique();

            entity.HasIndex(e => e.company_id, "ix_ban_cta_company");

            entity.Property(e => e.ban_cta_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.cod_centro).HasMaxLength(30);
            entity.Property(e => e.codigo).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.cta_base).HasDefaultValue(0);
            entity.Property(e => e.cta_cc).HasDefaultValue(0);
            entity.Property(e => e.cta_cf).HasDefaultValue(0);
            entity.Property(e => e.cta_mov).HasDefaultValue(0);
            entity.Property(e => e.cta_ter).HasDefaultValue(0);
            entity.Property(e => e.descripcion).HasMaxLength(60);
            entity.Property(e => e.ecg).HasDefaultValue(0);
            entity.Property(e => e.es_banco).HasDefaultValue(false);
            entity.Property(e => e.grupo).HasMaxLength(30);
            entity.Property(e => e.iea).HasDefaultValue(0);
            entity.Property(e => e.saldo_actual).HasPrecision(28, 4);
            entity.Property(e => e.tdc).HasDefaultValue(0);
            entity.Property(e => e.tercero).HasMaxLength(30);
            entity.Property(e => e.u_banco).HasMaxLength(30);
            entity.Property(e => e.u_benef).HasMaxLength(50);
            entity.Property(e => e.u_coment1).HasMaxLength(50);
            entity.Property(e => e.u_coment2).HasMaxLength(50);
            entity.Property(e => e.u_dcto).HasMaxLength(25);
            entity.Property(e => e.u_monto).HasPrecision(28, 4);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.company).WithMany(p => p.ban_cta)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_cta_company_id_fkey");
        });

        modelBuilder.Entity<ban_cuenta>(entity =>
        {
            entity.HasKey(e => e.banco_cuenta_id).HasName("ban_cuenta_pkey");

            entity.HasIndex(e => new { e.company_id, e.code }, "ban_cuenta_company_id_code_key").IsUnique();

            entity.HasIndex(e => new { e.company_id, e.numero_cuenta }, "ban_cuenta_company_id_numero_cuenta_key").IsUnique();

            entity.HasIndex(e => e.ban_banco_id, "ix_ban_cuenta_banco");

            entity.HasIndex(e => e.company_id, "ix_ban_cuenta_company");

            entity.HasIndex(e => e.ban_cta_id, "ix_ban_cuenta_cta");

            entity.Property(e => e.banco_cuenta_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.activo).HasDefaultValue(true);
            entity.Property(e => e.allow_reconciliation).HasDefaultValue(true);
            entity.Property(e => e.banco_nombre).HasMaxLength(150);
            entity.Property(e => e.code).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.cta_conc)
                .HasColumnType("integer");
            entity.Property(e => e.cta_debito).HasMaxLength(30);
            entity.Property(e => e.currency_code)
                .HasMaxLength(3)
                .IsFixedLength();
            entity.Property(e => e.estado)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ACTIVE'::character varying");
            entity.Property(e => e.idb).HasDefaultValue(0);
            entity.Property(e => e.inversion_cheque).HasDefaultValue(0);
            entity.Property(e => e.meses_h).HasDefaultValue(0);
            entity.Property(e => e.n_comp0).HasDefaultValue(0);
            entity.Property(e => e.n_comp1).HasDefaultValue(0);
            entity.Property(e => e.n_comp2).HasDefaultValue(0);
            entity.Property(e => e.n_comp3).HasDefaultValue(0);
            entity.Property(e => e.n_comp4).HasDefaultValue(0);
            entity.Property(e => e.n_comp5).HasDefaultValue(0);
            entity.Property(e => e.nombre).HasMaxLength(150);
            entity.Property(e => e.numero_cuenta).HasMaxLength(50);
            entity.Property(e => e.pdb).HasPrecision(28, 4);
            entity.Property(e => e.proxima_conciliacion).HasDefaultValue(0);
            entity.Property(e => e.proximo_cheque).HasPrecision(28, 4);
            entity.Property(e => e.proximo_nddb).HasDefaultValue(0);
            entity.Property(e => e.r_transf).HasDefaultValue(0);
            entity.Property(e => e.saldo_actual).HasPrecision(28, 4);
            entity.Property(e => e.saldo_c1).HasPrecision(28, 4);
            entity.Property(e => e.saldo_c2).HasPrecision(28, 4);
            entity.Property(e => e.saldo_inicial).HasPrecision(18, 2);
            entity.Property(e => e.tdc).HasDefaultValue(0);
            entity.Property(e => e.tipo)
                .HasMaxLength(20)
                .HasDefaultValueSql("'CHEQUES'::character varying");
            entity.Property(e => e.updated_by).HasMaxLength(100);
            entity.Property(e => e.v_no_ch).HasDefaultValue(0);
            entity.Property(e => e.v_no_dp).HasDefaultValue(0);
            entity.Property(e => e.v_no_nc).HasDefaultValue(0);
            entity.Property(e => e.v_no_nd).HasDefaultValue(0);

            entity.HasOne(d => d.ban_banco).WithMany(p => p.ban_cuenta)
                .HasForeignKey(d => d.ban_banco_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_ban_cuenta_banco");

            entity.HasOne(d => d.ban_cta).WithMany(p => p.ban_cuenta)
                .HasForeignKey(d => d.ban_cta_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_ban_cuenta_cta");

            entity.HasOne(d => d.company).WithMany(p => p.ban_cuenta)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_cuenta_company_id_fkey");
        });

        modelBuilder.Entity<ban_moneda>(entity =>
        {
            entity.HasKey(e => e.ban_moneda_id).HasName("ban_moneda_pkey");

            entity.HasIndex(e => new { e.company_id, e.codigo }, "ban_moneda_company_id_codigo_key").IsUnique();

            entity.HasIndex(e => e.company_id, "ix_ban_moneda_company");

            entity.Property(e => e.ban_moneda_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.codigo).HasMaxLength(5);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.descripcion).HasMaxLength(60);
            entity.Property(e => e.es_base).HasDefaultValue(false);
            entity.Property(e => e.factor).HasPrecision(28, 4);
            entity.Property(e => e.pais).HasMaxLength(25);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.company).WithMany(p => p.ban_moneda)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_moneda_company_id_fkey");
        });

        modelBuilder.Entity<ban_movimiento>(entity =>
        {
            entity.HasKey(e => e.movimiento_id).HasName("ban_movimiento_pkey");

            entity.HasIndex(e => e.banco_cuenta_id, "ix_ban_movimiento_cuenta");

            entity.HasIndex(e => new { e.origen_modulo, e.origen_documento_id }, "ix_ban_movimiento_origen");

            entity.Property(e => e.movimiento_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.aod)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'::bpchar");
            entity.Property(e => e.bene_ori).HasMaxLength(50);
            entity.Property(e => e.bene_origen).HasMaxLength(40);
            entity.Property(e => e.c_refer).HasMaxLength(35);
            entity.Property(e => e.cdcd).HasDefaultValue(0);
            entity.Property(e => e.cod_bene).HasMaxLength(30);
            entity.Property(e => e.cod_esta).HasMaxLength(30);
            entity.Property(e => e.cod_oper).HasMaxLength(10);
            entity.Property(e => e.cod_sucu).HasMaxLength(5);
            entity.Property(e => e.cod_usua).HasMaxLength(30);
            entity.Property(e => e.comentario1).HasMaxLength(50);
            entity.Property(e => e.comentario2).HasMaxLength(50);
            entity.Property(e => e.comentario3).HasMaxLength(50);
            entity.Property(e => e.conciliado).HasDefaultValue(false);
            entity.Property(e => e.consolidado).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.cta_idb).HasMaxLength(30);
            entity.Property(e => e.currency_code)
                .HasMaxLength(3)
                .IsFixedLength();
            entity.Property(e => e.estado)
                .HasMaxLength(20)
                .HasDefaultValueSql("'POSTED'::character varying");
            entity.Property(e => e.estado_legacy).HasDefaultValue(0);
            entity.Property(e => e.exchange_rate)
                .HasPrecision(18, 6)
                .HasDefaultValueSql("1");
            entity.Property(e => e.monto).HasPrecision(18, 2);
            entity.Property(e => e.monto1).HasPrecision(28, 4);
            entity.Property(e => e.monto2).HasPrecision(28, 4);
            entity.Property(e => e.monto_local).HasPrecision(18, 2);
            entity.Property(e => e.mto_cr).HasPrecision(28, 4);
            entity.Property(e => e.mto_db).HasPrecision(28, 4);
            entity.Property(e => e.mto_deb).HasPrecision(28, 4);
            entity.Property(e => e.mto_debito).HasPrecision(28, 4);
            entity.Property(e => e.mto_idb).HasPrecision(28, 4);
            entity.Property(e => e.mto_ori).HasPrecision(28, 4);
            entity.Property(e => e.mto_origen).HasPrecision(28, 4);
            entity.Property(e => e.no_conc).HasDefaultValue(0);
            entity.Property(e => e.no_ope).HasDefaultValue(0);
            entity.Property(e => e.nro_comp).HasDefaultValue(0);
            entity.Property(e => e.nro_egreso).HasPrecision(28, 4);
            entity.Property(e => e.nro_ppal).HasDefaultValue(0);
            entity.Property(e => e.obcp).HasMaxLength(1);
            entity.Property(e => e.ope_rel).HasDefaultValue(0);
            entity.Property(e => e.origen_legacy).HasMaxLength(35);
            entity.Property(e => e.origen_modulo).HasMaxLength(30);
            entity.Property(e => e.referencia).HasMaxLength(100);
            entity.Property(e => e.saldo).HasPrecision(28, 4);
            entity.Property(e => e.tdc).HasDefaultValue(0);
            entity.Property(e => e.tip_ben).HasDefaultValue(0);
            entity.Property(e => e.tipo).HasMaxLength(20);
            entity.Property(e => e.tipo_ope).HasDefaultValue(0);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.banco_cuenta).WithMany(p => p.ban_movimiento)
                .HasForeignKey(d => d.banco_cuenta_id)
                .HasConstraintName("ban_movimiento_banco_cuenta_id_fkey");

            entity.HasOne(d => d.company).WithMany(p => p.ban_movimiento)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_movimiento_company_id_fkey");
        });

        modelBuilder.Entity<ban_kardex>(entity =>
        {
            entity.HasKey(e => e.ban_kardex_id).HasName("ban_kardex_pkey");

            entity.HasIndex(e => new { e.company_id, e.banco_cuenta_id, e.fecha_movimiento }, "ix_ban_kardex_cuenta_fecha");

            entity.HasIndex(e => new { e.company_id, e.banco_cuenta_id, e.fecha_movimiento, e.estado }, "ix_ban_kardex_cuenta_fecha_estado");

            entity.HasIndex(e => e.partida_cuenta_id, "ix_ban_kardex_poliza");

            entity.HasIndex(e => e.id_tipo_transaccion, "ix_ban_kardex_tipo_transaccion");

            entity.Property(e => e.ban_kardex_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.correlativo_t_transacc).HasMaxLength(100);
            entity.Property(e => e.descripcion).HasMaxLength(500);
            entity.Property(e => e.estado).HasDefaultValue(1);
            entity.Property(e => e.estado_conciliacion).HasMaxLength(10);
            entity.Property(e => e.monto)
                .HasPrecision(28, 4)
                .HasDefaultValue(0);
            entity.Property(e => e.referencia).HasMaxLength(100);
            entity.Property(e => e.tasa_cambio)
                .HasPrecision(28, 4)
                .HasDefaultValue(1);
            entity.Property(e => e.saldo)
                .HasPrecision(28, 4)
                .HasDefaultValue(0);
            entity.Property(e => e.fecha_registro).HasDefaultValueSql("now()");
            entity.Property(e => e.usuario_conciliacion).HasMaxLength(100);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.banco_cuenta).WithMany()
                .HasForeignKey(d => d.banco_cuenta_id)
                .HasConstraintName("fk_ban_kardex_cuenta");

            entity.HasOne(d => d.ban_banco).WithMany()
                .HasForeignKey(d => d.ban_banco_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_ban_kardex_banco");

            entity.HasOne(d => d.ban_moneda).WithMany()
                .HasForeignKey(d => d.ban_moneda_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_ban_kardex_moneda");

            entity.HasOne(d => d.company).WithMany()
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("fk_ban_kardex_company");
        });

        modelBuilder.Entity<ban_movimiento_detalle>(entity =>
        {
            entity.HasKey(e => e.movimiento_detalle_id).HasName("ban_movimiento_detalle_pkey");

            entity.HasIndex(e => new { e.movimiento_id, e.linea_num }, "ban_movimiento_detalle_movimiento_id_linea_num_key").IsUnique();

            entity.HasIndex(e => e.company_id, "ix_ban_mov_detalle_company");

            entity.HasIndex(e => e.movimiento_id, "ix_ban_mov_detalle_movimiento");

            entity.Property(e => e.movimiento_detalle_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.base_tr).HasPrecision(28, 4);
            entity.Property(e => e.cdcd).HasDefaultValue(0);
            entity.Property(e => e.cod_cen_cto).HasMaxLength(30);
            entity.Property(e => e.cod_cta).HasMaxLength(30);
            entity.Property(e => e.cod_esta).HasMaxLength(30);
            entity.Property(e => e.cod_oper).HasMaxLength(10);
            entity.Property(e => e.cod_sucu).HasMaxLength(5);
            entity.Property(e => e.cod_tercero).HasMaxLength(30);
            entity.Property(e => e.cod_usua).HasMaxLength(30);
            entity.Property(e => e.consolidado).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.descripcion).HasMaxLength(60);
            entity.Property(e => e.dh).HasDefaultValue(0);
            entity.Property(e => e.enc_ope).HasDefaultValue(0);
            entity.Property(e => e.es_cuenta).HasDefaultValue(0);
            entity.Property(e => e.es_transf).HasDefaultValue(0);
            entity.Property(e => e.estado).HasDefaultValue(0);
            entity.Property(e => e.flujo_e).HasPrecision(28, 3);
            entity.Property(e => e.linea_num).HasDefaultValue(0);
            entity.Property(e => e.monto).HasPrecision(28, 4);
            entity.Property(e => e.mto_cr).HasPrecision(28, 4);
            entity.Property(e => e.mto_db).HasPrecision(28, 4);
            entity.Property(e => e.n_mo).HasDefaultValue(0);
            entity.Property(e => e.origen).HasMaxLength(35);
            entity.Property(e => e.si_centro).HasDefaultValue(0);
            entity.Property(e => e.si_tercero).HasDefaultValue(0);
            entity.Property(e => e.tercero).HasMaxLength(50);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.company).WithMany(p => p.ban_movimiento_detalle)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_movimiento_detalle_company_id_fkey");

            entity.HasOne(d => d.movimiento).WithMany(p => p.ban_movimiento_detalle)
                .HasForeignKey(d => d.movimiento_id)
                .HasConstraintName("ban_movimiento_detalle_movimiento_id_fkey");
        });

        modelBuilder.Entity<ban_movimiento_transito>(entity =>
        {
            entity.HasKey(e => e.movimiento_transito_id).HasName("ban_movimiento_transito_pkey");

            entity.HasIndex(e => e.company_id, "ix_ban_mov_transito_company");

            entity.HasIndex(e => e.banco_cuenta_id, "ix_ban_mov_transito_cuenta");

            entity.HasIndex(e => e.movimiento_id, "ix_ban_mov_transito_movimiento");

            entity.Property(e => e.movimiento_transito_id).UseIdentityAlwaysColumn();
            entity.Property(e => e.aod)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'::bpchar");
            entity.Property(e => e.c_refer).HasMaxLength(35);
            entity.Property(e => e.cdcd).HasDefaultValue(0);
            entity.Property(e => e.cod_bene).HasMaxLength(30);
            entity.Property(e => e.cod_usua).HasMaxLength(30);
            entity.Property(e => e.comentario1).HasMaxLength(60);
            entity.Property(e => e.comentario2).HasMaxLength(50);
            entity.Property(e => e.comentario3).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("now()");
            entity.Property(e => e.created_by)
                .HasMaxLength(100)
                .HasDefaultValueSql("CURRENT_USER");
            entity.Property(e => e.descripcion).HasMaxLength(50);
            entity.Property(e => e.documento).HasMaxLength(25);
            entity.Property(e => e.estado).HasDefaultValue(0);
            entity.Property(e => e.monto).HasPrecision(28, 4);
            entity.Property(e => e.monto1).HasPrecision(28, 4);
            entity.Property(e => e.monto2).HasPrecision(28, 4);
            entity.Property(e => e.mto_cr).HasPrecision(28, 4);
            entity.Property(e => e.mto_db).HasPrecision(28, 4);
            entity.Property(e => e.no_conc).HasDefaultValue(0);
            entity.Property(e => e.no_ope).HasDefaultValue(0);
            entity.Property(e => e.obcp)
                .HasMaxLength(1)
                .HasDefaultValueSql("'N'::character varying");
            entity.Property(e => e.origen).HasMaxLength(35);
            entity.Property(e => e.saldo).HasPrecision(28, 4);
            entity.Property(e => e.tdc).HasDefaultValue(0);
            entity.Property(e => e.updated_by).HasMaxLength(100);

            entity.HasOne(d => d.banco_cuenta).WithMany(p => p.ban_movimiento_transito)
                .HasForeignKey(d => d.banco_cuenta_id)
                .HasConstraintName("ban_movimiento_transito_banco_cuenta_id_fkey");

            entity.HasOne(d => d.company).WithMany(p => p.ban_movimiento_transito)
                .HasForeignKey(d => d.company_id)
                .HasConstraintName("ban_movimiento_transito_company_id_fkey");

            entity.HasOne(d => d.movimiento).WithMany(p => p.ban_movimiento_transito)
                .HasForeignKey(d => d.movimiento_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("ban_movimiento_transito_movimiento_id_fkey");
        });

        modelBuilder.Entity<pagos_miscelaneos_dtl>(entity =>
        {
            entity.HasKey(e => new { e.recibo, e.linea }).HasName("pagos_miscelaneos_dtl_pkey");

            entity.ToTable("pagos_miscelaneos_dtl");

            entity.Property(e => e.concepto).HasMaxLength(500);
            entity.Property(e => e.monto).HasPrecision(18, 2);

            entity.HasOne(d => d.reciboNavigation).WithMany(p => p.pagos_miscelaneos_dtl)
                .HasForeignKey(d => d.recibo)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("pagos_miscelaneos_dtl_recibo_fkey");
        });

        OnModelCreatingPartial(modelBuilder);
        ApplyCompanyScope(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

