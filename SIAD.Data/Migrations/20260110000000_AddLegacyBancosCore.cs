using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SIAD.Data.Migrations
{
    [DbContext(typeof(global::SIAD.Data.SiadDbContext))]
    [Migration("20260110000000_AddLegacyBancosCore")]
    public partial class AddLegacyBancosCore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            const string upSql = @"
CREATE TABLE IF NOT EXISTS public.ban_cuenta
(
    banco_cuenta_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code               varchar(30)    NOT NULL,
    nombre             varchar(150)   NOT NULL,
    banco_nombre       varchar(150),
    branch_id          bigint,
    tipo               varchar(20)    NOT NULL DEFAULT 'CHEQUES',
    currency_code      char(3)        NOT NULL,
    numero_cuenta      varchar(50)    NOT NULL,
    saldo_inicial      numeric(18,2)  NOT NULL DEFAULT 0,
    fecha_saldo        date,
    estado             varchar(20)    NOT NULL DEFAULT 'ACTIVE',
    allow_reconciliation boolean      NOT NULL DEFAULT true,
    cont_account_id    bigint,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100),
    UNIQUE (company_id, code),
    UNIQUE (company_id, numero_cuenta)
);

CREATE INDEX IF NOT EXISTS ix_ban_cuenta_company ON public.ban_cuenta (company_id);

CREATE TABLE IF NOT EXISTS public.ban_movimiento
(
    movimiento_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id         bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    banco_cuenta_id    bigint         NOT NULL REFERENCES public.ban_cuenta(banco_cuenta_id) ON DELETE CASCADE,
    tipo               varchar(20)    NOT NULL,
    fecha_movimiento   date           NOT NULL,
    currency_code      char(3)        NOT NULL,
    exchange_rate      numeric(18,6)  NOT NULL DEFAULT 1,
    monto              numeric(18,2)  NOT NULL,
    monto_local        numeric(18,2)  NOT NULL DEFAULT 0,
    descripcion        varchar(300),
    referencia         varchar(100),
    origen_modulo      varchar(30),
    origen_documento_id bigint,
    con_partida_hdr_id      bigint,
    estado             varchar(20)    NOT NULL DEFAULT 'POSTED',
    conciliado         boolean        NOT NULL DEFAULT false,
    fecha_conciliacion date,
    created_at         timestamptz    NOT NULL DEFAULT now(),
    created_by         varchar(100)   NOT NULL DEFAULT current_user,
    updated_at         timestamptz,
    updated_by         varchar(100)
);

CREATE INDEX IF NOT EXISTS ix_ban_movimiento_cuenta ON public.ban_movimiento (banco_cuenta_id);
CREATE INDEX IF NOT EXISTS ix_ban_movimiento_origen ON public.ban_movimiento (origen_modulo, origen_documento_id);

