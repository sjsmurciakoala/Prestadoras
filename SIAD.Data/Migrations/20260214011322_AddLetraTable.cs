using System;
using System.Collections;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SIAD.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLetraTable : Migration
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
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    logo_mime = table.Column<string>(type: "text", nullable: true)
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
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    legacy_key_cost = table.Column<int>(type: "integer", nullable: true),
                    legacy_type_trans = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    legacy_parent_code = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    allows_movement = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    start_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    end_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    legacy_notes = table.Column<string>(type: "text", nullable: true),
                    is_periodic = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    legacy_status = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_centro_costo", x => x.cost_center_id);
                    table.CheckConstraint("ck_con_centro_costo_legacy_type_trans", "legacy_type_trans >= 0 AND legacy_type_trans <= 5");
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
                    allows_budget = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allows_third = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_tax_base = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allows_cost_center = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allows_multi_currency = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    adjustment_account_id = table.Column<long>(type: "bigint", nullable: true),
                    correction_account_id = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_plan_cuentas", x => x.account_id);
                    table.UniqueConstraint("AK_con_plan_cuentas_code", x => x.code);
                    table.ForeignKey(
                        name: "FK_con_plan_cuentas_con_plan_cuentas_adjustment_account_id",
                        column: x => x.adjustment_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_plan_cuentas_con_plan_cuentas_correction_account_id",
                        column: x => x.correction_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
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
                name: "letras",
                columns: table => new
                {
                    letras = table.Column<string>(type: "character(1)", fixedLength: true, maxLength: 1, nullable: false),
                    num = table.Column<decimal>(type: "numeric(1,0)", nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("letra_pkey", x => x.letras);
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
                    estado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    banco = table.Column<string>(type: "text", nullable: true),
                    usuario = table.Column<string>(type: "text", nullable: true)
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
                    depto_appmitrabajo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
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
                name: "ban_banco",
                columns: table => new
                {
                    ban_banco_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    sucursal = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    nombre_sucursal = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    pais_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estado_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ciudad_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    municipio_id = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    zipcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion1 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    direccion2 = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    gerente = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    telefonos = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    fax = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    email = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    memo = table.Column<string>(type: "text", nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_banco_pkey", x => x.ban_banco_id);
                    table.ForeignKey(
                        name: "ban_banco_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ban_config",
                columns: table => new
                {
                    ban_config_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    max_cheque = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    dias_d1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dias_d2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dias_d3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    i_cheque_ne = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    i_c_egreso = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    prx_c_egreso = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    prx_deposito = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    prx_n_debito = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    prx_n_credito = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    p_deb_ban = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    meses_h = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    st_dta = table.Column<string>(type: "character varying(90)", maxLength: 90, nullable: true),
                    alertar_nd = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    m_ope_conc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    consolidado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    dir_contab = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    dir_dta_cont = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    cc_tipo = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cc_descrip = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    cc_ssw = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cc_server = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    cc_db = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    cc_user = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    cc_pwd = table.Column<string>(type: "character varying(70)", maxLength: 70, nullable: true),
                    cc_prefix = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    nro_cxb = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    a_ctas0 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    a_ctas1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    a_ctas2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    a_ctas3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    a_ctas4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    a_ctas5 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope5 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope6 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope7 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope8 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope9 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_ope10 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_aux1 = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cta_aux2 = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cta_aux3 = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cod_sucu = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_config_pkey", x => x.ban_config_id);
                    table.ForeignKey(
                        name: "ban_config_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ban_cta",
                columns: table => new
                {
                    ban_cta_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    iea = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ecg = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    grupo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    u_fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    u_dcto = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    u_banco = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    u_benef = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    u_coment1 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    u_coment2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    u_monto = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    es_banco = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tdc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    saldo_actual = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    tercero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cod_centro = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cta_cf = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_mov = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_ter = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_cc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_base = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_cta_pkey", x => x.ban_cta_id);
                    table.ForeignKey(
                        name: "ban_cta_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ban_moneda",
                columns: table => new
                {
                    ban_moneda_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    pais = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    factor = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    es_base = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_moneda_pkey", x => x.ban_moneda_id);
                    table.ForeignKey(
                        name: "ban_moneda_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_activo_tipo",
                schema: "public",
                columns: table => new
                {
                    type_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    depreciation_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    useful_life_years = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_activo_tipo", x => x.type_id);
                    table.ForeignKey(
                        name: "FK_con_activo_tipo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
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
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    logo = table.Column<byte[]>(type: "bytea", nullable: true),
                    logo_mime = table.Column<string>(type: "text", nullable: true)
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
                name: "con_tercero",
                schema: "public",
                columns: table => new
                {
                    third_party_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    tax_id = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_supplier = table.Column<bool>(type: "boolean", nullable: false),
                    is_customer = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_tercero", x => x.third_party_id);
                    table.UniqueConstraint("AK_con_tercero_company_id_third_party_id", x => new { x.company_id, x.third_party_id });
                    table.ForeignKey(
                        name: "FK_con_tercero_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_tipo_transaccion",
                schema: "public",
                columns: table => new
                {
                    type_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    category = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_automatic = table.Column<bool>(type: "boolean", nullable: false),
                    allows_cost_center = table.Column<bool>(type: "boolean", nullable: false),
                    allows_third_party = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    type_trans = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    type_oper = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    frequency = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    max_entries = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    allows_cash_flow = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    allows_account_limit = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_tipo_transaccion", x => x.type_id);
                    table.UniqueConstraint("AK_con_tipo_transaccion_company_id_type_id", x => new { x.company_id, x.type_id });
                    table.CheckConstraint("ck_con_tipo_transaccion_frequency", "frequency >= 0 AND frequency <= 2");
                    table.CheckConstraint("ck_con_tipo_transaccion_max_entries", "max_entries >= 0");
                    table.CheckConstraint("ck_con_tipo_transaccion_type_oper", "type_oper >= 0 AND type_oper <= 10");
                    table.CheckConstraint("ck_con_tipo_transaccion_type_trans", "type_trans >= 0 AND type_trans <= 5");
                    table.ForeignKey(
                        name: "FK_con_tipo_transaccion_cfg_company_company_id",
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
                    descripcion = table.Column<string>(type: "character varying", nullable: true),
                    estado = table.Column<bool>(type: "boolean", nullable: false),
                    usuariocreacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechacreacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    usuariomodificacion = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    fechamodificacion = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rutas", x => x.id);
                    table.ForeignKey(
                        name: "rutas_fk",
                        column: x => x.codciclo,
                        principalTable: "ciclos",
                        principalColumn: "ciclos_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_apertura_centro_costo",
                schema: "public",
                columns: table => new
                {
                    opening_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo_transaccion = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,9)", nullable: true, defaultValue: 1m),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_apertura_centro_costo", x => x.opening_id);
                    table.CheckConstraint("ck_con_apertura_centro_costo_tipo_transaccion", "tipo_transaccion >= 0 AND tipo_transaccion <= 5");
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_centro_costo_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "con_apertura_saldo",
                schema: "public",
                columns: table => new
                {
                    opening_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: true),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,9)", nullable: true, defaultValue: 1m),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_apertura_saldo", x => x.opening_id);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_apertura_saldo_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "con_balance_mensual",
                schema: "public",
                columns: table => new
                {
                    monthly_balance_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    account_id = table.Column<long>(type: "bigint", nullable: false),
                    cost_center_id = table.Column<long>(type: "bigint", nullable: true),
                    month_number = table.Column<short>(type: "smallint", nullable: false),
                    debit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    credit_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    transaction_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_balance_mensual", x => x.monthly_balance_id);
                    table.CheckConstraint("ck_con_balance_mensual_month", "month_number >= 1 AND month_number <= 13");
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_con_centro_costo_cost_center_id",
                        column: x => x.cost_center_id,
                        principalSchema: "public",
                        principalTable: "con_centro_costo",
                        principalColumn: "cost_center_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_balance_mensual_con_plan_cuentas_account_id",
                        column: x => x.account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_balance",
                schema: "public",
                columns: table => new
                {
                    config_balance_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_linea = table.Column<short>(type: "smallint", nullable: false),
                    clase = table.Column<byte>(type: "smallint", nullable: false),
                    codigo_cuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    descripcion_cuenta = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    descripcion_linea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    porcentaje_activo = table.Column<decimal>(type: "numeric(5,2)", nullable: true),
                    mostrar_en_reporte = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_balance", x => x.config_balance_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_balance_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_balance_con_periodo_contable_periodo_id",
                        column: x => x.periodo_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_configuracion_balance_con_plan_cuentas_codigo_cuenta",
                        column: x => x.codigo_cuenta,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "code",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_configuracion_linea_resultado",
                schema: "public",
                columns: table => new
                {
                    config_linea_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    numero_linea = table.Column<short>(type: "smallint", nullable: false),
                    tipo_linea = table.Column<byte>(type: "smallint", nullable: false),
                    codigo_cuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    descripcion_linea = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    columna_reporte = table.Column<byte>(type: "smallint", nullable: true),
                    mostrar_subtotal = table.Column<bool>(type: "boolean", nullable: false),
                    nivel_indentacion = table.Column<byte>(type: "smallint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_configuracion_linea_resultado", x => x.config_linea_id);
                    table.ForeignKey(
                        name: "FK_con_configuracion_linea_resultado_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_configuracion_linea_resultado_con_periodo_contable_peri~",
                        column: x => x.periodo_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_configuracion_linea_resultado_con_plan_cuentas_codigo_c~",
                        column: x => x.codigo_cuenta,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "code",
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
                name: "con_saldo_cuenta",
                schema: "public",
                columns: table => new
                {
                    saldo_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    periodo_id = table.Column<long>(type: "bigint", nullable: false),
                    codigo_cuenta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    mes = table.Column<byte>(type: "smallint", nullable: false),
                    tipo_transaccion = table.Column<byte>(type: "smallint", nullable: false),
                    debitos = table.Column<decimal>(type: "numeric(28,2)", nullable: false, defaultValue: 0m),
                    creditos = table.Column<decimal>(type: "numeric(28,2)", nullable: false, defaultValue: 0m),
                    cantidad_debitos = table.Column<int>(type: "integer", nullable: false),
                    cantidad_creditos = table.Column<int>(type: "integer", nullable: false),
                    presupuesto = table.Column<decimal>(type: "numeric(28,2)", nullable: false, defaultValue: 0m),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_saldo_cuenta", x => x.saldo_id);
                    table.ForeignKey(
                        name: "FK_con_saldo_cuenta_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_saldo_cuenta_con_periodo_contable_periodo_id",
                        column: x => x.periodo_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_saldo_cuenta_con_plan_cuentas_codigo_cuenta",
                        column: x => x.codigo_cuenta,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "code",
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
                name: "pagos_miscelaneos_dtl",
                columns: table => new
                {
                    recibo = table.Column<long>(type: "bigint", nullable: false),
                    linea = table.Column<int>(type: "integer", nullable: false),
                    concepto = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pagos_miscelaneos_dtl_pkey", x => new { x.recibo, x.linea });
                    table.ForeignKey(
                        name: "pagos_miscelaneos_dtl_recibo_fkey",
                        column: x => x.recibo,
                        principalTable: "pagos_miscelaneos",
                        principalColumn: "recibo",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "cliente_maestro",
                columns: table => new
                {
                    maestro_cliente_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:IdentitySequenceOptions", "'102789', '1', '', '', 'False', '1'")
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
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
                name: "ban_cuenta",
                columns: table => new
                {
                    banco_cuenta_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    banco_nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    branch_id = table.Column<long>(type: "bigint", nullable: true),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'CHEQUES'::character varying"),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    numero_cuenta = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    saldo_inicial = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    fecha_saldo = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'ACTIVE'::character varying"),
                    allow_reconciliation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    cont_account_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ban_banco_id = table.Column<long>(type: "bigint", nullable: true),
                    ban_cta_id = table.Column<long>(type: "bigint", nullable: true),
                    tdc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    saldo_actual = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    saldo_c1 = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    fecha_c1 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    saldo_c2 = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    fecha_c2 = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    proxima_conciliacion = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    inversion_cheque = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    idb = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    pdb = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    cta_debito = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    proximo_nddb = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_conc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    r_transf = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    meses_h = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    v_no_ch = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    v_no_dp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    v_no_nc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    v_no_nd = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    proximo_cheque = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    n_comp0 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_comp1 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_comp2 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_comp3 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_comp4 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_comp5 = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_cuenta_pkey", x => x.banco_cuenta_id);
                    table.ForeignKey(
                        name: "ban_cuenta_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ban_cuenta_banco",
                        column: x => x.ban_banco_id,
                        principalTable: "ban_banco",
                        principalColumn: "ban_banco_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_ban_cuenta_cta",
                        column: x => x.ban_cta_id,
                        principalTable: "ban_cta",
                        principalColumn: "ban_cta_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "con_activo_fijo",
                schema: "public",
                columns: table => new
                {
                    asset_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    asset_type_id = table.Column<long>(type: "bigint", nullable: false),
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    acquisition_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    in_service_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    acquisition_cost = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    salvage_value = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    useful_life_years = table.Column<short>(type: "smallint", nullable: false),
                    depreciation_method = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    accumulated_depreciation = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    current_value = table.Column<decimal>(type: "numeric(18,2)", nullable: true),
                    asset_account_id = table.Column<long>(type: "bigint", nullable: true),
                    depreciation_account_id = table.Column<long>(type: "bigint", nullable: true),
                    location = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_activo_fijo", x => x.asset_id);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_con_activo_tipo_asset_type_id",
                        column: x => x.asset_type_id,
                        principalSchema: "public",
                        principalTable: "con_activo_tipo",
                        principalColumn: "type_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_con_plan_cuentas_asset_account_id",
                        column: x => x.asset_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_con_activo_fijo_con_plan_cuentas_depreciation_account_id",
                        column: x => x.depreciation_account_id,
                        principalSchema: "public",
                        principalTable: "con_plan_cuentas",
                        principalColumn: "account_id",
                        onDelete: ReferentialAction.SetNull);
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
                    codigo_cuenta_util_acumulada_historica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_util_ejercicio_historica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_perdida_acumulada_historica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_perdida_ejercicio_historica = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_util_acumulada_inflacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_util_ejercicio_inflacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_perdida_acumulada_inflacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    codigo_cuenta_perdida_ejercicio_inflacion = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    mostrar_orden = table.Column<bool>(type: "boolean", nullable: false),
                    mostrar_percontra = table.Column<bool>(type: "boolean", nullable: false),
                    titulo_estado_resultados = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    titulo_balance_general = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion_activo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion_pasivo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion_capital = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion_pasivo_capital = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion_orden = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                name: "con_libro_iva",
                schema: "public",
                columns: table => new
                {
                    iva_register_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    transaction_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    document_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    third_party_id = table.Column<long>(type: "bigint", nullable: true),
                    taxable_base = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    exempt_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    tax_rate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    tax_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    iva_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_libro_iva", x => x.iva_register_id);
                    table.ForeignKey(
                        name: "FK_con_libro_iva_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_libro_iva_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_con_libro_iva_con_tercero_third_party_id",
                        column: x => x.third_party_id,
                        principalSchema: "public",
                        principalTable: "con_tercero",
                        principalColumn: "third_party_id",
                        onDelete: ReferentialAction.SetNull);
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
                    status = table.Column<short>(type: "smallint", nullable: false),
                    source_reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    type_id = table.Column<long>(type: "bigint", nullable: false),
                    posted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    posted_by = table.Column<long>(type: "bigint", nullable: true),
                    total_debit = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    total_credit = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
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
                    table.ForeignKey(
                        name: "FK_con_partida_hdr_con_tipo_transaccion_company_id_type_id",
                        columns: x => new { x.company_id, x.type_id },
                        principalSchema: "public",
                        principalTable: "con_tipo_transaccion",
                        principalColumns: new[] { "company_id", "type_id" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_tipo_transaccion_rule",
                schema: "public",
                columns: table => new
                {
                    rule_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    type_id = table.Column<long>(type: "bigint", nullable: false),
                    line_number = table.Column<int>(type: "integer", nullable: false),
                    account_code_from = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    account_code_to = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cost_center_code_from = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cost_center_code_to = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    third_party_code_from = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    third_party_code_to = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_tipo_transaccion_rule", x => x.rule_id);
                    table.CheckConstraint("ck_con_tipo_transaccion_rule_line", "line_number >= 1");
                    table.ForeignKey(
                        name: "FK_con_tipo_transaccion_rule_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_tipo_transaccion_rule_con_tipo_transaccion_type_id",
                        column: x => x.type_id,
                        principalSchema: "public",
                        principalTable: "con_tipo_transaccion",
                        principalColumn: "type_id",
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
                    company_id = table.Column<long>(type: "bigint", nullable: false),
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
                name: "ban_movimiento",
                columns: table => new
                {
                    movimiento_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    banco_cuenta_id = table.Column<long>(type: "bigint", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_movimiento = table.Column<DateOnly>(type: "date", nullable: false),
                    currency_code = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    exchange_rate = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false, defaultValueSql: "1"),
                    monto = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    monto_local = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    referencia = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    origen_modulo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    origen_documento_id = table.Column<long>(type: "bigint", nullable: true),
                    con_partida_hdr_id = table.Column<long>(type: "bigint", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValueSql: "'POSTED'::character varying"),
                    conciliado = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    fecha_conciliacion = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    aod = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false, defaultValueSql: "'N'::bpchar"),
                    no_ope = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    nro_comp = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    ope_rel = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    c_refer = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    cod_esta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cod_usua = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cod_sucu = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    cod_oper = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    fecha_lib = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    tip_ben = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cod_bene = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tdc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cdcd = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    documento = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    comentario1 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comentario2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comentario3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    memo = table.Column<string>(type: "text", nullable: true),
                    obcp = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    nro_ppal = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    origen_legacy = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    estado_legacy = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    fec_conc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    no_conc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    mto_db = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_cr = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    endosable = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    tipo_ope = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cta_idb = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    d_cta_idb = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    mto_idb = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    consolidado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    f_consolidado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    nro_egreso = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_debito = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    dcto_origen = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    mto_origen = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    bene_origen = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    monto1 = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    monto2 = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_deb = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    dcto_ori = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    bene_ori = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    mto_ori = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_movimiento_pkey", x => x.movimiento_id);
                    table.ForeignKey(
                        name: "ban_movimiento_banco_cuenta_id_fkey",
                        column: x => x.banco_cuenta_id,
                        principalTable: "ban_cuenta",
                        principalColumn: "banco_cuenta_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ban_movimiento_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "con_deprecacion",
                schema: "public",
                columns: table => new
                {
                    depreciation_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    asset_id = table.Column<long>(type: "bigint", nullable: false),
                    period_id = table.Column<long>(type: "bigint", nullable: false),
                    month_number = table.Column<short>(type: "smallint", nullable: false),
                    depreciation_amount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    accumulated_to_date = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    poliza_id = table.Column<long>(type: "bigint", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_con_deprecacion", x => x.depreciation_id);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_cfg_company_company_id",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_con_activo_fijo_asset_id",
                        column: x => x.asset_id,
                        principalSchema: "public",
                        principalTable: "con_activo_fijo",
                        principalColumn: "asset_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_con_periodo_contable_period_id",
                        column: x => x.period_id,
                        principalSchema: "public",
                        principalTable: "con_periodo_contable",
                        principalColumn: "period_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_con_deprecacion_con_partida_hdr_poliza_id",
                        column: x => x.poliza_id,
                        principalSchema: "public",
                        principalTable: "con_partida_hdr",
                        principalColumn: "poliza_id",
                        onDelete: ReferentialAction.SetNull);
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
                    source_document = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    third_party_id = table.Column<long>(type: "bigint", nullable: true)
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
                    table.ForeignKey(
                        name: "FK_con_partida_dtl_con_tercero_company_id_third_party_id",
                        columns: x => new { x.company_id, x.third_party_id },
                        principalSchema: "public",
                        principalTable: "con_tercero",
                        principalColumns: new[] { "company_id", "third_party_id" });
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

            migrationBuilder.CreateTable(
                name: "ban_movimiento_detalle",
                columns: table => new
                {
                    movimiento_detalle_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    movimiento_id = table.Column<long>(type: "bigint", nullable: false),
                    linea_num = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cod_cta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    es_transf = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    es_cuenta = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cod_usua = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cod_sucu = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: true),
                    cod_oper = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cod_esta = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cdcd = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    enc_ope = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    origen = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    dh = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    n_mo = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    base_tr = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    monto = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_db = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_cr = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    consolidado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    f_consolidado = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    si_centro = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    si_tercero = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cod_cen_cto = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    cod_tercero = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tercero = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    flujo_e = table.Column<decimal>(type: "numeric(28,3)", precision: 28, scale: 3, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_movimiento_detalle_pkey", x => x.movimiento_detalle_id);
                    table.ForeignKey(
                        name: "ban_movimiento_detalle_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ban_movimiento_detalle_movimiento_id_fkey",
                        column: x => x.movimiento_id,
                        principalTable: "ban_movimiento",
                        principalColumn: "movimiento_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ban_movimiento_transito",
                columns: table => new
                {
                    movimiento_transito_id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    company_id = table.Column<long>(type: "bigint", nullable: false),
                    banco_cuenta_id = table.Column<long>(type: "bigint", nullable: false),
                    movimiento_id = table.Column<long>(type: "bigint", nullable: true),
                    fecha = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    aod = table.Column<char>(type: "character(1)", maxLength: 1, nullable: false, defaultValueSql: "'N'::bpchar"),
                    no_ope = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    no_conc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    c_refer = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    cod_usua = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    fecha_lib = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    cod_bene = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    tdc = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    cdcd = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    descripcion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    documento = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: true),
                    comentario1 = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    comentario2 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    comentario3 = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    obcp = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false, defaultValueSql: "'N'::character varying"),
                    origen = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: true),
                    estado = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    monto = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_db = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    mto_cr = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    monto1 = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    monto2 = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    saldo = table.Column<decimal>(type: "numeric(28,4)", precision: 28, scale: 4, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false, defaultValueSql: "CURRENT_USER"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ban_movimiento_transito_pkey", x => x.movimiento_transito_id);
                    table.ForeignKey(
                        name: "ban_movimiento_transito_banco_cuenta_id_fkey",
                        column: x => x.banco_cuenta_id,
                        principalTable: "ban_cuenta",
                        principalColumn: "banco_cuenta_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ban_movimiento_transito_company_id_fkey",
                        column: x => x.company_id,
                        principalSchema: "public",
                        principalTable: "cfg_company",
                        principalColumn: "company_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "ban_movimiento_transito_movimiento_id_fkey",
                        column: x => x.movimiento_id,
                        principalTable: "ban_movimiento",
                        principalColumn: "movimiento_id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ban_banco_company_id_code_key",
                table: "ban_banco",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_banco_company",
                table: "ban_banco",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ban_config_company_id_key",
                table: "ban_config",
                column: "company_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_config_company",
                table: "ban_config",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ban_cta_company_id_codigo_key",
                table: "ban_cta",
                columns: new[] { "company_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_cta_company",
                table: "ban_cta",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ban_cuenta_company_id_code_key",
                table: "ban_cuenta",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ban_cuenta_company_id_numero_cuenta_key",
                table: "ban_cuenta",
                columns: new[] { "company_id", "numero_cuenta" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_cuenta_banco",
                table: "ban_cuenta",
                column: "ban_banco_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_cuenta_company",
                table: "ban_cuenta",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_cuenta_cta",
                table: "ban_cuenta",
                column: "ban_cta_id");

            migrationBuilder.CreateIndex(
                name: "ban_moneda_company_id_codigo_key",
                table: "ban_moneda",
                columns: new[] { "company_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_moneda_company",
                table: "ban_moneda",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_ban_movimiento_company_id",
                table: "ban_movimiento",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_movimiento_cuenta",
                table: "ban_movimiento",
                column: "banco_cuenta_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_movimiento_origen",
                table: "ban_movimiento",
                columns: new[] { "origen_modulo", "origen_documento_id" });

            migrationBuilder.CreateIndex(
                name: "ban_movimiento_detalle_movimiento_id_linea_num_key",
                table: "ban_movimiento_detalle",
                columns: new[] { "movimiento_id", "linea_num" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ban_mov_detalle_company",
                table: "ban_movimiento_detalle",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_mov_detalle_movimiento",
                table: "ban_movimiento_detalle",
                column: "movimiento_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_mov_transito_company",
                table: "ban_movimiento_transito",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_mov_transito_cuenta",
                table: "ban_movimiento_transito",
                column: "banco_cuenta_id");

            migrationBuilder.CreateIndex(
                name: "ix_ban_mov_transito_movimiento",
                table: "ban_movimiento_transito",
                column: "movimiento_id");

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
                name: "IX_con_activo_fijo_asset_account_id",
                schema: "public",
                table: "con_activo_fijo",
                column: "asset_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_asset_type_id",
                schema: "public",
                table: "con_activo_fijo",
                column: "asset_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_company_id_code",
                schema: "public",
                table: "con_activo_fijo",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_fijo_depreciation_account_id",
                schema: "public",
                table: "con_activo_fijo",
                column: "depreciation_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_activo_tipo_company_id_code",
                schema: "public",
                table: "con_activo_tipo",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_account_id",
                schema: "public",
                table: "con_apertura_centro_costo",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_company_id_period_id_account_id_c~",
                schema: "public",
                table: "con_apertura_centro_costo",
                columns: new[] { "company_id", "period_id", "account_id", "cost_center_id", "tipo_transaccion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_cost_center_id",
                schema: "public",
                table: "con_apertura_centro_costo",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_centro_costo_period_id",
                schema: "public",
                table: "con_apertura_centro_costo",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_account_id",
                schema: "public",
                table: "con_apertura_saldo",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_company_id_period_id_account_id_cost_cen~",
                schema: "public",
                table: "con_apertura_saldo",
                columns: new[] { "company_id", "period_id", "account_id", "cost_center_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_cost_center_id",
                schema: "public",
                table: "con_apertura_saldo",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_apertura_saldo_period_id",
                schema: "public",
                table: "con_apertura_saldo",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_account_id",
                schema: "public",
                table: "con_balance_mensual",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_company_id_period_id_account_id_cost_ce~",
                schema: "public",
                table: "con_balance_mensual",
                columns: new[] { "company_id", "period_id", "account_id", "cost_center_id", "month_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_cost_center_id",
                schema: "public",
                table: "con_balance_mensual",
                column: "cost_center_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_balance_mensual_period_id",
                schema: "public",
                table: "con_balance_mensual",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_centro_costo_company_id_code",
                schema: "public",
                table: "con_centro_costo",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_balance_codigo_cuenta",
                schema: "public",
                table: "con_configuracion_balance",
                column: "codigo_cuenta");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_balance_company_id_periodo_id_numero_linea",
                schema: "public",
                table: "con_configuracion_balance",
                columns: new[] { "company_id", "periodo_id", "numero_linea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_balance_periodo_id",
                schema: "public",
                table: "con_configuracion_balance",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_correlativo_company_id_tipo",
                schema: "public",
                table: "con_configuracion_correlativo",
                columns: new[] { "company_id", "tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_estado_resultado_company_id_codigo",
                schema: "public",
                table: "con_configuracion_estado_resultado",
                columns: new[] { "company_id", "codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_linea_resultado_codigo_cuenta",
                schema: "public",
                table: "con_configuracion_linea_resultado",
                column: "codigo_cuenta");

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_linea_resultado_company_id_periodo_id_num~",
                schema: "public",
                table: "con_configuracion_linea_resultado",
                columns: new[] { "company_id", "periodo_id", "numero_linea" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_configuracion_linea_resultado_periodo_id",
                schema: "public",
                table: "con_configuracion_linea_resultado",
                column: "periodo_id");

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
                name: "IX_con_deprecacion_asset_id_period_id_month_number",
                schema: "public",
                table: "con_deprecacion",
                columns: new[] { "asset_id", "period_id", "month_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_company_id",
                schema: "public",
                table: "con_deprecacion",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_period_id",
                schema: "public",
                table: "con_deprecacion",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_deprecacion_poliza_id",
                schema: "public",
                table: "con_deprecacion",
                column: "poliza_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_diario_company_id_code",
                schema: "public",
                table: "con_diario",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_company_id",
                schema: "public",
                table: "con_libro_iva",
                column: "company_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_period_id",
                schema: "public",
                table: "con_libro_iva",
                column: "period_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_third_party_id",
                schema: "public",
                table: "con_libro_iva",
                column: "third_party_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_libro_iva_transaction_date",
                schema: "public",
                table: "con_libro_iva",
                column: "transaction_date");

            migrationBuilder.CreateIndex(
                name: "IX_con_periodo_contable_company_id_code",
                schema: "public",
                table: "con_periodo_contable",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_plan_cuentas_adjustment_account_id",
                schema: "public",
                table: "con_plan_cuentas",
                column: "adjustment_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_plan_cuentas_company_id_code",
                schema: "public",
                table: "con_plan_cuentas",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_plan_cuentas_correction_account_id",
                schema: "public",
                table: "con_plan_cuentas",
                column: "correction_account_id");

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
                name: "IX_con_partida_hdr_company_id_type_id",
                schema: "public",
                table: "con_partida_hdr",
                columns: new[] { "company_id", "type_id" });

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
                name: "IX_con_partida_dtl_company_id_third_party_id",
                schema: "public",
                table: "con_partida_dtl",
                columns: new[] { "company_id", "third_party_id" });

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
                name: "IX_con_saldo_cuenta_codigo_cuenta",
                schema: "public",
                table: "con_saldo_cuenta",
                column: "codigo_cuenta");

            migrationBuilder.CreateIndex(
                name: "IX_con_saldo_cuenta_company_id_periodo_id_codigo_cuenta_mes_ti~",
                schema: "public",
                table: "con_saldo_cuenta",
                columns: new[] { "company_id", "periodo_id", "codigo_cuenta", "mes", "tipo_transaccion" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_saldo_cuenta_periodo_id",
                schema: "public",
                table: "con_saldo_cuenta",
                column: "periodo_id");

            migrationBuilder.CreateIndex(
                name: "IX_con_tercero_category",
                schema: "public",
                table: "con_tercero",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "IX_con_tercero_company_id_code",
                schema: "public",
                table: "con_tercero",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_tipo_transaccion_company_id_code",
                schema: "public",
                table: "con_tipo_transaccion",
                columns: new[] { "company_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_tipo_transaccion_company_id_is_default",
                schema: "public",
                table: "con_tipo_transaccion",
                columns: new[] { "company_id", "is_default" },
                unique: true,
                filter: "is_default");

            migrationBuilder.CreateIndex(
                name: "IX_con_tipo_transaccion_rule_company_id_type_id_line_number",
                schema: "public",
                table: "con_tipo_transaccion_rule",
                columns: new[] { "company_id", "type_id", "line_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_con_tipo_transaccion_rule_type_id",
                schema: "public",
                table: "con_tipo_transaccion_rule",
                column: "type_id");

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
                name: "ban_config");

            migrationBuilder.DropTable(
                name: "ban_moneda");

            migrationBuilder.DropTable(
                name: "ban_movimiento_detalle");

            migrationBuilder.DropTable(
                name: "ban_movimiento_transito");

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
                name: "con_apertura_centro_costo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_apertura_saldo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_balance_mensual",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_balance",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_correlativo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_estado_resultado",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_linea_resultado",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_configuracion_sistema",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_deprecacion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_libro_iva",
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
                name: "con_saldo_cuenta",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_tipo_transaccion_rule",
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
                name: "letras");

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
                name: "pagos_miscelaneos_dtl");

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
                name: "ban_movimiento");

            migrationBuilder.DropTable(
                name: "maestro_medidor");

            migrationBuilder.DropTable(
                name: "cln_plan_pago_hdr");

            migrationBuilder.DropTable(
                name: "con_empresa_configuracion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_activo_fijo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_partida_hdr",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_tercero",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_centro_costo",
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
                name: "pagos_miscelaneos");

            migrationBuilder.DropTable(
                name: "ban_cuenta");

            migrationBuilder.DropTable(
                name: "con_activo_tipo",
                schema: "public");

            migrationBuilder.DropTable(
                name: "con_plan_cuentas",
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
                name: "con_tipo_transaccion",
                schema: "public");

            migrationBuilder.DropTable(
                name: "tarifas_catalogo");

            migrationBuilder.DropTable(
                name: "cliente_maestro");

            migrationBuilder.DropTable(
                name: "catalogo_cajas");

            migrationBuilder.DropTable(
                name: "ban_banco");

            migrationBuilder.DropTable(
                name: "ban_cta");

            migrationBuilder.DropTable(
                name: "barrio");

            migrationBuilder.DropTable(
                name: "categoria_servicio");

            migrationBuilder.DropTable(
                name: "ciclos");

            migrationBuilder.DropTable(
                name: "tipo_uso_servicio");

            migrationBuilder.DropTable(
                name: "cfg_company",
                schema: "public");
        }
    }
}



