using System;
using System.Collections;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "public");

            migrationBuilder.CreateTable(
                name: "abogado",
                columns: table => new
                {
                    abogado_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    abogado_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    abogado_nombrecorto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    abogado_nombrelargo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    abogado_telefono = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    codcuenta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("abogado_id_pkey", x => x.abogado_id);
                });

            migrationBuilder.CreateTable(
                name: "ajustes",
                columns: table => new
                {
                    documento = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying", nullable: true),
                    observacion = table.Column<string>(type: "character varying", nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    motivo = table.Column<int>(type: "integer", nullable: true),
                    tipo_nota = table.Column<int>(type: "integer", nullable: true),
                    saldo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    periodo = table.Column<string>(type: "character varying", nullable: true),
                    lectura = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    usuario = table.Column<string>(type: "character varying", nullable: true),
                    cliente_clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    correlativo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ajustes_pkey", x => x.documento);
                });

            migrationBuilder.CreateTable(
                name: "ajustes_detalle",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    documento = table.Column<int>(type: "integer", nullable: true),
                    tipo_servicio = table.Column<string>(type: "character varying", nullable: true),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ajustes_detalle_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "axl_accion_cobranza",
                columns: table => new
                {
                    cod_accion = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("axl_accion_cobranza_pkey", x => x.cod_accion);
                });

            migrationBuilder.CreateTable(
                name: "axl_observacion_cobranza",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    observacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("axl_observacion_cobranza_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "barrio",
                columns: table => new
                {
                    barrio_codigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("barrio_pkey", x => x.barrio_codigo);
                });

            migrationBuilder.CreateTable(
                name: "bnc_bancos",
                columns: table => new
                {
                    cod_banco = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    recibe_dinero = table.Column<bool>(type: "boolean", nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()"),
                    observaciones = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("bnc_bancos_pkey", x => x.cod_banco);
                });

            migrationBuilder.CreateTable(
                name: "bnc_cuentas",
                columns: table => new
                {
                    cod_banco = table.Column<int>(type: "integer", nullable: false),
                    cod_cuenta = table.Column<int>(type: "integer", nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: false),
                    cuenta_contable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    emite_cheques = table.Column<bool>(type: "boolean", nullable: true),
                    numero_cheque = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ruta_transito = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    saldo = table.Column<double>(type: "double precision", nullable: true),
                    saldo_conciliado = table.Column<double>(type: "double precision", nullable: true),
                    tasa_promedio = table.Column<double>(type: "double precision", nullable: true),
                    tipo_cuenta = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()"),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "bnc_kardex_cuentas",
                columns: table => new
                {
                    cod_banco = table.Column<int>(type: "integer", nullable: false),
                    cod_cuenta = table.Column<int>(type: "integer", nullable: false),
                    conciliada = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    correlativo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    disas_tran_ant = table.Column<short>(type: "smallint", nullable: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fecha_transaccion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    monto = table.Column<double>(type: "double precision", nullable: false),
                    monto_dolares = table.Column<double>(type: "double precision", nullable: true),
                    num_cheque = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    pda = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    referencia1 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    referencia2 = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    referencia_afecta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    saldo = table.Column<double>(type: "double precision", nullable: false),
                    saldo_ant = table.Column<double>(type: "double precision", nullable: false),
                    saldo_dol = table.Column<double>(type: "double precision", nullable: true),
                    saldo_dol_ant = table.Column<double>(type: "double precision", nullable: true),
                    suma_balance = table.Column<double>(type: "double precision", nullable: true),
                    tasa = table.Column<double>(type: "double precision", nullable: true),
                    tipo_transacion = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    tipo_transacion2 = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    ultima_trn = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    usuario_creo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "bnc_tipotransacciones",
                columns: table => new
                {
                    cod_centrocosto = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cod_partida = table.Column<int>(type: "integer", nullable: false),
                    correlativo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    cuenta_contable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    del_sistema = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false),
                    destino = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    emite_cheque = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false),
                    entra_sale = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    filtro = table.Column<short>(type: "smallint", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    pad = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    pda = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    rel_empleados = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    tipo_transaccion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    trn_prestamo = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    usuario_creo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    usuario_modifica = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()"),
                    cuenta_alterna = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "cai",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ruta = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cai = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    fecha_emision = table.Column<DateOnly>(type: "date", nullable: true),
                    rango_inicial = table.Column<int>(type: "integer", nullable: true),
                    rango_final = table.Column<int>(type: "integer", nullable: true),
                    codigo_base = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    contador_actual = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cai_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "calendariopro",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ano = table.Column<int>(type: "integer", nullable: true),
                    mes = table.Column<int>(type: "integer", nullable: true),
                    ciclo = table.Column<string>(type: "character varying", nullable: true),
                    fechaal = table.Column<DateOnly>(type: "date", nullable: true),
                    fechalec = table.Column<DateOnly>(type: "date", nullable: true),
                    fechafac = table.Column<DateOnly>(type: "date", nullable: true),
                    fecharefac = table.Column<DateOnly>(type: "date", nullable: true),
                    fechavence = table.Column<DateOnly>(type: "date", nullable: true),
                    diasvence = table.Column<int>(type: "integer", nullable: true),
                    fechafac2 = table.Column<DateOnly>(type: "date", nullable: true),
                    fechavence2 = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("calendariopro_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "catalogo_cajas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    fecha_apertura = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalogo_cajas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categoria_servicio",
                columns: table => new
                {
                    categoria_servicio_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'9', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("categoria_servicio_pkey", x => x.categoria_servicio_id);
                });

            migrationBuilder.CreateTable(
                name: "causa_refacturacion",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    tipo = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("causa_refacturacion_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "cfg_company",
                schema: "public",
                columns: table => new
                {
                    company_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    commercial_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    tax_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    country_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    timezone = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cfg_company", x => x.company_id);
                });

            migrationBuilder.CreateTable(
                name: "ciclos",
                columns: table => new
                {
                    ciclos_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ciclos_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ciclos_descripcioncorta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ciclos_descripcionlarga = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ciclos_id_pkey", x => x.ciclos_id);
                });

            migrationBuilder.CreateTable(
                name: "cln_accion_cobranza",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codigocliente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    accion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    observacion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cln_accion_cobranza_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "cnt_balance",
                columns: table => new
                {
                    cod_cuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()"),
                    niveles = table.Column<int>(type: "integer", nullable: true),
                    cod_reporte = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "cnt_catalogo",
                columns: table => new
                {
                    cod_cuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cod_mayor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    cod_grupo_cta = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    cuenta_ext = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    fecha_creacion = table.Column<DateOnly>(type: "date", nullable: true),
                    flag_budget = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    flag_fijovariable = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    status = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    tipo_cuenta = table.Column<short>(type: "smallint", nullable: true),
                    ult_fecha_modificada = table.Column<DateOnly>(type: "date", nullable: true),
                    ult_usuario = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    usuario_creo = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()"),
                    cod_sub_grupo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    cscuenta = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_catalogo_pkey", x => x.cod_cuenta);
                });

            migrationBuilder.CreateTable(
                name: "cnt_centro_costos_grupo",
                columns: table => new
                {
                    codccg = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_centro_costos_grupo_pkey", x => x.codccg);
                });

            migrationBuilder.CreateTable(
                name: "cnt_centro_costos_subgrupo",
                columns: table => new
                {
                    codccg = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    codsccg = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_centro_costos_subgrupo_pkey", x => new { x.codccg, x.codsccg });
                });

            migrationBuilder.CreateTable(
                name: "cnt_centrocostos_dtl",
                columns: table => new
                {
                    cuenta = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    aprobado = table.Column<decimal>(type: "money", nullable: false),
                    compro = table.Column<decimal>(type: "money", nullable: false),
                    pagado = table.Column<decimal>(type: "money", nullable: false),
                    obs = table.Column<decimal>(type: "money", nullable: false),
                    valor = table.Column<decimal>(type: "money", nullable: false),
                    ampl = table.Column<decimal>(type: "money", nullable: false),
                    saldo = table.Column<decimal>(type: "money", nullable: false),
                    mov = table.Column<decimal>(type: "money", nullable: false),
                    transfe = table.Column<decimal>(type: "money", nullable: false),
                    fondo = table.Column<decimal>(type: "money", nullable: false),
                    proyeccion = table.Column<decimal>(type: "money", nullable: false),
                    nuevoaprobado = table.Column<decimal>(type: "money", nullable: false),
                    tipo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "cnt_centrocostos_hdr",
                columns: table => new
                {
                    cuenta = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    codccg = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    codsccg = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    contable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_centrocostos_hdr_pkey", x => x.cuenta);
                });

            migrationBuilder.CreateTable(
                name: "cnt_centroscosto",
                columns: table => new
                {
                    cod_centrocosto = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nom_centrocosto = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    status = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    flag_tipo_cc = table.Column<bool>(type: "boolean", nullable: true),
                    fechadesde = table.Column<DateOnly>(type: "date", nullable: true),
                    fechahasta = table.Column<DateOnly>(type: "date", nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_centroscosto_pkey", x => x.cod_centrocosto);
                });

            migrationBuilder.CreateTable(
                name: "cnt_grupo_cta",
                columns: table => new
                {
                    cod_grupo_cta = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_grupo_cta_pkey", x => x.cod_grupo_cta);
                });

            migrationBuilder.CreateTable(
                name: "cnt_mayores",
                columns: table => new
                {
                    cod_grupo_cta = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cod_sub_grupo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cod_mayor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    orden = table.Column<short>(type: "smallint", nullable: true),
                    partida_resumen = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_mayores_pkey", x => new { x.cod_grupo_cta, x.cod_sub_grupo, x.cod_mayor });
                });

            migrationBuilder.CreateTable(
                name: "cnt_partidas_dtl",
                columns: table => new
                {
                    cargos = table.Column<decimal>(type: "numeric(15,4)", precision: 15, scale: 4, nullable: true),
                    cod_centrocosto = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cod_cliente = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    cod_cuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    cod_marcagrupo = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    cod_partida = table.Column<int>(type: "integer", nullable: true),
                    comprobante = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    concepto = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    correlativo = table.Column<int>(type: "integer", nullable: true),
                    creditos = table.Column<decimal>(type: "numeric(15,4)", precision: 15, scale: 4, nullable: true),
                    tasacambio = table.Column<int>(type: "integer", nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "cnt_partidas_hdr",
                columns: table => new
                {
                    cod_partida = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cod_empresa = table.Column<int>(type: "integer", nullable: false),
                    cod_tipopartid = table.Column<int>(type: "integer", nullable: false),
                    correlativo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    fecha_creacion = table.Column<DateOnly>(type: "date", nullable: true),
                    hora_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fecha_partida = table.Column<DateOnly>(type: "date", nullable: true),
                    maestro = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    sinopsis = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    tipo_transaccion = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    usuario_creacion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_partidas_hdr_pkey", x => x.cod_partida);
                });

            migrationBuilder.CreateTable(
                name: "cnt_rubros",
                columns: table => new
                {
                    cod_empresa = table.Column<int>(type: "integer", nullable: false),
                    cod_reporte = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    orden_reporte = table.Column<int>(type: "integer", nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "cnt_saldos",
                columns: table => new
                {
                    cargos = table.Column<double>(type: "double precision", nullable: false),
                    cod_cuenta = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: false),
                    creditos = table.Column<double>(type: "double precision", nullable: false),
                    fecha_cierre = table.Column<DateOnly>(type: "date", nullable: true),
                    hora_cierre = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    saldo_actual = table.Column<double>(type: "double precision", nullable: false),
                    saldo_anterior = table.Column<double>(type: "double precision", nullable: false),
                    ult_fecha_modificada = table.Column<DateOnly>(type: "date", nullable: true),
                    ult_usuario = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "cnt_sub_cuenta",
                columns: table => new
                {
                    cod_grupo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    csgrupo = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cod_mayor = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cscuenta = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_sub_cuenta_pkey", x => new { x.cod_grupo, x.csgrupo, x.cod_mayor, x.cscuenta });
                });

            migrationBuilder.CreateTable(
                name: "cnt_sub_grupo",
                columns: table => new
                {
                    cod_grupo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    cod_sub_grupo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    cod_empresa = table.Column<int>(type: "integer", nullable: true),
                    descripcion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_sub_grupo_pkey", x => new { x.cod_grupo, x.cod_sub_grupo });
                });

            migrationBuilder.CreateTable(
                name: "cnt_tipopartida",
                columns: table => new
                {
                    cod_tipopartida = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("cnt_tipopartida_pkey", x => x.cod_tipopartida);
                });

            migrationBuilder.CreateTable(
                name: "con_centro_costo",
                schema: "public",
                columns: table => new
                {
                    cost_center_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_centro_costo", x => x.cost_center_id);
                });

            migrationBuilder.CreateTable(
                name: "con_diario",
                schema: "public",
                columns: table => new
                {
                    journal_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    sequence_prefix = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    last_sequence = table.Column<long>(type: "bigint", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    allows_manual = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_diario", x => x.journal_id);
                });

            migrationBuilder.CreateTable(
                name: "con_periodo_contable",
                schema: "public",
                columns: table => new
                {
                    period_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    closed_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_periodo_contable", x => x.period_id);
                });

            migrationBuilder.CreateTable(
                name: "con_plan_cuentas",
                schema: "public",
                columns: table => new
                {
                    account_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    parent_account_id = table.Column<long>(type: "bigint", nullable: true),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    account_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    level = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    allows_posting = table.Column<bool>(type: "boolean", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_plan_cuentas", x => x.account_id);
                    table.ForeignKey(
                        name: "FK_con_plan_cuentas_con_plan_cuentas_parent_account_id",
                        column: x => x.parent_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_plantilla_partida_hdr",
                schema: "public",
                columns: table => new
                {
                    template_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    module = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_plantilla_partida_hdr", x => x.template_id);
                });

            migrationBuilder.CreateTable(
                name: "concepto_cobro_adicional",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    concepto = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("concepto_cobro_adicional_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "condicion_lectura",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying", nullable: false),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    tipo = table.Column<string>(type: "character varying", nullable: true),
                    facturacion = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("condicion_lectura_pkey", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_app_lectura_medidores",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    valor_numeros = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    valor_letras = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("configuracion_app_lectura_medidores_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_cobros_adicionales",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    concepto_id = table.Column<int>(type: "integer", nullable: false),
                    categoria_id = table.Column<int>(type: "integer", nullable: true),
                    aplica_descuento = table.Column<bool>(type: "boolean", nullable: true),
                    servicio_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("configuracion_cobros_adicionales_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "coordenadas_empleado",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: true),
                    mes = table.Column<int>(type: "integer", nullable: true),
                    dia = table.Column<int>(type: "integer", nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    latitud = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    longitud = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("coordenadas_empleado_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "factura",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numrecibo = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'3075052', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numfactura = table.Column<string>(type: "character varying", nullable: true),
                    clientecodigo = table.Column<string>(type: "character varying", nullable: true),
                    tipofactura = table.Column<string>(type: "character varying", nullable: true),
                    ano = table.Column<string>(type: "character varying", nullable: true),
                    mes = table.Column<string>(type: "character varying", nullable: true),
                    fechaemision = table.Column<DateOnly>(type: "date", nullable: true),
                    fechavence = table.Column<DateOnly>(type: "date", nullable: true),
                    rtn = table.Column<string>(type: "character varying", nullable: true),
                    periodo = table.Column<string>(type: "character varying", nullable: true),
                    numdei = table.Column<string>(type: "character varying", nullable: true),
                    saldototal = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    usuario = table.Column<string>(type: "character varying", nullable: true),
                    identidad = table.Column<string>(type: "character varying", nullable: true),
                    estado = table.Column<string>(type: "character varying", nullable: true),
                    recolectora = table.Column<string>(type: "character varying", nullable: true),
                    fechapago = table.Column<DateOnly>(type: "date", nullable: true),
                    tipofacturacion = table.Column<string>(type: "character varying", nullable: true),
                    referencia = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("factura_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "factura_detalle",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    numrecibo = table.Column<int>(type: "integer", nullable: true),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    tiposervicio = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    montovalor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    factura_id = table.Column<int>(type: "integer", nullable: true),
                    montovalor_saldo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("factura_detalle_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grupo_estado_detalle",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    grupo_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("grupo_estado_detalle_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "grupoestado",
                columns: table => new
                {
                    idgrupo = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("grupoestado_pkey", x => x.idgrupo);
                });

            migrationBuilder.CreateTable(
                name: "historialmes",
                columns: table => new
                {
                    ano = table.Column<decimal>(type: "numeric(4)", precision: 4, nullable: false),
                    mes = table.Column<decimal>(type: "numeric(2)", precision: 2, nullable: false),
                    ciclo = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: false),
                    ruta = table.Column<string>(type: "character(10)", fixedLength: true, maxLength: 10, nullable: true, defaultValueSql: "NULL::bpchar"),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    sep = table.Column<decimal>(type: "numeric(1)", precision: 1, nullable: true, defaultValueSql: "NULL::numeric"),
                    sep2 = table.Column<decimal>(type: "numeric(1)", precision: 1, nullable: true, defaultValueSql: "'0'::numeric"),
                    fechacierre = table.Column<DateOnly>(type: "date", nullable: true),
                    usuarioapertura = table.Column<string>(type: "character(150)", fixedLength: true, maxLength: 150, nullable: true, defaultValueSql: "NULL::bpchar"),
                    usuariocierre = table.Column<string>(type: "character(20)", fixedLength: true, maxLength: 20, nullable: true, defaultValueSql: "NULL::bpchar"),
                    cerrado = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    cerrarperiodo = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "'P'::bpchar"),
                    fechaperiodo = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    _2Sep = table.Column<string>(name: "2-Sep", type: "character varying(32767)", maxLength: 32767, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("historialmes_pkey", x => new { x.ano, x.mes, x.ciclo });
                });

            migrationBuilder.CreateTable(
                name: "historicomedicion",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'1119616', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    ano = table.Column<decimal>(type: "numeric(4)", precision: 4, nullable: false),
                    mes = table.Column<decimal>(type: "numeric(2)", precision: 2, nullable: false),
                    contador = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "NULL::character varying"),
                    ciclo = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, defaultValueSql: "'0'::bpchar"),
                    ruta = table.Column<string>(type: "character(25)", fixedLength: true, maxLength: 25, nullable: true, defaultValueSql: "''::bpchar"),
                    secuencia = table.Column<string>(type: "character(9)", fixedLength: true, maxLength: 9, nullable: true, defaultValueSql: "NULL::bpchar"),
                    clave = table.Column<string>(type: "character(15)", fixedLength: true, maxLength: 15, nullable: true, defaultValueSql: "NULL::bpchar"),
                    fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "NULL::character varying"),
                    lect_act = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true, defaultValueSql: "NULL::numeric"),
                    lect_ant = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true, defaultValueSql: "NULL::numeric"),
                    fecha_lect_act = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_lect_ant = table.Column<DateOnly>(type: "date", nullable: true),
                    hora = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    consumo = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true, defaultValueSql: "'0'::numeric"),
                    consumoant = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true, defaultValueSql: "'0'::numeric"),
                    tservi1 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    taservi1 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    tservi2 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    taservi2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    tservi3 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    taservi3 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    tservi4 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    taservi4 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    cerrado = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "'0'::bpchar"),
                    ser1 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser2 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser3 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser4 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    comentario = table.Column<string>(type: "character varying(145)", maxLength: 145, nullable: true, defaultValueSql: "NULL::character varying"),
                    propietario = table.Column<string>(type: "character(100)", fixedLength: true, maxLength: 100, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ubicacion = table.Column<string>(type: "character(100)", fixedLength: true, maxLength: 100, nullable: true, defaultValueSql: "NULL::bpchar"),
                    otros = table.Column<string>(type: "character(20)", fixedLength: true, maxLength: 20, nullable: true, defaultValueSql: "NULL::bpchar"),
                    categoria = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "'0'::bpchar"),
                    observacion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true, defaultValueSql: "NULL::character varying"),
                    mes2 = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, defaultValueSql: "''::bpchar"),
                    condicion = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    lec_prom = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true, defaultValueSql: "'0'::numeric"),
                    tipolectura = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "''::bpchar"),
                    codinfo = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, defaultValueSql: "''::bpchar"),
                    ser5 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser6 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser7 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser8 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser9 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    ser10 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar"),
                    tservi5 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    tservi6 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    tservi7 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    tservi8 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    tservi9 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    tservi10 = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true, defaultValueSql: "NULL::bpchar"),
                    taservi5 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    taservi6 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    taservi7 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    taservi8 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    taservi9 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    taservi10 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true, defaultValueSql: "0.00"),
                    revision = table.Column<decimal>(type: "numeric(1)", precision: 1, nullable: true, defaultValueSql: "'0'::numeric"),
                    orden = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "'0'::bpchar"),
                    revision2 = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "'0'::bpchar"),
                    sel = table.Column<decimal>(type: "numeric(1)", precision: 1, nullable: true, defaultValueSql: "'0'::numeric"),
                    numeroord = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true, defaultValueSql: "'0'::numeric"),
                    ajuste = table.Column<string>(type: "character(2)", fixedLength: true, maxLength: 2, nullable: true, defaultValueSql: "''::bpchar"),
                    numerofactura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true, defaultValueSql: "NULL::character varying"),
                    correlativocai = table.Column<long>(type: "bigint", nullable: true),
                    idcai = table.Column<long>(type: "bigint", nullable: true),
                    imagenmedidor = table.Column<byte[]>(type: "bytea", nullable: true),
                    descuentoapp = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true, defaultValueSql: "NULL::numeric"),
                    categoriacliente = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true, defaultValueSql: "NULL::bpchar")
                },
                constraints: table =>
                {
                    table.PrimaryKey("historicomedicion_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "historicosinmedidor",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cuenta = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: true),
                    mes = table.Column<int>(type: "integer", nullable: true),
                    numerofactura = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    correlativocai = table.Column<int>(type: "integer", nullable: true),
                    idcai = table.Column<int>(type: "integer", nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuario = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("historicosinmedidor_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "identityrole",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "identityuser",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "informativo",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cod_condicion = table.Column<string>(type: "character varying", nullable: true),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("informativo_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "log_cliclo_descarga_app",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    anio = table.Column<int>(type: "integer", nullable: true),
                    mes = table.Column<int>(type: "integer", nullable: true),
                    ciclo = table.Column<int>(type: "integer", nullable: true),
                    usuario = table.Column<string>(type: "character varying", nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "maestro_medidor",
                columns: table => new
                {
                    maestro_medidor_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'23718', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    maestro_medidor_numero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    maestro_medidor_marca = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    maestro_medidor_fecha_instala = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    maestro_medidor_diametro = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    maestro_medidor_empleado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    maestro_medidor_acueducto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("maestro_medidor_id_pkey", x => x.maestro_medidor_id);
                });

            migrationBuilder.CreateTable(
                name: "materialesapp",
                columns: table => new
                {
                    codproduc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    descripcion = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "miscelaneos_catalogo",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    nombre = table.Column<string>(type: "character varying", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("miscelaneos_catalogo_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "ops_compromisos",
                columns: table => new
                {
                    cod_empresa = table.Column<int>(type: "integer", nullable: false),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    orden = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    beneficiario = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    cod_programa = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    cod_actvidad = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    cod_gastos = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    compromiso = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fechavence = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    concepto = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    pagos = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    fechap = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    docu = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    codproy = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    fondo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    paga = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    cta_contable = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    ctacobrar = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    ordenp = table.Column<int>(type: "integer", nullable: true),
                    id = table.Column<int>(type: "integer", nullable: true),
                    cod_proveedor = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: true),
                    bor = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    aplicado = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    rtn = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "orden_trabajo",
                columns: table => new
                {
                    orden_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'227878', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    orden_numero = table.Column<int>(type: "integer", nullable: false),
                    maestro_cliente_clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    concepto = table.Column<string>(type: "character varying", nullable: false),
                    estado = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    informe = table.Column<string>(type: "character varying", nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: false),
                    mes = table.Column<int>(type: "integer", nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    usuario = table.Column<string>(type: "character varying", nullable: true),
                    tipo = table.Column<string>(type: "character varying", nullable: true),
                    empleado = table.Column<string>(type: "character varying", nullable: true),
                    personas = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("orden_trabajo_pkey", x => x.orden_id);
                });

            migrationBuilder.CreateTable(
                name: "orden_trabajo_adjunto",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    adjunto = table.Column<byte[]>(type: "bytea", nullable: true),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    latitud = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    longitud = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    numeroorden = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    fechainicio = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fechafin = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fechaobtenerordenes = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("orden_trabajo_adjunto_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "orden_trabajo_estado",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    permite_asignacion = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("orden_trabajo_estado_pkey", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "ordent_mate",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cuenta = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numero = table.Column<int>(type: "integer", nullable: true),
                    codproduc = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    cantidad = table.Column<int>(type: "integer", nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ordent_mate_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "pagos_bancos",
                columns: table => new
                {
                    idreg = table.Column<char>(type: "character(1)", maxLength: 1, nullable: true),
                    cliente_clave = table.Column<string>(type: "character varying", nullable: true),
                    rtn = table.Column<string>(type: "character varying", nullable: true),
                    recibo = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true),
                    montop = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    fechap = table.Column<DateOnly>(type: "date", nullable: true),
                    referencia = table.Column<string>(type: "character varying", nullable: true),
                    banco = table.Column<string>(type: "character varying", nullable: true),
                    sucursal = table.Column<string>(type: "character varying", nullable: true),
                    agencia = table.Column<string>(type: "character varying", nullable: true),
                    cajero = table.Column<string>(type: "character varying", nullable: true),
                    terminal = table.Column<string>(type: "character varying", nullable: true),
                    horap = table.Column<TimeOnly>(type: "time without time zone", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "pagos_miscelaneos",
                columns: table => new
                {
                    recibo = table.Column<long>(type: "bigint", nullable: false),
                    cliente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_miscelaneos", x => x.recibo);
                });

            migrationBuilder.CreateTable(
                name: "pagovariostemp",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    recibo = table.Column<int>(type: "integer", nullable: true),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    fecha = table.Column<DateOnly>(type: "date", nullable: true),
                    fecha_vence = table.Column<DateOnly>(type: "date", nullable: true),
                    identidad = table.Column<string>(type: "character varying", nullable: true),
                    nombre = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    valor = table.Column<decimal>(name: "valor ", type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    usuario = table.Column<string>(type: "character varying", nullable: true),
                    tipo_servicio = table.Column<string>(type: "character varying", nullable: true),
                    tipo_factura = table.Column<string>(type: "character varying", nullable: true),
                    cod_banco = table.Column<string>(type: "character varying", nullable: true),
                    cajero = table.Column<string>(type: "character varying", nullable: true),
                    cliente_clave = table.Column<string>(type: "character varying", nullable: true),
                    estado = table.Column<string>(type: "character varying", nullable: true),
                    expe = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pagovariostemp_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "portal_branding",
                columns: table => new
                {
                    branding_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_name = table.Column<string>(type: "text", nullable: false),
                    company_short_name = table.Column<string>(type: "text", nullable: false),
                    logo_mime = table.Column<string>(type: "text", nullable: false),
                    logo_bytes = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_portal_branding", x => x.branding_id);
                });

            migrationBuilder.CreateTable(
                name: "presupuesto_fondos",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    fondo_descripcion = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("presupuesto_fondos_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "presupuestos",
                columns: table => new
                {
                    id_presupuesto = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    centro_costo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    monto = table.Column<double>(type: "double precision", nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    usuario_creo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    usuario_modifico = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fondo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("presupuestos_pkey", x => x.id_presupuesto);
                });

            migrationBuilder.CreateTable(
                name: "proyectos",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    empre = table.Column<string>(type: "character varying", nullable: true),
                    ano = table.Column<int>(type: "integer", nullable: true),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    lugar = table.Column<string>(type: "character varying", nullable: true),
                    ubicacion = table.Column<string>(type: "character varying", nullable: true),
                    aprobado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    supervisor = table.Column<string>(type: "character varying", nullable: true),
                    ejectutor = table.Column<string>(type: "character varying", nullable: true),
                    presupuesto = table.Column<string>(type: "character varying", nullable: true),
                    fecha1 = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fecha2 = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fuente_financiamiento = table.Column<string>(type: "character varying", nullable: true),
                    ampliado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    pagado = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    fondo = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("proyectos_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "pruebainsert",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ejemplo = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "prv_compromiso_dtl",
                columns: table => new
                {
                    numero_orden = table.Column<int>(type: "integer", nullable: false),
                    cod_presupuestario = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    programa = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    actividad = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    objeto_gasto = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cuenta_gasto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    conceptodtl = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "prv_compromiso_hdr",
                columns: table => new
                {
                    numero_orden = table.Column<int>(type: "integer", nullable: false),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    concepto = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cod_proveedor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    flag_proveedor = table.Column<int>(type: "integer", nullable: true),
                    cuenta_contable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    cod_proyecto = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    rtn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pagar_a = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status_transacc = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "prv_kardex",
                columns: table => new
                {
                    cod_proveedor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    correlativo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cuenta_debitar = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    dias_trn_ant = table.Column<short>(type: "smallint", nullable: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_transaccion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    fec_vencimiento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    monto = table.Column<double>(type: "double precision", nullable: false),
                    monto_dolares = table.Column<double>(type: "double precision", nullable: true),
                    num_cheque = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: true),
                    observaciones = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    pda = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    referencia1 = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                    referencia2 = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    referencia_afecta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    saldo = table.Column<double>(type: "double precision", nullable: true),
                    saldo_anterior = table.Column<double>(type: "double precision", nullable: true),
                    saldo_dolares = table.Column<double>(type: "double precision", nullable: true),
                    status_pago = table.Column<BitArray>(type: "bit(1)", nullable: true),
                    suma_balance = table.Column<double>(type: "double precision", nullable: true),
                    tipo_transaccion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    tipo_transaccion2 = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    ultima_trn = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    usuario_creo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rowid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    correlativo_dei = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    cai = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    cod_correlativo_dei = table.Column<int>(type: "integer", nullable: true),
                    nombre_proveedor_p = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    saldo_anterior_dol = table.Column<double>(type: "double precision", nullable: true),
                    cuenta_acreditar = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "prv_proveedores",
                columns: table => new
                {
                    cod_proveedor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    cod_tipoproveedor = table.Column<short>(type: "smallint", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    cuenta_contable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    direccion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    status = table.Column<bool>(type: "boolean", nullable: true),
                    cuenta_bancaria = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()"),
                    compras_acum = table.Column<double>(type: "double precision", nullable: true),
                    compras_dolares = table.Column<double>(type: "double precision", nullable: true),
                    saldo_actual = table.Column<double>(type: "double precision", nullable: true),
                    saldo_act_dolares = table.Column<double>(type: "double precision", nullable: true),
                    saldo_anterior = table.Column<double>(type: "double precision", nullable: true),
                    saldo_ant_doleres = table.Column<double>(type: "double precision", nullable: true),
                    razon_social = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    rtn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pagina_web = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    fax = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    nombrebanco1 = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    nombrebanco2 = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "prv_tipoproveedor",
                columns: table => new
                {
                    cod_tipoproveedor = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    observaciones = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    rowid = table.Column<Guid>(type: "uuid", nullable: true, defaultValueSql: "gen_random_uuid()")
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "prv_tipostransacc",
                columns: table => new
                {
                    tipo_transaccion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    cod_tipopartida = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    correlativo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    cuenta_contable = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    del_sistema = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    entra_sale = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    nombre = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    observaciones = table.Column<string>(type: "text", nullable: true),
                    pda = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    usuario_creo = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    usuario_modifica = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    rowid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    cod_correlativo_dei = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PRV_TIPOSTRANSACC_pkey", x => x.tipo_transaccion);
                });

            migrationBuilder.CreateTable(
                name: "recolectora",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    descripcion = table.Column<string>(type: "character(40)", fixedLength: true, maxLength: 40, nullable: true),
                    ctabanco = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    aplica = table.Column<decimal>(type: "numeric(11,4)", precision: 11, scale: 4, nullable: true),
                    contable = table.Column<string>(type: "character(20)", fixedLength: true, maxLength: 20, nullable: true),
                    llave = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    vigencia = table.Column<DateOnly>(type: "date", nullable: true),
                    idbancows = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    logo = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("recolectora_pkey", x => x.codigo);
                });

            migrationBuilder.CreateTable(
                name: "servicios",
                columns: table => new
                {
                    servicios_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    servicios_codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    servicios_descripcioncorta = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    servicios_descripcionlarga = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("servicios_id_pkey", x => x.servicios_id);
                });

            migrationBuilder.CreateTable(
                name: "tarifas",
                columns: table => new
                {
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    categoria_id = table.Column<int>(type: "integer", nullable: false),
                    codigo = table.Column<string>(type: "character varying", nullable: false),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tarifas_pkey", x => new { x.tipo, x.categoria_id, x.codigo });
                });

            migrationBuilder.CreateTable(
                name: "tarifas_catalogo",
                columns: table => new
                {
                    tarifa_catalogo_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    precio_base = table.Column<decimal>(type: "numeric(11,4)", precision: 11, scale: 4, nullable: true),
                    cargo_fijo = table.Column<decimal>(type: "numeric(11,4)", precision: 11, scale: 4, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true, defaultValueSql: "now()"),
                    fecha_modificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tarifas_catalogo_pkey", x => x.tarifa_catalogo_id);
                });

            migrationBuilder.CreateTable(
                name: "tarifas_contador",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'17', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    categoria_id = table.Column<int>(type: "integer", nullable: true),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    minimo = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true),
                    maximo = table.Column<decimal>(type: "numeric(12)", precision: 12, nullable: true),
                    cuota = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    valor_base = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    alquiler = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tarifas_contador_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "tipo_d",
                columns: table => new
                {
                    tipo_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    depto = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    tipo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    descripcion = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    concepto = table.Column<string>(type: "character varying", nullable: true),
                    depto_appmitrabajo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tipo_d_pkey", x => x.tipo_id);
                });

            migrationBuilder.CreateTable(
                name: "tipo_transaccion",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    codigo = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: true),
                    usuario_actualizacion = table.Column<string>(type: "character varying", nullable: true),
                    fecha_actualizacion = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tipo_transaccion_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "tipo_uso_servicio",
                columns: table => new
                {
                    tipo_uso_codigo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    descripcion = table.Column<string>(type: "text", nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("tipo_uso_codigo_pkey", x => x.tipo_uso_codigo);
                });

            migrationBuilder.CreateTable(
                name: "transaccion_abonado",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'3075446', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cliente_clave = table.Column<string>(type: "character varying", nullable: true),
                    recibo = table.Column<decimal>(type: "numeric", nullable: true),
                    tipotransaccion = table.Column<string>(type: "character varying", nullable: true),
                    docufuente = table.Column<decimal>(type: "numeric", nullable: true),
                    docufuente2 = table.Column<string>(type: "character varying", nullable: true),
                    fecha_docu = table.Column<DateOnly>(type: "date", nullable: true),
                    tipo_partida = table.Column<string>(type: "character varying", nullable: true),
                    banco = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    plazo = table.Column<decimal>(type: "numeric", nullable: true),
                    docuaplicar = table.Column<decimal>(type: "numeric", nullable: true),
                    trans_aplicar = table.Column<string>(type: "character varying", nullable: true),
                    debitos = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    creditos = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    saldo = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    tipo_servicio = table.Column<string>(type: "character varying", nullable: true),
                    aplicar_alca = table.Column<string>(type: "character varying", nullable: true),
                    periodo = table.Column<string>(type: "character varying", nullable: true),
                    tasa = table.Column<string>(type: "character varying", nullable: true),
                    estado = table.Column<string>(type: "character varying", nullable: true),
                    fecha_registro = table.Column<DateOnly>(type: "date", nullable: true),
                    ciclo = table.Column<string>(type: "character varying", nullable: true),
                    ruta = table.Column<string>(type: "character varying", nullable: true),
                    secuencia = table.Column<string>(type: "character varying", nullable: true),
                    tiene_med = table.Column<string>(type: "character varying", nullable: true),
                    codigoplan = table.Column<string>(type: "character varying", nullable: true),
                    motivo = table.Column<string>(type: "character varying", nullable: true),
                    usuario = table.Column<string>(type: "character varying", nullable: true),
                    saldo_detalle = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("transaccion_abonado_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "transaccion_presupuesto",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    trpr_descripcion = table.Column<string>(type: "character varying", nullable: true),
                    trpr_tipo_transaccion = table.Column<string>(type: "character varying", nullable: true),
                    trpr_presupuesto_origen = table.Column<string>(type: "character varying", nullable: true),
                    trpr_fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    trpr_monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    trp_saldo = table.Column<decimal>(type: "numeric", nullable: true),
                    trpr_ano = table.Column<int>(type: "integer", nullable: true),
                    trpr_destino = table.Column<string>(type: "character varying", nullable: true),
                    trpr_codigoproyecto = table.Column<string>(type: "character varying", nullable: true),
                    trpr_tipodestino = table.Column<string>(type: "character varying", nullable: true),
                    trpr_fondo_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("transaccion_presupuesto_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "usuarioapc",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    usuario = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    clave = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    ruta = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    estado = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("usuarioapc_pkey", x => x.ide);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_miorden",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    nombre = table.Column<string>(type: "character varying", nullable: false),
                    usuario = table.Column<string>(type: "character varying", nullable: false),
                    clave = table.Column<string>(type: "character varying", nullable: false),
                    tipo = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<BitArray>(type: "bit(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("usuarios_miorden_pkey", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_tipotransaccion_dtl",
                columns: table => new
                {
                    id_usertransacc_dtl = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cod_usertransacc_hdr = table.Column<int>(type: "integer", nullable: false),
                    cod_tipotransaccion = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("usuarios_tipotransaccion_dtl_pkey", x => x.id_usertransacc_dtl);
                });

            migrationBuilder.CreateTable(
                name: "usuarios_tipotransaccion_hdr",
                columns: table => new
                {
                    id_usertransacc = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    fecha_creacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    usuario_creo = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("usuarios_tipotransaccion_hdr_pkey", x => x.id_usertransacc);
                });

            migrationBuilder.CreateTable(
                name: "pagos_hdr",
                columns: table => new
                {
                    numfactura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cliente_clave = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    total = table.Column<decimal>(type: "numeric", nullable: false),
                    estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    caja_id = table.Column<int>(type: "integer", nullable: true),
                    banco = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    usuario = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_hdr", x => x.numfactura);
                    table.ForeignKey(
                        name: "fk_pagos_hdr_caja",
                        column: x => x.caja_id,
                        principalTable: "catalogo_cajas",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "solicitud_servicio",
                columns: table => new
                {
                    solicitud_servicio_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    cliente_identidad = table.Column<string>(type: "text", nullable: false),
                    categoria_servicio_id = table.Column<int>(type: "integer", nullable: false),
                    cliente_rtn = table.Column<string>(type: "text", nullable: true),
                    cliente_nombre = table.Column<string>(type: "text", nullable: false),
                    cliente_telefono = table.Column<string>(type: "text", nullable: true),
                    cliente_movil = table.Column<string>(type: "text", nullable: false),
                    cliente_email = table.Column<string>(type: "text", nullable: true),
                    cliente_direccion = table.Column<string>(type: "text", nullable: false),
                    cliente_color_casa = table.Column<string>(type: "text", nullable: true),
                    observacion = table.Column<string>(type: "text", nullable: true),
                    empresa_nombre = table.Column<string>(type: "text", nullable: true),
                    empresa_telefono = table.Column<string>(type: "text", nullable: true),
                    empresa_direccion = table.Column<string>(type: "text", nullable: true),
                    negocio_nombre = table.Column<string>(type: "text", nullable: true),
                    negocio_telefono = table.Column<string>(type: "text", nullable: true),
                    negocio_clave_catastral = table.Column<string>(type: "text", nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    asiginada = table.Column<bool>(type: "boolean", nullable: true),
                    fechanacimiento = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    clave_sure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("solicitud_servicio_pkey", x => x.solicitud_servicio_id);
                    table.ForeignKey(
                        name: "solicitud_servicio_categoria_servicio_id_fkey",
                        column: x => x.categoria_servicio_id,
                        principalTable: "categoria_servicio",
                        principalColumn: "categoria_servicio_id");
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_correlativo",
                schema: "public",
                columns: table => new
                {
                    config_correlativo_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    numerador = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    siguiente_numero = table.Column<int>(type: "integer", nullable: false),
                    formato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_correlativo", x => x.config_correlativo_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_correlativo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_estado_resultado",
                schema: "public",
                columns: table => new
                {
                    config_resultado_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_estado_resultado", x => x.config_resultado_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_estado_resultado_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_empresa_configuracion",
                schema: "public",
                columns: table => new
                {
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo_empresa = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    id_fiscal_siglas = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    id_fiscal_valor = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    tamano = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    capital = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fecha_constitucion = table.Column<DateOnly>(type: "date", nullable: true),
                    contacto = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    direccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    telefonos = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ciudad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    pais = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    email = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    pagina_web = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_empresa_configuracion", x => x.company_id);
                    table.ForeignKey(
                        name: "FK_con_empresa_configuracion_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rutas",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    codciclo = table.Column<int>(type: "integer", nullable: true),
                    codruta = table.Column<string>(type: "character varying", nullable: true),
                    descripcion = table.Column<string>(type: "character varying", nullable: true)
                },
                constraints: table =>
                {
                    table.ForeignKey(
                        name: "rutas_fk",
                        column: x => x.codciclo,
                        principalTable: "ciclos",
                        principalColumn: "ciclos_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_cuentas_utilidad",
                schema: "public",
                columns: table => new
                {
                    config_util_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    cuenta_util_acumulada_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_util_acumulada_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_util_ejercicio_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_util_ejercicio_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_acumulada_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_acumulada_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_ejercicio_historica = table.Column<long>(type: "bigint", nullable: true),
                    cuenta_perdida_ejercicio_inflacion = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_cuentas_utilidad", x => x.config_util_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta_~",
                        column: x => x.cuenta_perdida_acumulada_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~1",
                        column: x => x.cuenta_perdida_acumulada_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~2",
                        column: x => x.cuenta_perdida_ejercicio_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~3",
                        column: x => x.cuenta_perdida_ejercicio_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~4",
                        column: x => x.cuenta_util_acumulada_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~5",
                        column: x => x.cuenta_util_acumulada_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~6",
                        column: x => x.cuenta_util_ejercicio_historica,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_cuentas_utilidad_con_plan_cuentas_cuenta~7",
                        column: x => x.cuenta_util_ejercicio_inflacion,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_esf",
                schema: "public",
                columns: table => new
                {
                    config_esf_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    pasivo_corto_plazo_1 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_corto_plazo_2 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_largo_plazo_1 = table.Column<long>(type: "bigint", nullable: true),
                    pasivo_largo_plazo_2 = table.Column<long>(type: "bigint", nullable: true),
                    capital_aportado = table.Column<long>(type: "bigint", nullable: true),
                    resultados_acumulados = table.Column<long>(type: "bigint", nullable: true),
                    utilidad_perdida_ejercicio = table.Column<long>(type: "bigint", nullable: true),
                    sobrevaluaciones = table.Column<long>(type: "bigint", nullable: true),
                    mostrar_orden = table.Column<bool>(type: "boolean", nullable: false),
                    mostrar_percontra = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_esf", x => x.config_esf_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_capital_aportado",
                        column: x => x.capital_aportado,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_corto_plazo_1",
                        column: x => x.pasivo_corto_plazo_1,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_corto_plazo_2",
                        column: x => x.pasivo_corto_plazo_2,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_largo_plazo_1",
                        column: x => x.pasivo_largo_plazo_1,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_pasivo_largo_plazo_2",
                        column: x => x.pasivo_largo_plazo_2,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_resultados_acumulados",
                        column: x => x.resultados_acumulados,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_sobrevaluaciones",
                        column: x => x.sobrevaluaciones,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_configuracion_esf_con_plan_cuentas_utilidad_perdida_eje~",
                        column: x => x.utilidad_perdida_ejercicio,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_regla_integracion",
                schema: "public",
                columns: table => new
                {
                    regla_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    module = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_type_id = table.Column<long>(type: "bigint", nullable: false),
                    scenario_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    debit_account_id = table.Column<long>(type: "bigint", nullable: false),
                    credit_account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_regla_integracion", x => x.regla_id);
                    table.ForeignKey(
                        name: "FK_con_regla_integracion_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_regla_integracion_con_plan_cuentas_credit_account_id",
                        column: x => x.credit_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_regla_integracion_con_plan_cuentas_debit_account_id",
                        column: x => x.debit_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "con_plantilla_partida_dtl",
                schema: "public",
                columns: table => new
                {
                    template_line_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    template_id = table.Column<long>(type: "bigint", nullable: false),
                    line_number = table.Column<short>(type: "smallint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    debit_formula = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    credit_formula = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_plantilla_partida_dtl", x => x.template_line_id);
                    table.ForeignKey(
                        name: "FK_con_plantilla_partida_dtl_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_plantilla_partida_dtl_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_plantilla_partida_dtl_con_plantilla_partida_hdr_template_id",
                        column: x => x.template_id,
                        principalSchema: "public",
                        principalTable: "con_plantilla_partida_hdr",
                        principalColumn: "template_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_partida_hdr",
                schema: "public",
                columns: table => new
                {
                    poliza_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    journal_id = table.Column<long>(type: "bigint", nullable: true),
                    period_id = table.Column<long>(type: "bigint", nullable: true),
                    template_id = table.Column<long>(type: "bigint", nullable: true),
                    module = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    document_id = table.Column<long>(type: "bigint", nullable: true),
                    document_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    poliza_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    sequence_number = table.Column<long>(type: "bigint", nullable: true),
                    poliza_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_partida_hdr", x => x.poliza_id);
                    table.ForeignKey(
                        name: "FK_con_partida_hdr_con_diario_journal_id",
                        column: x => x.journal_id,
                        principalSchema: "public",
                        principalTable: "con_diario",
                        principalColumn: "journal_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_partida_hdr_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_partida_hdr_con_plantilla_partida_hdr_template_id",
                        column: x => x.template_id,
                        principalSchema: "public",
                        principalTable: "con_plantilla_partida_hdr",
                        principalColumn: "template_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "configuracion_cobros_adicionales_detalle",
                columns: table => new
                {
                    ide = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    configuracion_cobro_adicional_ide = table.Column<int>(type: "integer", nullable: false),
                    servicio_id = table.Column<int>(type: "integer", nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("configuracion_cobros_adicionales_detalle_pkey", x => x.ide);
                    table.ForeignKey(
                        name: "configuracion_cobros_fkey",
                        column: x => x.configuracion_cobro_adicional_ide,
                        principalTable: "configuracion_cobros_adicionales",
                        principalColumn: "ide");
                });

            migrationBuilder.CreateTable(
                name: "grupoestadodetalle",
                columns: table => new
                {
                    idgrupodetalle = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    nombre = table.Column<string>(type: "text", nullable: false),
                    idgrupo = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("grupoestadodetalle_pkey", x => x.idgrupodetalle);
                    table.ForeignKey(
                        name: "fk_idgrupo",
                        column: x => x.idgrupo,
                        principalTable: "grupoestado",
                        principalColumn: "idgrupo");
                });

            migrationBuilder.CreateTable(
                name: "identityroleclaim<string>",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_role_claims_asp_net_roles_identity_role_id",
                        column: x => x.role_id,
                        principalTable: "identityrole",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identityuserclaim<string>",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<string>(type: "text", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_claims_asp_net_users_identity_user_id",
                        column: x => x.user_id,
                        principalTable: "identityuser",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identityuserlogin<string>",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_user_logins_asp_net_users_identity_user_id",
                        column: x => x.user_id,
                        principalTable: "identityuser",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identityuserrole<string>",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    role_id = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_user_roles_asp_net_roles_identity_role_id",
                        column: x => x.role_id,
                        principalTable: "identityrole",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_roles_asp_net_users_identity_user_id",
                        column: x => x.user_id,
                        principalTable: "identityuser",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "identityusertoken<string>",
                columns: table => new
                {
                    user_id = table.Column<string>(type: "text", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_user_tokens_asp_net_users_identity_user_id",
                        column: x => x.user_id,
                        principalTable: "identityuser",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cliente_maestro",
                columns: table => new
                {
                    maestro_cliente_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'102789', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    maestro_cliente_clave = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    maestro_cliente_identidad = table.Column<string>(type: "text", nullable: false),
                    maestro_cliente_rtn = table.Column<string>(type: "text", nullable: true),
                    maestro_cliente_nombre = table.Column<string>(type: "text", nullable: false),
                    maestro_cliente_tercera_edad = table.Column<bool>(type: "boolean", nullable: true),
                    categoria_servicio_id = table.Column<int>(type: "integer", nullable: true),
                    barrio_codigo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    maestro_cliente_fecha_baja = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    maestro_cliente_indicativo_ruta = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    maestro_cliente_secuencia = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    tipo_uso_codigo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    ciclos_id = table.Column<int>(type: "integer", nullable: true),
                    cliente_fecha_nac = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    maestro_cliente_tiene_contrato = table.Column<bool>(type: "boolean", nullable: true),
                    maestro_cliente_tiene_convenio = table.Column<bool>(type: "boolean", nullable: true),
                    maestro_cliente_tiene_medidor = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    clave_sure = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    contador = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    letracodigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    descuento_tercera_edad = table.Column<double>(type: "double precision", nullable: true),
                    bloqueado_cobranza = table.Column<bool>(type: "boolean", nullable: true, defaultValue: false),
                    abogado = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cliente_maestro_pkey", x => x.maestro_cliente_id);
                    table.ForeignKey(
                        name: "barrio_codigo_cliente_maestro_fkey",
                        column: x => x.barrio_codigo,
                        principalTable: "barrio",
                        principalColumn: "barrio_codigo");
                    table.ForeignKey(
                        name: "categoria_servicio_id_cliente_maestro_fkey",
                        column: x => x.categoria_servicio_id,
                        principalTable: "categoria_servicio",
                        principalColumn: "categoria_servicio_id");
                    table.ForeignKey(
                        name: "ciclos_id_cliente_maestro_fkey",
                        column: x => x.ciclos_id,
                        principalTable: "ciclos",
                        principalColumn: "ciclos_id");
                    table.ForeignKey(
                        name: "tipo_uso_codigo_cliente_maestro_fkey",
                        column: x => x.tipo_uso_codigo,
                        principalTable: "tipo_uso_servicio",
                        principalColumn: "tipo_uso_codigo");
                });

            migrationBuilder.CreateTable(
                name: "pagos_dtl",
                columns: table => new
                {
                    numfactura = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    linea = table.Column<int>(type: "integer", nullable: false),
                    servicio = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    monto = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    montovalor = table.Column<decimal>(type: "numeric(18,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pagos_dtl", x => new { x.numfactura, x.linea });
                    table.ForeignKey(
                        name: "fk_pagos_dtl_hdr",
                        column: x => x.numfactura,
                        principalTable: "pagos_hdr",
                        principalColumn: "numfactura",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_sistema",
                schema: "public",
                columns: table => new
                {
                    config_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    fecha_inicio_ejercicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    fecha_fin_ejercicio = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    meses_calculados = table.Column<int>(type: "integer", nullable: false),
                    separador_codigo = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    formato_cuentas = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    formato_centros = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    symbol_saldo_acreedor = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    monto_maximo = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    frecuencia_depreciacion = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ultima_depreciacion = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    empresa_configuracioncompany_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_sistema", x => x.config_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_sistema_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_sistema_con_empresa_configuracion_empresa~",
                        column: x => x.empresa_configuracioncompany_id,
                        principalSchema: "public",
                        principalTable: "con_empresa_configuracion",
                        principalColumn: "company_id");
                });

            migrationBuilder.CreateTable(
                name: "con_partida_dtl",
                schema: "public",
                columns: table => new
                {
                    poliza_line_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    poliza_id = table.Column<long>(type: "bigint", nullable: false),
                    line_number = table.Column<short>(type: "smallint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric", nullable: true),
                    source_document = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_partida_dtl", x => x.poliza_line_id);
                    table.ForeignKey(
                        name: "FK_con_partida_dtl_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_partida_dtl_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_partida_dtl_con_partida_hdr_poliza_id",
                        column: x => x.poliza_id,
                        principalSchema: "public",
                        principalTable: "con_partida_hdr",
                        principalColumn: "poliza_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cliente_detalle",
                columns: table => new
                {
                    detalle_cliente_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'46839', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    maestro_cliente_id = table.Column<int>(type: "integer", nullable: false),
                    detalle_cliente_telefono = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    detalle_cliente_movil = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    detalle_cliente_email = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detalle_cliente_direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    detalle_cliente_color_casa = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    detalle_cliente_inquilino = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    maestro_medidor_id = table.Column<int>(type: "integer", nullable: true),
                    empresa_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    empresa_telefono = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    empresa_direccion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    negocio_nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    negocio_telefono = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    negocio_clave_catastral = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    clave = table.Column<string>(type: "character varying(32767)", maxLength: 32767, nullable: true),
                    observaciones = table.Column<string>(type: "character varying", nullable: true),
                    numero_contrato = table.Column<string>(type: "character varying", nullable: true),
                    descuento_valor = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cliente_detalle_pkey", x => x.detalle_cliente_id);
                    table.ForeignKey(
                        name: "maestro_cliente_id_cliente_detalle_fkey",
                        column: x => x.maestro_cliente_id,
                        principalTable: "cliente_maestro",
                        principalColumn: "maestro_cliente_id");
                    table.ForeignKey(
                        name: "maestro_medidor_id_cliente_detalle_fkey",
                        column: x => x.maestro_medidor_id,
                        principalTable: "maestro_medidor",
                        principalColumn: "maestro_medidor_id");
                });

            migrationBuilder.CreateTable(
                name: "cln_plan_pago_hdr",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    correlativo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    clienteid = table.Column<int>(type: "integer", nullable: true),
                    monto = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    representante = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    docrepresentante = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    numrepresentante = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    fechappago = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    comentario = table.Column<string>(type: "text", nullable: true),
                    porcprima = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    vprima = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    montofinanc = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    meses = table.Column<int>(type: "integer", nullable: true),
                    estadopago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cln_plan_pago_hdr_pkey", x => x.id);
                    table.ForeignKey(
                        name: "cln_plan_pago_hdr_clienteid_fkey",
                        column: x => x.clienteid,
                        principalTable: "cliente_maestro",
                        principalColumn: "maestro_cliente_id");
                });

            migrationBuilder.CreateTable(
                name: "configuracion_tasas",
                columns: table => new
                {
                    configuracion_tasas_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'38784', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    maestro_cliente_id = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    tarifa_catalogo_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("configuracion_tasas_id_pkey", x => x.configuracion_tasas_id);
                    table.ForeignKey(
                        name: "fk_configuracion_tasas_tarifas_catalogo",
                        column: x => x.tarifa_catalogo_id,
                        principalTable: "tarifas_catalogo",
                        principalColumn: "tarifa_catalogo_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "maestro_cliente_id_fkey",
                        column: x => x.maestro_cliente_id,
                        principalTable: "cliente_maestro",
                        principalColumn: "maestro_cliente_id");
                });

            migrationBuilder.CreateTable(
                name: "cln_plan_pago_dtl",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    idhdr = table.Column<int>(type: "integer", nullable: true),
                    valorcuota = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    fechacuota = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    mes = table.Column<int>(type: "integer", nullable: true),
                    estadopago = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("cln_plan_pago_dtl_pkey", x => x.id);
                    table.ForeignKey(
                        name: "cln_plan_pago_dtl_idhdr_fkey",
                        column: x => x.idhdr,
                        principalTable: "cln_plan_pago_hdr",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "configuracion_tasas_detalle",
                columns: table => new
                {
                    configuracion_tasas_detalle_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'149941', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    configuracion_tasas_id = table.Column<int>(type: "integer", nullable: false),
                    servicios_id = table.Column<int>(type: "integer", nullable: false),
                    configuracion_tasas_detalle_aplicaservicio = table.Column<bool>(type: "boolean", nullable: false),
                    configuracion_tasas_detalle_monto = table.Column<decimal>(type: "numeric(11,4)", precision: 11, scale: 4, nullable: false),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("configuracion_tasas_detalle_id_pkey", x => x.configuracion_tasas_detalle_id);
                    table.ForeignKey(
                        name: "configurcion_tasas_id_fkey",
                        column: x => x.configuracion_tasas_id,
                        principalTable: "configuracion_tasas",
                        principalColumn: "configuracion_tasas_id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_cfg_company_code",
                schema: "public",
                table: "cfg_company",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cliente_detalle_maestro_cliente_id",
                table: "cliente_detalle",
                column: "maestro_cliente_id");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_detalle_maestro_medidor_id",
                table: "cliente_detalle",
                column: "maestro_medidor_id");

            migrationBuilder.CreateIndex(
                name: "cliente_maestro_unique",
                table: "cliente_maestro",
                column: "maestro_cliente_clave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_cliente_maestro_barrio_codigo",
                table: "cliente_maestro",
                column: "barrio_codigo");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_maestro_categoria_servicio_id",
                table: "cliente_maestro",
                column: "categoria_servicio_id");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_maestro_ciclos_id",
                table: "cliente_maestro",
                column: "ciclos_id");

            migrationBuilder.CreateIndex(
                name: "IX_cliente_maestro_tipo_uso_codigo",
                table: "cliente_maestro",
                column: "tipo_uso_codigo");

            migrationBuilder.CreateIndex(
                name: "IX_cln_plan_pago_dtl_idhdr",
                table: "cln_plan_pago_dtl",
                column: "idhdr");

            migrationBuilder.CreateIndex(
                name: "IX_cln_plan_pago_hdr_clienteid",
                table: "cln_plan_pago_hdr",
                column: "clienteid");

            migrationBuilder.CreateIndex(
                name: "nombreunico",
                table: "cnt_rubros",
                column: "nombre",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_centro_costo_company_id_code",
                schema: "public",
                table: "con_centro_costo",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_correlativo_company_id_tipo",
                schema: "public",
                table: "con_configuracion_correlativo",
                columns: new[] { "company_id", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_company_id",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_perdida_acumulad~1",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_perdida_acumulada_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_perdida_acumulada~",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_perdida_acumulada_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_perdida_ejercici~1",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_perdida_ejercicio_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_perdida_ejercicio~",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_perdida_ejercicio_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_util_acumulada_hi~",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_util_acumulada_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_util_acumulada_in~",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_util_acumulada_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_util_ejercicio_hi~",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_util_ejercicio_historica");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_cuentas_utilidad_cuenta_util_ejercicio_in~",
                schema: "public",
                table: "con_configuracion_cuentas_utilidad",
                column: "cuenta_util_ejercicio_inflacion");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_capital_aportado",
                schema: "public",
                table: "con_configuracion_esf",
                column: "capital_aportado");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_company_id",
                schema: "public",
                table: "con_configuracion_esf",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_pasivo_corto_plazo_1",
                schema: "public",
                table: "con_configuracion_esf",
                column: "pasivo_corto_plazo_1");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_pasivo_corto_plazo_2",
                schema: "public",
                table: "con_configuracion_esf",
                column: "pasivo_corto_plazo_2");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_pasivo_largo_plazo_1",
                schema: "public",
                table: "con_configuracion_esf",
                column: "pasivo_largo_plazo_1");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_pasivo_largo_plazo_2",
                schema: "public",
                table: "con_configuracion_esf",
                column: "pasivo_largo_plazo_2");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_resultados_acumulados",
                schema: "public",
                table: "con_configuracion_esf",
                column: "resultados_acumulados");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_sobrevaluaciones",
                schema: "public",
                table: "con_configuracion_esf",
                column: "sobrevaluaciones");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_esf_utilidad_perdida_ejercicio",
                schema: "public",
                table: "con_configuracion_esf",
                column: "utilidad_perdida_ejercicio");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_estado_resultado_company_id_codigo",
                schema: "public",
                table: "con_configuracion_estado_resultado",
                columns: new[] { "company_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_company_id",
                schema: "public",
                table: "con_configuracion_sistema",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_sistema_empresa_configuracioncompany_id",
                schema: "public",
                table: "con_configuracion_sistema",
                column: "empresa_configuracioncompany_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_diario_company_id_code",
                schema: "public",
                table: "con_diario",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_periodo_contable_company_id_code",
                schema: "public",
                table: "con_periodo_contable",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_plan_cuentas_company_id_code",
                schema: "public",
                table: "con_plan_cuentas",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_plan_cuentas_parent_account_id",
                schema: "public",
                table: "con_plan_cuentas",
                column: "parent_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_plantilla_partida_hdr_company_id_name",
                schema: "public",
                table: "con_plantilla_partida_hdr",
                columns: new[] { "company_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_plantilla_partida_dtl_account_id",
                schema: "public",
                table: "con_plantilla_partida_dtl",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_plantilla_partida_dtl_company_id_template_id_line_numb~",
                schema: "public",
                table: "con_plantilla_partida_dtl",
                columns: new[] { "company_id", "template_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_plantilla_partida_dtl_cost_center_id",
                schema: "public",
                table: "con_plantilla_partida_dtl",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_plantilla_partida_dtl_template_id",
                schema: "public",
                table: "con_plantilla_partida_dtl",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_hdr_company_id_poliza_number",
                schema: "public",
                table: "con_partida_hdr",
                columns: new[] { "company_id", "poliza_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_hdr_journal_id",
                schema: "public",
                table: "con_partida_hdr",
                column: "journal_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_hdr_period_id",
                schema: "public",
                table: "con_partida_hdr",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_hdr_template_id",
                schema: "public",
                table: "con_partida_hdr",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_dtl_account_id",
                schema: "public",
                table: "con_partida_dtl",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_dtl_company_id_poliza_id_line_number",
                schema: "public",
                table: "con_partida_dtl",
                columns: new[] { "company_id", "poliza_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_dtl_cost_center_id",
                schema: "public",
                table: "con_partida_dtl",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_partida_dtl_poliza_id",
                schema: "public",
                table: "con_partida_dtl",
                column: "poliza_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_regla_integracion_company_id_module_scenario_code",
                schema: "public",
                table: "con_regla_integracion",
                columns: new[] { "company_id", "module", "scenario_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_regla_integracion_cost_center_id",
                schema: "public",
                table: "con_regla_integracion",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_regla_integracion_credit_account_id",
                schema: "public",
                table: "con_regla_integracion",
                column: "credit_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_regla_integracion_debit_account_id",
                schema: "public",
                table: "con_regla_integracion",
                column: "debit_account_id");

            migrationBuilder.CreateIndex(
                name: "fki_configuracion_cobros_fkey",
                table: "configuracion_cobros_adicionales_detalle",
                column: "configuracion_cobro_adicional_ide");

            migrationBuilder.CreateIndex(
                name: "ix_configuracion_tasas_cliente_tarifa",
                table: "configuracion_tasas",
                columns: new[] { "maestro_cliente_id", "tarifa_catalogo_id" });

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_tasas_tarifa_catalogo_id",
                table: "configuracion_tasas",
                column: "tarifa_catalogo_id");

            migrationBuilder.CreateIndex(
                name: "IX_configuracion_tasas_detalle_configuracion_tasas_id",
                table: "configuracion_tasas_detalle",
                column: "configuracion_tasas_id");

            migrationBuilder.CreateIndex(
                name: "IX_grupoestadodetalle_idgrupo",
                table: "grupoestadodetalle",
                column: "idgrupo");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "identityrole",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_role_claims_role_id",
                table: "identityroleclaim<string>",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "identityuser",
                column: "normalized_email");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "identityuser",
                column: "normalized_user_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_claims_user_id",
                table: "identityuserclaim<string>",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_logins_user_id",
                table: "identityuserlogin<string>",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_roles_role_id",
                table: "identityuserrole<string>",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_pagos_hdr_caja_id",
                table: "pagos_hdr",
                column: "caja_id");

            migrationBuilder.CreateIndex(
                name: "IX_rutas_codciclo",
                table: "rutas",
                column: "codciclo");

            migrationBuilder.CreateIndex(
                name: "IX_solicitud_servicio_categoria_servicio_id",
                table: "solicitud_servicio",
                column: "categoria_servicio_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "abogado");

            migrationBuilder.DropTable(
                name: "ajustes");

            migrationBuilder.DropTable(
                name: "ajustes_detalle");

            migrationBuilder.DropTable(
                name: "axl_accion_cobranza");

            migrationBuilder.DropTable(
                name: "axl_observacion_cobranza");

            migrationBuilder.DropTable(
                name: "bnc_bancos");

            migrationBuilder.DropTable(
                name: "bnc_cuentas");

            migrationBuilder.DropTable(
                name: "bnc_kardex_cuentas");

            migrationBuilder.DropTable(
                name: "bnc_tipotransacciones");

            migrationBuilder.DropTable(
                name: "cai");

            migrationBuilder.DropTable(
                name: "calendariopro");

            migrationBuilder.DropTable(
                name: "causa_refacturacion");

            migrationBuilder.DropTable(
                name: "cliente_detalle");

            migrationBuilder.DropTable(
                name: "cln_accion_cobranza");

            migrationBuilder.DropTable(
                name: "cln_plan_pago_dtl");

            migrationBuilder.DropTable(
                name: "cnt_balance");

            migrationBuilder.DropTable(
                name: "cnt_catalogo");

            migrationBuilder.DropTable(
                name: "cnt_centro_costos_grupo");

            migrationBuilder.DropTable(
                name: "cnt_centro_costos_subgrupo");

            migrationBuilder.DropTable(
                name: "cnt_centrocostos_dtl");

            migrationBuilder.DropTable(
                name: "cnt_centrocostos_hdr");

            migrationBuilder.DropTable(
                name: "cnt_centroscosto");

            migrationBuilder.DropTable(
                name: "cnt_grupo_cta");

            migrationBuilder.DropTable(
                name: "cnt_mayores");

            migrationBuilder.DropTable(
                name: "cnt_partidas_dtl");

            migrationBuilder.DropTable(
                name: "cnt_partidas_hdr");

            migrationBuilder.DropTable(
                name: "cnt_rubros");

            migrationBuilder.DropTable(
                name: "cnt_saldos");

            migrationBuilder.DropTable(
                name: "cnt_sub_cuenta");

            migrationBuilder.DropTable(
                name: "cnt_sub_grupo");

            migrationBuilder.DropTable(
                name: "cnt_tipopartida");

            migrationBuilder.DropTable(
                name: "con_configuracion_correlativo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_cuentas_utilidad",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_esf",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_estado_resultado",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_sistema",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_plantilla_partida_dtl",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_partida_dtl",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_regla_integracion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "concepto_cobro_adicional");

            migrationBuilder.DropTable(
                name: "condicion_lectura");

            migrationBuilder.DropTable(
                name: "configuracion_app_lectura_medidores");

            migrationBuilder.DropTable(
                name: "configuracion_cobros_adicionales_detalle");

            migrationBuilder.DropTable(
                name: "configuracion_tasas_detalle");

            migrationBuilder.DropTable(
                name: "coordenadas_empleado");

            migrationBuilder.DropTable(
                name: "factura");

            migrationBuilder.DropTable(
                name: "factura_detalle");

            migrationBuilder.DropTable(
                name: "grupo_estado_detalle");

            migrationBuilder.DropTable(
                name: "grupoestadodetalle");

            migrationBuilder.DropTable(
                name: "historialmes");

            migrationBuilder.DropTable(
                name: "historicomedicion");

            migrationBuilder.DropTable(
                name: "historicosinmedidor");

            migrationBuilder.DropTable(
                name: "identityroleclaim<string>");

            migrationBuilder.DropTable(
                name: "identityuserclaim<string>");

            migrationBuilder.DropTable(
                name: "identityuserlogin<string>");

            migrationBuilder.DropTable(
                name: "identityuserrole<string>");

            migrationBuilder.DropTable(
                name: "identityusertoken<string>");

            migrationBuilder.DropTable(
                name: "informativo");

            migrationBuilder.DropTable(
                name: "log_cliclo_descarga_app");

            migrationBuilder.DropTable(
                name: "materialesapp");

            migrationBuilder.DropTable(
                name: "miscelaneos_catalogo");

            migrationBuilder.DropTable(
                name: "ops_compromisos");

            migrationBuilder.DropTable(
                name: "orden_trabajo");

            migrationBuilder.DropTable(
                name: "orden_trabajo_adjunto");

            migrationBuilder.DropTable(
                name: "orden_trabajo_estado");

            migrationBuilder.DropTable(
                name: "ordent_mate");

            migrationBuilder.DropTable(
                name: "pagos_bancos");

            migrationBuilder.DropTable(
                name: "pagos_dtl");

            migrationBuilder.DropTable(
                name: "pagos_miscelaneos");

            migrationBuilder.DropTable(
                name: "pagovariostemp");

            migrationBuilder.DropTable(
                name: "portal_branding");

            migrationBuilder.DropTable(
                name: "presupuesto_fondos");

            migrationBuilder.DropTable(
                name: "presupuestos");

            migrationBuilder.DropTable(
                name: "proyectos");

            migrationBuilder.DropTable(
                name: "pruebainsert");

            migrationBuilder.DropTable(
                name: "prv_compromiso_dtl");

            migrationBuilder.DropTable(
                name: "prv_compromiso_hdr");

            migrationBuilder.DropTable(
                name: "prv_kardex");

            migrationBuilder.DropTable(
                name: "prv_proveedores");

            migrationBuilder.DropTable(
                name: "prv_tipoproveedor");

            migrationBuilder.DropTable(
                name: "prv_tipostransacc");

            migrationBuilder.DropTable(
                name: "recolectora");

            migrationBuilder.DropTable(
                name: "rutas");

            migrationBuilder.DropTable(
                name: "servicios");

            migrationBuilder.DropTable(
                name: "solicitud_servicio");

            migrationBuilder.DropTable(
                name: "tarifas");

            migrationBuilder.DropTable(
                name: "tarifas_contador");

            migrationBuilder.DropTable(
                name: "tipo_d");

            migrationBuilder.DropTable(
                name: "tipo_transaccion");

            migrationBuilder.DropTable(
                name: "transaccion_abonado");

            migrationBuilder.DropTable(
                name: "transaccion_presupuesto");

            migrationBuilder.DropTable(
                name: "usuarioapc");

            migrationBuilder.DropTable(
                name: "usuarios_miorden");

            migrationBuilder.DropTable(
                name: "usuarios_tipotransaccion_dtl");

            migrationBuilder.DropTable(
                name: "usuarios_tipotransaccion_hdr");

            migrationBuilder.DropTable(
                name: "maestro_medidor");

            migrationBuilder.DropTable(
                name: "cln_plan_pago_hdr");

            migrationBuilder.DropTable(
                name: "con_empresa_configuracion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_partida_hdr",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_centro_costo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_plan_cuentas",
                schema: "public");

            migrationBuilder.DropTable(
                name: "configuracion_cobros_adicionales");

            migrationBuilder.DropTable(
                name: "configuracion_tasas");

            migrationBuilder.DropTable(
                name: "grupoestado");

            migrationBuilder.DropTable(
                name: "identityrole");

            migrationBuilder.DropTable(
                name: "identityuser");

            migrationBuilder.DropTable(
                name: "pagos_hdr");

            migrationBuilder.DropTable(
                name: "cfg_company",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_diario",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_periodo_contable",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_plantilla_partida_hdr",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tarifas_catalogo");

            migrationBuilder.DropTable(
                name: "cliente_maestro");

            migrationBuilder.DropTable(
                name: "catalogo_cajas");

            migrationBuilder.DropTable(
                name: "barrio");

            migrationBuilder.DropTable(
                name: "categoria_servicio");

            migrationBuilder.DropTable(
                name: "ciclos");

            migrationBuilder.DropTable(
                name: "tipo_uso_servicio");
        }
    }
}