CREATE TABLE IF NOT EXISTS public.ban_config
(
    ban_config_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id       bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    max_cheque       numeric(28,4)  NOT NULL DEFAULT 0,
    dias_d1          int            NOT NULL DEFAULT 0,
    dias_d2          int            NOT NULL DEFAULT 0,
    dias_d3          int            NOT NULL DEFAULT 0,
    i_cheque_ne      int            NOT NULL DEFAULT 0,
    i_c_egreso       int            NOT NULL DEFAULT 0,
    prx_c_egreso     int            NOT NULL DEFAULT 0,
    prx_deposito     int            NOT NULL DEFAULT 0,
    prx_n_debito     int            NOT NULL DEFAULT 0,
    prx_n_credito    int            NOT NULL DEFAULT 0,
    p_deb_ban        numeric(28,4)  NOT NULL DEFAULT 0,
    meses_h          int            NOT NULL DEFAULT 0,
    st_dta           varchar(90),
    alertar_nd       int            NOT NULL DEFAULT 0,
    m_ope_conc       int            NOT NULL DEFAULT 0,
    consolidado      boolean        NOT NULL DEFAULT false,
    cuenta_mayor     varchar(30),
    dir_contab       varchar(70),
    dir_dta_cont     varchar(70),
    cc_tipo          int            NOT NULL DEFAULT 0,
    cc_descrip       varchar(40),
    cc_ssw           int            NOT NULL DEFAULT 0,
    cc_server        varchar(70),
    cc_db            varchar(70),
    cc_user          varchar(70),
    cc_pwd           varchar(70),
    cc_prefix        int            NOT NULL DEFAULT 0,
    nro_cxb          int            NOT NULL DEFAULT 0,
    a_ctas0          int            NOT NULL DEFAULT 0,
    a_ctas1          int            NOT NULL DEFAULT 0,
    a_ctas2          int            NOT NULL DEFAULT 0,
    a_ctas3          int            NOT NULL DEFAULT 0,
    a_ctas4          int            NOT NULL DEFAULT 0,
    a_ctas5          int            NOT NULL DEFAULT 0,
    n_ope1           int            NOT NULL DEFAULT 0,
    n_ope2           int            NOT NULL DEFAULT 0,
    n_ope3           int            NOT NULL DEFAULT 0,
    n_ope4           int            NOT NULL DEFAULT 0,
    n_ope5           int            NOT NULL DEFAULT 0,
    n_ope6           int            NOT NULL DEFAULT 0,
    n_ope7           int            NOT NULL DEFAULT 0,
    n_ope8           int            NOT NULL DEFAULT 0,
    n_ope9           int            NOT NULL DEFAULT 0,
    n_ope10          int            NOT NULL DEFAULT 0,
    cta_aux1         varchar(30),
    cta_aux2         varchar(30),
    cta_aux3         varchar(30),
    cod_sucu         varchar(5),
    created_at       timestamptz    NOT NULL DEFAULT now(),
    created_by       varchar(100)   NOT NULL DEFAULT current_user,
    updated_at       timestamptz,
    updated_by       varchar(100),
    UNIQUE (company_id)
);

CREATE INDEX IF NOT EXISTS ix_ban_config_company ON public.ban_config (company_id);

CREATE TABLE IF NOT EXISTS public.ban_moneda
(
    ban_moneda_id   bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id      bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    codigo          varchar(5)     NOT NULL,
    descripcion     varchar(60)    NOT NULL,
    pais            varchar(25),
    factor          numeric(28,4)  NOT NULL DEFAULT 0,
    es_base         boolean        NOT NULL DEFAULT false,
    created_at      timestamptz    NOT NULL DEFAULT now(),
    created_by      varchar(100)   NOT NULL DEFAULT current_user,
    updated_at      timestamptz,
    updated_by      varchar(100),
    UNIQUE (company_id, codigo)
);

CREATE INDEX IF NOT EXISTS ix_ban_moneda_company ON public.ban_moneda (company_id);

CREATE TABLE IF NOT EXISTS public.ban_banco
(
    ban_banco_id    bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id      bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    code            varchar(30)    NOT NULL,
    nombre          varchar(60)    NOT NULL,
    sucursal        varchar(50),
    nombre_sucursal varchar(40),
    pais_id         int            NOT NULL DEFAULT 0,
    estado_id       int            NOT NULL DEFAULT 0,
    ciudad_id       int            NOT NULL DEFAULT 0,
    municipio_id    int            NOT NULL DEFAULT 0,
    zipcode         varchar(20),
    direccion1      varchar(40),
    direccion2      varchar(40),
    gerente         varchar(30),
    telefonos       varchar(25),
    fax             varchar(25),
    email           varchar(25),
    memo            text,
    activo          boolean        NOT NULL DEFAULT true,
    created_at      timestamptz    NOT NULL DEFAULT now(),
    created_by      varchar(100)   NOT NULL DEFAULT current_user,
    updated_at      timestamptz,
    updated_by      varchar(100),
    UNIQUE (company_id, code)
);

CREATE INDEX IF NOT EXISTS ix_ban_banco_company ON public.ban_banco (company_id);

