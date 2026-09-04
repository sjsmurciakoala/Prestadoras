-- =============================================================================
-- Aprobación por niveles — estructura (Fase 1 de 7)
-- Fecha: 2026-08-31
-- Diseño: docs/plans/2026-08-31-aprobacion-niveles-compras-plan.md
-- Regla DB Mirror: aplicar también en siad_v3_restore (localhost) antes que en el SRV
--
-- POR QUÉ
-- Hoy la orden de compra tiene UN SOLO escalón de aprobación: OrdenCompraService.AprobarAsync
-- pasa de Borrador (1) a Aprobada (2), sella `aprobado_por` y compromete presupuesto, todo en un
-- clic. Peor: el endpoint se autoriza como PermissionAction.Edit, así que QUIEN EDITA, APRUEBA.
-- No hay escalera por monto, ni rastro de quién firmó qué, ni forma de configurar aprobadores
-- sin desplegar código. Este script crea el modelo para una aprobación por niveles CONFIGURABLE.
--
-- QUÉ SE CREA
--   1) cfg_aprobacion_control       -> interruptor por empresa y documento. NACE APAGADO.
--   2) cfg_aprobacion_nivel         -> la escalera por monto (D1).
--   3) cfg_aprobacion_aprobador     -> quién firma cada nivel: usuario O rol (D3).
--   4) alm_orden_compra_aprobacion  -> flujo vivo: una fila por nivel exigido de cada orden.
--   5) apr_bitacora                 -> historial append-only para auditoría.
--   6) alm_orden_compra             -> CHECK de estado AMPLIADO con 7 (En aprobación).
--
-- DECISIONES APLICADAS (usuario 2026-08-31, ver §2 del diseño)
--   - D1  Escalera ACUMULATIVA: se exigen todos los niveles con monto_desde <= total del
--         documento. Por eso NO existe columna monto_hasta: con escalera sobra, y dos columnas
--         de rango generan huecos por redondeo y solapamientos que nada impide.
--   - D1b Dentro de un nivel firma CUALQUIERA de sus aprobadores.
--   - D2  El presupuesto se compromete en la PRIMERA firma (no en la última). Este script no
--         mueve presupuesto; solo habilita el estado 7 en el que esa reserva va a vivir.
--         Verificado: sp_pst_comprometer_documento NO consulta el estado de la orden, así que
--         comprometer con la orden en estado 7 no exige tocar el motor presupuestario.
--   - D3  El aprobador puede ser un USUARIO (tipo 1) o un ROL de Identity (tipo 2).
--   - D4  Devolver a borrador SIEMPRE borra las firmas (por eso el flujo es borrable y la
--         bitácora no: lo que se borra de la tabla 4 queda para siempre en la 5).
--   - D5  Nadie aprueba su propia orden, pero es CONFIGURABLE:
--         cfg_aprobacion_control.permite_autoaprobacion nace en FALSE (prohibido).
--
-- POR QUÉ NO HAY FK A LOS USUARIOS
-- El portal no tiene tabla funcional de usuarios: la identidad es ASP.NET Identity (AspNetUsers,
-- schema `identity`, DbContext distinto) y todos los documentos guardan al usuario como TEXTO
-- (alm_orden_compra.usuariocreacion / aprobado_por = User.Identity.Name). Una FK cruzaría schema
-- y contexto, y el filtro multitenant no aplica a Identity. Por eso `valor` y `usuario_firma` son
-- texto, normalizados a minúsculas cuando identifican a una persona.
--
-- Cambio ADITIVO y reversible: cinco tablas nuevas (vacías) y un CHECK que solo AMPLÍA los
-- valores admitidos. No altera ni borra una sola fila existente.
-- =============================================================================
BEGIN;

-- -----------------------------------------------------------------------------
-- 1) Interruptor del control por empresa y documento
--    Mismo patrón que cfg_presupuesto_control: modo numérico y semilla apagada, para que
--    aplicar el script no cambie el comportamiento de ninguna pantalla.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_aprobacion_control (
    company_id             BIGINT        NOT NULL,
    documento              VARCHAR(30)   NOT NULL,
    modo                   SMALLINT      NOT NULL DEFAULT 0,
    permite_autoaprobacion BOOLEAN       NOT NULL DEFAULT false,
    usuariocreacion        VARCHAR(100)  NULL,
    fechacreacion          TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion    VARCHAR(100)  NULL,
    fechamodificacion      TIMESTAMP     NULL,
    CONSTRAINT pk_cfg_aprobacion_control PRIMARY KEY (company_id, documento),
    CONSTRAINT ck_cfg_aprobacion_control_modo CHECK (modo IN (0, 1)),
    CONSTRAINT ck_cfg_aprobacion_control_doc  CHECK (documento IN
        ('COMPRAS_OC', 'COMPRAS_FACTURA', 'PROVEEDORES_PAGO', 'ALMACEN_REQUISICION'))
);

