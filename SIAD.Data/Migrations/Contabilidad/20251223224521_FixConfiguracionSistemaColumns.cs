using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations.Contabilidad
{
    /// <inheritdoc />
    public partial class FixConfiguracionSistemaColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_util_acumulada_historica'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_acumulada_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_util_acumulada_historica TO codigo_cuenta_util_acumulada_historica';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_acumulada_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_util_acumulada_historica character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_acumulada_historica'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_util_acumulada_historica TYPE character varying(30) USING codigo_cuenta_util_acumulada_historica::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_util_ejercicio_historica'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_ejercicio_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_util_ejercicio_historica TO codigo_cuenta_util_ejercicio_historica';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_ejercicio_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_util_ejercicio_historica character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_ejercicio_historica'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_util_ejercicio_historica TYPE character varying(30) USING codigo_cuenta_util_ejercicio_historica::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_perdida_acumulada_historica'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_acumulada_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_perdida_acumulada_historica TO codigo_cuenta_perdida_acumulada_historica';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_acumulada_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_perdida_acumulada_historica character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_acumulada_historica'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_perdida_acumulada_historica TYPE character varying(30) USING codigo_cuenta_perdida_acumulada_historica::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_perdida_ejercicio_historica'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_ejercicio_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_perdida_ejercicio_historica TO codigo_cuenta_perdida_ejercicio_historica';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_ejercicio_historica'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_perdida_ejercicio_historica character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_ejercicio_historica'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_perdida_ejercicio_historica TYPE character varying(30) USING codigo_cuenta_perdida_ejercicio_historica::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_util_acumulada_inflacion'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_acumulada_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_util_acumulada_inflacion TO codigo_cuenta_util_acumulada_inflacion';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_acumulada_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_util_acumulada_inflacion character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_acumulada_inflacion'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_util_acumulada_inflacion TYPE character varying(30) USING codigo_cuenta_util_acumulada_inflacion::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_util_ejercicio_inflacion'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_ejercicio_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_util_ejercicio_inflacion TO codigo_cuenta_util_ejercicio_inflacion';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_ejercicio_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_util_ejercicio_inflacion character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_util_ejercicio_inflacion'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_util_ejercicio_inflacion TYPE character varying(30) USING codigo_cuenta_util_ejercicio_inflacion::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_perdida_acumulada_inflacion'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_acumulada_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_perdida_acumulada_inflacion TO codigo_cuenta_perdida_acumulada_inflacion';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_acumulada_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_perdida_acumulada_inflacion character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_acumulada_inflacion'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_perdida_acumulada_inflacion TYPE character varying(30) USING codigo_cuenta_perdida_acumulada_inflacion::text';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'cuenta_perdida_ejercicio_inflacion'
    ) AND NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_ejercicio_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema RENAME COLUMN cuenta_perdida_ejercicio_inflacion TO codigo_cuenta_perdida_ejercicio_inflacion';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_ejercicio_inflacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN codigo_cuenta_perdida_ejercicio_inflacion character varying(30)';
    END IF;

    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'codigo_cuenta_perdida_ejercicio_inflacion'
          AND (data_type <> 'character varying' OR character_maximum_length IS DISTINCT FROM 30)
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ALTER COLUMN codigo_cuenta_perdida_ejercicio_inflacion TYPE character varying(30) USING codigo_cuenta_perdida_ejercicio_inflacion::text';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'mostrar_orden'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN mostrar_orden boolean NOT NULL DEFAULT false';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'mostrar_percontra'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN mostrar_percontra boolean NOT NULL DEFAULT false';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'titulo_estado_resultados'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN titulo_estado_resultados character varying(100) NOT NULL DEFAULT ''Estado de Resultados''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'titulo_balance_general'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN titulo_balance_general character varying(100) NOT NULL DEFAULT ''Balance General''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'descripcion_activo'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN descripcion_activo character varying(100) NOT NULL DEFAULT ''ACTIVO''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'descripcion_pasivo'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN descripcion_pasivo character varying(100) NOT NULL DEFAULT ''PASIVO''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'descripcion_capital'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN descripcion_capital character varying(100) NOT NULL DEFAULT ''CAPITAL''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'descripcion_pasivo_capital'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN descripcion_pasivo_capital character varying(100) NOT NULL DEFAULT ''PASIVO y CAPITAL''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'descripcion_orden'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN descripcion_orden character varying(100) NOT NULL DEFAULT ''CUENTAS ORDEN''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'fecha_inicio_ejercicio'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN fecha_inicio_ejercicio timestamp with time zone';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'fecha_fin_ejercicio'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN fecha_fin_ejercicio timestamp with time zone';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'meses_calculados'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN meses_calculados integer NOT NULL DEFAULT 0';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'separador_codigo'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN separador_codigo character varying(1) NOT NULL DEFAULT ''-''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'formato_cuentas'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN formato_cuentas character varying(50) NOT NULL DEFAULT ''###-###-##''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'formato_centros'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN formato_centros character varying(50) NOT NULL DEFAULT ''###-##''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'symbol_saldo_acreedor'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN symbol_saldo_acreedor character varying(5) NOT NULL DEFAULT ''CR''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'monto_maximo'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN monto_maximo numeric(18,2)';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'frecuencia_depreciacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN frecuencia_depreciacion character varying(20) NOT NULL DEFAULT ''Mensual''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'ultima_depreciacion'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN ultima_depreciacion timestamp with time zone';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'created_at'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN created_at timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'created_by'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN created_by character varying(100) NOT NULL DEFAULT ''migration''';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'updated_at'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN updated_at timestamp with time zone';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'updated_by'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN updated_by character varying(100)';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'con_configuracion_sistema'
          AND column_name = 'empresa_configuracioncompany_id'
    ) THEN
        EXECUTE 'ALTER TABLE public.con_configuracion_sistema ADD COLUMN empresa_configuracioncompany_id bigint';
    END IF;
END $$;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "minimo",
                table: "tarifas_contador",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "maximo",
                table: "tarifas_contador",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "recibo",
                table: "pagos_bancos",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "sel",
                table: "historicomedicion",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "revision",
                table: "historicomedicion",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "numeroord",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "mes",
                table: "historicomedicion",
                type: "numeric(2,0)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "lect_ant",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "lect_act",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "lec_prom",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "consumoant",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "consumo",
                table: "historicomedicion",
                type: "numeric(12,0)",
                precision: 12,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(12)",
                oldPrecision: 12,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "ano",
                table: "historicomedicion",
                type: "numeric(4,0)",
                precision: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4)",
                oldPrecision: 4);

            migrationBuilder.AlterColumn<decimal>(
                name: "sep2",
                table: "historialmes",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "'0'::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "'0'::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "sep",
                table: "historialmes",
                type: "numeric(1,0)",
                precision: 1,
                nullable: true,
                defaultValueSql: "NULL::numeric",
                oldClrType: typeof(decimal),
                oldType: "numeric(1)",
                oldPrecision: 1,
                oldNullable: true,
                oldDefaultValueSql: "NULL::numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "mes",
                table: "historialmes",
                type: "numeric(2,0)",
                precision: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(2)",
                oldPrecision: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "ano",
                table: "historialmes",
                type: "numeric(4,0)",
                precision: 4,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(4)",
                oldPrecision: 4);
        }
    }
}