CREATE TABLE IF NOT EXISTS public.ban_cta
(
    ban_cta_id      bigint         GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id      bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    codigo          varchar(30)    NOT NULL,
    descripcion     varchar(60)    NOT NULL,
    iea             int            NOT NULL DEFAULT 0,
    ecg             int            NOT NULL DEFAULT 0,
    grupo           varchar(30),
    u_fecha         timestamptz,
    u_dcto          varchar(25),
    u_banco         varchar(30),
    u_benef         varchar(50),
    u_coment1       varchar(50),
    u_coment2       varchar(50),
    u_monto         numeric(28,4)  NOT NULL DEFAULT 0,
    es_banco        boolean        NOT NULL DEFAULT false,
    tdc             int            NOT NULL DEFAULT 0,
    saldo_actual    numeric(28,4)  NOT NULL DEFAULT 0,
    tercero         varchar(30),
    cod_centro      varchar(30),
    cta_cf          int            NOT NULL DEFAULT 0,
    cta_mov         int            NOT NULL DEFAULT 0,
    cta_ter         int            NOT NULL DEFAULT 0,
    cta_cc          int            NOT NULL DEFAULT 0,
    cta_base        int            NOT NULL DEFAULT 0,
    created_at      timestamptz    NOT NULL DEFAULT now(),
    created_by      varchar(100)   NOT NULL DEFAULT current_user,
    updated_at      timestamptz,
    updated_by      varchar(100),
    UNIQUE (company_id, codigo)
);

CREATE INDEX IF NOT EXISTS ix_ban_cta_company ON public.ban_cta (company_id);

CREATE TABLE IF NOT EXISTS public.ban_movimiento_detalle
(
    movimiento_detalle_id bigint       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id      bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    movimiento_id   bigint         NOT NULL REFERENCES public.ban_movimiento(movimiento_id) ON DELETE CASCADE,
    linea_num       int            NOT NULL DEFAULT 0,
    cod_cta         varchar(30),
    es_transf       int            NOT NULL DEFAULT 0,
    es_cuenta       int            NOT NULL DEFAULT 0,
    cod_usua        varchar(30),
    cod_sucu        varchar(5),
    cod_oper        varchar(10),
    cod_esta        varchar(30),
    cdcd            int            NOT NULL DEFAULT 0,
    enc_ope         int            NOT NULL DEFAULT 0,
    fecha           timestamptz    NOT NULL,
    descripcion     varchar(60),
    origen          varchar(35),
    estado          int            NOT NULL DEFAULT 0,
    dh              int            NOT NULL DEFAULT 0,
    n_mo            int            NOT NULL DEFAULT 0,
    base_tr         numeric(28,4)  NOT NULL DEFAULT 0,
    monto           numeric(28,4)  NOT NULL DEFAULT 0,
    mto_db          numeric(28,4)  NOT NULL DEFAULT 0,
    mto_cr          numeric(28,4)  NOT NULL DEFAULT 0,
    consolidado     int            NOT NULL DEFAULT 0,
    f_consolidado   timestamptz,
    si_centro       int            NOT NULL DEFAULT 0,
    si_tercero      int            NOT NULL DEFAULT 0,
    cod_cen_cto     varchar(30),
    cod_tercero     varchar(30),
    tercero         varchar(50),
    flujo_e         numeric(28,3),
    created_at      timestamptz    NOT NULL DEFAULT now(),
    created_by      varchar(100)   NOT NULL DEFAULT current_user,
    updated_at      timestamptz,
    updated_by      varchar(100),
    UNIQUE (movimiento_id, linea_num)
);

CREATE INDEX IF NOT EXISTS ix_ban_mov_detalle_company ON public.ban_movimiento_detalle (company_id);
CREATE INDEX IF NOT EXISTS ix_ban_mov_detalle_movimiento ON public.ban_movimiento_detalle (movimiento_id);