COMMENT ON TABLE  public.cfg_aprobacion_control IS 'Interruptor de la aprobación por niveles, por empresa y documento. Nace en 0 (apagado): aplicar el script no cambia el comportamiento de nadie.';
COMMENT ON COLUMN public.cfg_aprobacion_control.documento IS 'COMPRAS_OC · COMPRAS_FACTURA · PROVEEDORES_PAGO · ALMACEN_REQUISICION. La primera entrega solo implementa COMPRAS_OC.';
COMMENT ON COLUMN public.cfg_aprobacion_control.modo IS '0 Apagado (el documento se aprueba como hoy, de un clic) · 1 Encendido (exige la escalera de cfg_aprobacion_nivel).';
COMMENT ON COLUMN public.cfg_aprobacion_control.permite_autoaprobacion IS 'D5. FALSE (defecto): quien crea el documento NO puede firmar ningún nivel. TRUE: sí puede, si es aprobador elegible del nivel.';

-- -----------------------------------------------------------------------------
-- 2) La escalera: niveles exigidos según el monto del documento (D1)
--    Se exigen TODOS los niveles activos cuyo monto_desde <= total. Una orden de 75,000 con
--    umbrales 0 / 10,000.01 / 50,000.01 / 200,000.01 exige los niveles 1, 2 y 3.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_aprobacion_nivel (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    documento           VARCHAR(30)   NOT NULL,
    nivel               SMALLINT      NOT NULL,
    descripcion         VARCHAR(100)  NOT NULL,
    monto_desde         NUMERIC(14,2) NOT NULL DEFAULT 0,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    -- Un nivel irrepetible por empresa y documento.
    CONSTRAINT uq_cfg_aprobacion_nivel UNIQUE (company_id, documento, nivel),
    -- AK para la FK compuesta tenant-safe desde cfg_aprobacion_aprobador.
    CONSTRAINT uq_cfg_aprobacion_nivel_tenant UNIQUE (company_id, id),
    CONSTRAINT ck_cfg_aprobacion_nivel_nivel CHECK (nivel BETWEEN 1 AND 9),
    CONSTRAINT ck_cfg_aprobacion_nivel_monto CHECK (monto_desde >= 0),
    CONSTRAINT ck_cfg_aprobacion_nivel_doc   CHECK (documento IN
        ('COMPRAS_OC', 'COMPRAS_FACTURA', 'PROVEEDORES_PAGO', 'ALMACEN_REQUISICION'))
);
CREATE INDEX IF NOT EXISTS ix_cfg_aprobacion_nivel_company
    ON public.cfg_aprobacion_nivel (company_id, documento, activo);

COMMENT ON TABLE  public.cfg_aprobacion_nivel IS 'Escalera de aprobación por monto (D1, acumulativa). Se exigen TODOS los niveles activos con monto_desde <= total del documento.';
COMMENT ON COLUMN public.cfg_aprobacion_nivel.nivel IS 'Orden de firma, 1..9. El nivel N solo se habilita cuando el N-1 quedó aprobado.';
COMMENT ON COLUMN public.cfg_aprobacion_nivel.monto_desde IS 'Umbral inclusivo. NO existe monto_hasta: la escalera es acumulativa (D1). La regla "monto_desde crece con el nivel" la valida el servicio.';
COMMENT ON COLUMN public.cfg_aprobacion_nivel.descripcion IS 'Etiqueta legible del nivel (Aprobación Nivel 1, Gerencia). Se copia como snapshot al flujo del documento.';