CREATE TABLE IF NOT EXISTS public.ban_movimiento_transito
(
    movimiento_transito_id bigint      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id      bigint         NOT NULL REFERENCES public.cfg_company(company_id) ON DELETE CASCADE,
    banco_cuenta_id bigint         NOT NULL REFERENCES public.ban_cuenta(banco_cuenta_id) ON DELETE CASCADE,
    movimiento_id   bigint         REFERENCES public.ban_movimiento(movimiento_id) ON DELETE SET NULL,
    fecha           timestamptz    NOT NULL,
    aod             char(1)        NOT NULL DEFAULT 'N',
    no_ope          int            NOT NULL DEFAULT 0,
    no_conc         int            NOT NULL DEFAULT 0,
    c_refer         varchar(35),
    cod_usua        varchar(30),
    fecha_lib       timestamptz    NOT NULL,
    cod_bene        varchar(30),
    tdc             int            NOT NULL DEFAULT 0,
    cdcd            int            NOT NULL DEFAULT 0,
    descripcion     varchar(50),
    documento       varchar(25),
    comentario1     varchar(60),
    comentario2     varchar(50),
    comentario3     varchar(50),
    obcp            varchar(1)     NOT NULL DEFAULT 'N',
    origen          varchar(35),
    estado          int            NOT NULL DEFAULT 0,
    monto           numeric(28,4)  NOT NULL DEFAULT 0,
    mto_db          numeric(28,4)  NOT NULL DEFAULT 0,
    mto_cr          numeric(28,4)  NOT NULL DEFAULT 0,
    monto1          numeric(28,4)  NOT NULL DEFAULT 0,
    monto2          numeric(28,4)  NOT NULL DEFAULT 0,
    saldo           numeric(28,4)  NOT NULL DEFAULT 0,
    created_at      timestamptz    NOT NULL DEFAULT now(),
    created_by      varchar(100)   NOT NULL DEFAULT current_user,
    updated_at      timestamptz,
    updated_by      varchar(100)
);

CREATE INDEX IF NOT EXISTS ix_ban_mov_transito_company ON public.ban_movimiento_transito (company_id);
CREATE INDEX IF NOT EXISTS ix_ban_mov_transito_cuenta ON public.ban_movimiento_transito (banco_cuenta_id);
CREATE INDEX IF NOT EXISTS ix_ban_mov_transito_movimiento ON public.ban_movimiento_transito (movimiento_id);

ALTER TABLE public.ban_cuenta
    ADD COLUMN IF NOT EXISTS ban_banco_id bigint,
    ADD COLUMN IF NOT EXISTS ban_cta_id bigint,
    ADD COLUMN IF NOT EXISTS tdc int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS saldo_actual numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS saldo_c1 numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS fecha_c1 timestamptz,
    ADD COLUMN IF NOT EXISTS saldo_c2 numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS fecha_c2 timestamptz,
    ADD COLUMN IF NOT EXISTS proxima_conciliacion int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS inversion_cheque int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS idb int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS pdb numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cta_debito varchar(30),
    ADD COLUMN IF NOT EXISTS proximo_nddb int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cta_conc varchar(255) NOT NULL DEFAULT '',
    ADD COLUMN IF NOT EXISTS r_transf int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS meses_h int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS v_no_ch int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS v_no_dp int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS v_no_nc int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS v_no_nd int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS proximo_cheque numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS n_comp0 int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS n_comp1 int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS n_comp2 int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS n_comp3 int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS n_comp4 int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS n_comp5 int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS activo boolean NOT NULL DEFAULT true;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ban_cuenta_banco'
          AND conrelid = 'public.ban_cuenta'::regclass
    ) THEN
        ALTER TABLE public.ban_cuenta
            ADD CONSTRAINT fk_ban_cuenta_banco FOREIGN KEY (ban_banco_id)
                REFERENCES public.ban_banco(ban_banco_id)
                ON DELETE SET NULL;
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ban_cuenta_cta'
          AND conrelid = 'public.ban_cuenta'::regclass
    ) THEN
        ALTER TABLE public.ban_cuenta
            ADD CONSTRAINT fk_ban_cuenta_cta FOREIGN KEY (ban_cta_id)
                REFERENCES public.ban_cta(ban_cta_id)
                ON DELETE SET NULL;
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS ix_ban_cuenta_banco ON public.ban_cuenta (ban_banco_id);
CREATE INDEX IF NOT EXISTS ix_ban_cuenta_cta ON public.ban_cuenta (ban_cta_id);

ALTER TABLE public.ban_movimiento
    ADD COLUMN IF NOT EXISTS aod char(1) NOT NULL DEFAULT 'N',
    ADD COLUMN IF NOT EXISTS no_ope int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS nro_comp int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS ope_rel int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS c_refer varchar(35),
    ADD COLUMN IF NOT EXISTS cod_esta varchar(30),
    ADD COLUMN IF NOT EXISTS cod_usua varchar(30),
    ADD COLUMN IF NOT EXISTS cod_sucu varchar(5),
    ADD COLUMN IF NOT EXISTS cod_oper varchar(10),
    ADD COLUMN IF NOT EXISTS fecha_lib timestamptz,
    ADD COLUMN IF NOT EXISTS tip_ben int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cod_bene varchar(30),
    ADD COLUMN IF NOT EXISTS tdc int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cdcd int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS documento varchar(25),
    ADD COLUMN IF NOT EXISTS comentario1 varchar(50),
    ADD COLUMN IF NOT EXISTS comentario2 varchar(50),
    ADD COLUMN IF NOT EXISTS comentario3 varchar(50),
    ADD COLUMN IF NOT EXISTS memo text,
    ADD COLUMN IF NOT EXISTS obcp varchar(1),
    ADD COLUMN IF NOT EXISTS nro_ppal int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS origen_legacy varchar(35),
    ADD COLUMN IF NOT EXISTS estado_legacy int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS fec_conc timestamptz,
    ADD COLUMN IF NOT EXISTS no_conc int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS mto_db numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS mto_cr numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS endosable int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS tipo_ope int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS cta_idb varchar(30),
    ADD COLUMN IF NOT EXISTS d_cta_idb varchar(60),
    ADD COLUMN IF NOT EXISTS mto_idb numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS consolidado int NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS f_consolidado timestamptz,
    ADD COLUMN IF NOT EXISTS nro_egreso numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS mto_debito numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS dcto_origen varchar(25),
    ADD COLUMN IF NOT EXISTS mto_origen numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS bene_origen varchar(40),
    ADD COLUMN IF NOT EXISTS monto1 numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS monto2 numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS mto_deb numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS dcto_ori varchar(20),
    ADD COLUMN IF NOT EXISTS bene_ori varchar(50),
    ADD COLUMN IF NOT EXISTS mto_ori numeric(28,4) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS saldo numeric(28,4) NOT NULL DEFAULT 0;
";

            migrationBuilder.Sql(upSql);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            const string downSql = @"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ban_cuenta_banco'
          AND conrelid = 'public.ban_cuenta'::regclass
    ) THEN
        ALTER TABLE public.ban_cuenta
            DROP CONSTRAINT fk_ban_cuenta_banco;
    END IF;
END $$;

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM pg_constraint
        WHERE conname = 'fk_ban_cuenta_cta'
          AND conrelid = 'public.ban_cuenta'::regclass
    ) THEN
        ALTER TABLE public.ban_cuenta
            DROP CONSTRAINT fk_ban_cuenta_cta;
    END IF;
END $$;