-- -----------------------------------------------------------------------------
-- 3) Quién firma cada nivel: usuario o rol (D3)
--    Sin FK a los usuarios (ver cabecera). `valor` guarda el user_name (email) en MINÚSCULAS
--    cuando tipo = 1, o el nombre del rol de Identity cuando tipo = 2.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.cfg_aprobacion_aprobador (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    nivel_id            INTEGER       NOT NULL,
    tipo                SMALLINT      NOT NULL,
    valor               VARCHAR(256)  NOT NULL,
    activo              BOOLEAN       NOT NULL DEFAULT true,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    usuariomodificacion VARCHAR(100)  NULL,
    fechamodificacion   TIMESTAMP     NULL,
    -- Tenant-safe: el aprobador vive SIEMPRE en la empresa de su nivel.
    CONSTRAINT fk_cfg_aprobacion_aprobador_nivel
        FOREIGN KEY (company_id, nivel_id)
        REFERENCES public.cfg_aprobacion_nivel (company_id, id)
        ON DELETE CASCADE,
    CONSTRAINT ck_cfg_aprobacion_aprobador_tipo CHECK (tipo IN (1, 2)),
    -- El usuario se guarda normalizado: la regla D5 y la elegibilidad comparan sin distinguir
    -- mayúsculas, y un email capturado como Juan@x.com no debe crear un aprobador distinto.
    -- Los roles quedan fuera de la regla porque su nombre sí lleva mayúsculas (Super Administrador).
    CONSTRAINT ck_cfg_aprobacion_aprobador_valor CHECK (tipo <> 1 OR valor = lower(valor)),
    CONSTRAINT ck_cfg_aprobacion_aprobador_vacio CHECK (btrim(valor) <> '')
);
-- Unicidad insensible a mayúsculas: evita el mismo rol dos veces escrito distinto.
CREATE UNIQUE INDEX IF NOT EXISTS uq_cfg_aprobacion_aprobador
    ON public.cfg_aprobacion_aprobador (company_id, nivel_id, tipo, lower(valor));
CREATE INDEX IF NOT EXISTS ix_cfg_aprobacion_aprobador_nivel
    ON public.cfg_aprobacion_aprobador (company_id, nivel_id, activo);

COMMENT ON TABLE  public.cfg_aprobacion_aprobador IS 'Aprobadores de cada nivel (D3): usuario nominal o rol de Identity. Dentro de un nivel firma CUALQUIERA de ellos (D1b).';
COMMENT ON COLUMN public.cfg_aprobacion_aprobador.tipo IS '1 Usuario (user_name/email en minúsculas) · 2 Rol de Identity (AspNetRoles.Name).';
COMMENT ON COLUMN public.cfg_aprobacion_aprobador.valor IS 'Sin FK: la identidad vive en el schema identity, otro DbContext. La existencia del usuario/rol la valida la pantalla de configuración.';

-- -----------------------------------------------------------------------------
-- 4) Flujo vivo de la orden de compra: una fila por nivel exigido
--    Se materializa al enviar la orden a aprobación y se BORRA al devolverla a borrador (D4).
--    El rastro permanente no vive aquí, vive en apr_bitacora.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.alm_orden_compra_aprobacion (
    id                  SERIAL        PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    orden_compra_id     INTEGER       NOT NULL,
    nivel               SMALLINT      NOT NULL,
    descripcion         VARCHAR(100)  NOT NULL,
    estado              SMALLINT      NOT NULL DEFAULT 1,
    usuario_firma       VARCHAR(256)  NULL,
    fecha_firma         TIMESTAMP     NULL,
    comentario          VARCHAR(500)  NULL,
    total_documento     NUMERIC(14,2) NOT NULL DEFAULT 0,
    usuariocreacion     VARCHAR(100)  NULL,
    fechacreacion       TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc'),
    -- Tenant-safe: el renglón del flujo vive SIEMPRE en la empresa de su orden.
    CONSTRAINT fk_alm_oc_aprobacion_oc
        FOREIGN KEY (company_id, orden_compra_id)
        REFERENCES public.alm_orden_compra (company_id, id)
        ON DELETE CASCADE,
    CONSTRAINT uq_alm_oc_aprobacion UNIQUE (company_id, orden_compra_id, nivel),
    CONSTRAINT ck_alm_oc_aprobacion_estado CHECK (estado IN (1, 2, 3, 4)),
    -- Coherencia: un nivel firmado (aprobado o rechazado) tiene quién y cuándo; uno sin firmar, no.
    CONSTRAINT ck_alm_oc_aprobacion_firma CHECK (
        (estado IN (3, 4) AND usuario_firma IS NOT NULL AND fecha_firma IS NOT NULL)
     OR (estado IN (1, 2) AND usuario_firma IS NULL     AND fecha_firma IS NULL)
    )
);
CREATE INDEX IF NOT EXISTS ix_alm_oc_aprobacion_oc
    ON public.alm_orden_compra_aprobacion (company_id, orden_compra_id, nivel);
-- Índice de la bandeja "Mis aprobaciones": los niveles pendientes de firma.
CREATE INDEX IF NOT EXISTS ix_alm_oc_aprobacion_pendiente
    ON public.alm_orden_compra_aprobacion (company_id, estado)
    WHERE estado = 2;

COMMENT ON TABLE  public.alm_orden_compra_aprobacion IS 'Flujo de aprobación vivo de una orden de compra: una fila por nivel exigido. Se borra al devolver la orden a borrador (D4); el rastro permanente queda en apr_bitacora.';
COMMENT ON COLUMN public.alm_orden_compra_aprobacion.estado IS '1 Bloqueado (espera al nivel anterior) · 2 Pendiente (firmable ahora) · 3 Aprobado · 4 Rechazado.';
COMMENT ON COLUMN public.alm_orden_compra_aprobacion.descripcion IS 'Snapshot de cfg_aprobacion_nivel.descripcion al momento del envío: renombrar el nivel después no reescribe la historia.';
COMMENT ON COLUMN public.alm_orden_compra_aprobacion.total_documento IS 'Snapshot del total que se está aprobando. Deja evidencia de QUÉ MONTO se firmó, que es lo que pregunta una auditoría.';
COMMENT ON COLUMN public.alm_orden_compra_aprobacion.usuario_firma IS 'user_name (email) en minúsculas de quien firmó. Sin FK: la identidad vive en el schema identity.';

-- -----------------------------------------------------------------------------
-- 5) Bitácora de aprobación — APPEND-ONLY, transversal a todos los documentos
--    Nunca se actualiza ni se borra. Es la fuente de verdad de auditoría: sobrevive a la
--    devolución a borrador, que sí borra el flujo de la tabla 4.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.apr_bitacora (
    id                  BIGSERIAL     PRIMARY KEY,
    company_id          BIGINT        NOT NULL,
    documento           VARCHAR(30)   NOT NULL,
    documento_id        BIGINT        NOT NULL,
    documento_numero    VARCHAR(40)   NULL,
    nivel               SMALLINT      NULL,
    accion              VARCHAR(20)   NOT NULL,
    usuario             VARCHAR(256)  NOT NULL,
    fecha               TIMESTAMP     NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    comentario          VARCHAR(500)  NULL,
    total_documento     NUMERIC(14,2) NULL,
    CONSTRAINT ck_apr_bitacora_accion CHECK (accion IN
        ('ENVIADA', 'APROBADA', 'RECHAZADA', 'DEVUELTA', 'ANULADA', 'REINICIADA')),
    CONSTRAINT ck_apr_bitacora_doc CHECK (documento IN
        ('COMPRAS_OC', 'COMPRAS_FACTURA', 'PROVEEDORES_PAGO', 'ALMACEN_REQUISICION'))
);
CREATE INDEX IF NOT EXISTS ix_apr_bitacora_doc
    ON public.apr_bitacora (company_id, documento, documento_id, fecha);
CREATE INDEX IF NOT EXISTS ix_apr_bitacora_usuario
    ON public.apr_bitacora (company_id, usuario, fecha);

COMMENT ON TABLE  public.apr_bitacora IS 'Historial de aprobación APPEND-ONLY (nunca UPDATE ni DELETE). Fuente de verdad de auditoría: conserva las firmas que la devolución a borrador borra del flujo.';
COMMENT ON COLUMN public.apr_bitacora.nivel IS 'NULL en los eventos del documento (ENVIADA, DEVUELTA, ANULADA); con valor en los eventos de un nivel (APROBADA, RECHAZADA).';
COMMENT ON COLUMN public.apr_bitacora.accion IS 'ENVIADA · APROBADA · RECHAZADA · DEVUELTA (a borrador, borra firmas) · ANULADA · REINICIADA (reenvío tras devolución).';