ALTER TABLE public.ban_cuenta
    DROP COLUMN IF EXISTS ban_banco_id,
    DROP COLUMN IF EXISTS ban_cta_id,
    DROP COLUMN IF EXISTS tdc,
    DROP COLUMN IF EXISTS saldo_actual,
    DROP COLUMN IF EXISTS saldo_c1,
    DROP COLUMN IF EXISTS fecha_c1,
    DROP COLUMN IF EXISTS saldo_c2,
    DROP COLUMN IF EXISTS fecha_c2,
    DROP COLUMN IF EXISTS proxima_conciliacion,
    DROP COLUMN IF EXISTS inversion_cheque,
    DROP COLUMN IF EXISTS idb,
    DROP COLUMN IF EXISTS pdb,
    DROP COLUMN IF EXISTS cta_debito,
    DROP COLUMN IF EXISTS proximo_nddb,
    DROP COLUMN IF EXISTS cta_conc,
    DROP COLUMN IF EXISTS r_transf,
    DROP COLUMN IF EXISTS meses_h,
    DROP COLUMN IF EXISTS v_no_ch,
    DROP COLUMN IF EXISTS v_no_dp,
    DROP COLUMN IF EXISTS v_no_nc,
    DROP COLUMN IF EXISTS v_no_nd,
    DROP COLUMN IF EXISTS proximo_cheque,
    DROP COLUMN IF EXISTS n_comp0,
    DROP COLUMN IF EXISTS n_comp1,
    DROP COLUMN IF EXISTS n_comp2,
    DROP COLUMN IF EXISTS n_comp3,
    DROP COLUMN IF EXISTS n_comp4,
    DROP COLUMN IF EXISTS n_comp5,
    DROP COLUMN IF EXISTS activo;

ALTER TABLE public.ban_movimiento
    DROP COLUMN IF EXISTS aod,
    DROP COLUMN IF EXISTS no_ope,
    DROP COLUMN IF EXISTS nro_comp,
    DROP COLUMN IF EXISTS ope_rel,
    DROP COLUMN IF EXISTS c_refer,
    DROP COLUMN IF EXISTS cod_esta,
    DROP COLUMN IF EXISTS cod_usua,
    DROP COLUMN IF EXISTS cod_sucu,
    DROP COLUMN IF EXISTS cod_oper,
    DROP COLUMN IF EXISTS fecha_lib,
    DROP COLUMN IF EXISTS tip_ben,
    DROP COLUMN IF EXISTS cod_bene,
    DROP COLUMN IF EXISTS tdc,
    DROP COLUMN IF EXISTS cdcd,
    DROP COLUMN IF EXISTS documento,
    DROP COLUMN IF EXISTS comentario1,
    DROP COLUMN IF EXISTS comentario2,
    DROP COLUMN IF EXISTS comentario3,
    DROP COLUMN IF EXISTS memo,
    DROP COLUMN IF EXISTS obcp,
    DROP COLUMN IF EXISTS nro_ppal,
    DROP COLUMN IF EXISTS origen_legacy,
    DROP COLUMN IF EXISTS estado_legacy,
    DROP COLUMN IF EXISTS fec_conc,
    DROP COLUMN IF EXISTS no_conc,
    DROP COLUMN IF EXISTS mto_db,
    DROP COLUMN IF EXISTS mto_cr,
    DROP COLUMN IF EXISTS endosable,
    DROP COLUMN IF EXISTS tipo_ope,
    DROP COLUMN IF EXISTS cta_idb,
    DROP COLUMN IF EXISTS d_cta_idb,
    DROP COLUMN IF EXISTS mto_idb,
    DROP COLUMN IF EXISTS consolidado,
    DROP COLUMN IF EXISTS f_consolidado,
    DROP COLUMN IF EXISTS nro_egreso,
    DROP COLUMN IF EXISTS mto_debito,
    DROP COLUMN IF EXISTS dcto_origen,
    DROP COLUMN IF EXISTS mto_origen,
    DROP COLUMN IF EXISTS bene_origen,
    DROP COLUMN IF EXISTS monto1,
    DROP COLUMN IF EXISTS monto2,
    DROP COLUMN IF EXISTS mto_deb,
    DROP COLUMN IF EXISTS dcto_ori,
    DROP COLUMN IF EXISTS bene_ori,
    DROP COLUMN IF EXISTS mto_ori,
    DROP COLUMN IF EXISTS saldo;

DROP TABLE IF EXISTS public.ban_movimiento_transito;
DROP TABLE IF EXISTS public.ban_movimiento_detalle;
DROP TABLE IF EXISTS public.ban_cta;
DROP TABLE IF EXISTS public.ban_banco;
DROP TABLE IF EXISTS public.ban_moneda;
DROP TABLE IF EXISTS public.ban_config;
";

            migrationBuilder.Sql(downSql);
        }
    }
}