-- -----------------------------------------------------------------------------
-- 6) Estado 7 "En aprobación" en la orden de compra
--    El CHECK vigente ya fue AMPLIADO por Database/2026-08-27_pst_compromiso_01_estructura.sql
--    con 5 (Rechazada) y 6 (Cancelada); por eso aquí se reemplaza y no se crea.
--    El conjunto nuevo es SUPERCONJUNTO del anterior: ninguna fila existente puede violarlo.
-- -----------------------------------------------------------------------------
ALTER TABLE public.alm_orden_compra DROP CONSTRAINT IF EXISTS ck_alm_orden_compra_estado;
ALTER TABLE public.alm_orden_compra ADD  CONSTRAINT ck_alm_orden_compra_estado
    CHECK (estado IN (1, 2, 3, 4, 5, 6, 7, 9));

COMMENT ON COLUMN public.alm_orden_compra.estado IS '1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · 5 Rechazada · 6 Cancelada · 7 En aprobación · 9 Anulada.';
COMMENT ON COLUMN public.alm_orden_compra.aprobado_por IS 'Usuario que dio la aprobación FINAL (el firmante del último nivel de la escalera). El detalle nivel por nivel vive en alm_orden_compra_aprobacion.';

-- -----------------------------------------------------------------------------
-- 7) Semilla: el control APAGADO para toda empresa y documento. Idempotente.
--    Igual que cfg_presupuesto_control: la fila existe para que la pantalla la pueda encender,
--    pero nace en 0 y con la autoaprobación prohibida (D5).
-- -----------------------------------------------------------------------------
INSERT INTO public.cfg_aprobacion_control (company_id, documento, modo, permite_autoaprobacion, usuariocreacion)
SELECT c.company_id, d.documento, 0, false, 'script-2026-08-31'
  FROM public.cfg_company c
 CROSS JOIN (VALUES ('COMPRAS_OC'), ('COMPRAS_FACTURA'), ('PROVEEDORES_PAGO'), ('ALMACEN_REQUISICION')) AS d(documento)
ON CONFLICT (company_id, documento) DO NOTHING;

COMMIT;

-- =============================================================================
-- VERIFICACIÓN (ejecutar después del COMMIT)
--
--   -- Las 5 tablas nuevas existen y están vacías (salvo la semilla del control):
--   SELECT 'cfg_aprobacion_control' t, count(*) FROM public.cfg_aprobacion_control
--   UNION ALL SELECT 'cfg_aprobacion_nivel',        count(*) FROM public.cfg_aprobacion_nivel
--   UNION ALL SELECT 'cfg_aprobacion_aprobador',    count(*) FROM public.cfg_aprobacion_aprobador
--   UNION ALL SELECT 'alm_orden_compra_aprobacion', count(*) FROM public.alm_orden_compra_aprobacion
--   UNION ALL SELECT 'apr_bitacora',                count(*) FROM public.apr_bitacora;
--
--   -- El control quedó APAGADO en todas las empresas (modo debe ser 0 en todas las filas):
--   SELECT modo, permite_autoaprobacion, count(*) FROM public.cfg_aprobacion_control GROUP BY 1, 2;
--
--   -- El CHECK admite el 7:
--   SELECT pg_get_constraintdef(oid) FROM pg_constraint WHERE conname = 'ck_alm_orden_compra_estado';
--
--   -- Ninguna orden quedó fuera del CHECK (debe devolver 0 filas):
--   SELECT id, numero, estado FROM public.alm_orden_compra
--    WHERE estado NOT IN (1, 2, 3, 4, 5, 6, 7, 9);
--
-- REVERSA (si hiciera falta deshacer: las tablas nacen vacías, no hay datos que perder)
--   DROP TABLE IF EXISTS public.apr_bitacora;
--   DROP TABLE IF EXISTS public.alm_orden_compra_aprobacion;
--   DROP TABLE IF EXISTS public.cfg_aprobacion_aprobador;
--   DROP TABLE IF EXISTS public.cfg_aprobacion_nivel;
--   DROP TABLE IF EXISTS public.cfg_aprobacion_control;
--   ALTER TABLE public.alm_orden_compra DROP CONSTRAINT IF EXISTS ck_alm_orden_compra_estado;
--   ALTER TABLE public.alm_orden_compra ADD  CONSTRAINT ck_alm_orden_compra_estado
--       CHECK (estado IN (1, 2, 3, 4, 5, 6, 9));
--   -- (la reversa del CHECK solo es válida si ninguna orden llegó a estado 7)
-- =============================================================================
