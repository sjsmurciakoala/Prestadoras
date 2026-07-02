-- Presupuesto: actividades (historico), vista de gestion y solicitudes.
-- Item 1 + Item 2 unificados en la tabla public.pst_actividad_presupuesto
-- y la vista public.vw_pst_gestion_actividad_presupuesto.

CREATE TABLE IF NOT EXISTS public.pst_actividad_presupuesto (
    actividad_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id BIGINT NOT NULL,
    id_presupuesto VARCHAR(10) NOT NULL,
    tipo_actividad VARCHAR(20) NOT NULL, -- AMPLIACION | TRASLADO
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE', -- PENDIENTE|APROBADA|RECHAZADA|ANULADA|APLICADA
    fecha_actividad TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),

    cuenta_origen_code VARCHAR(20),
    cuenta_destino_code VARCHAR(20) NOT NULL,
    monto NUMERIC(18, 4) NOT NULL,

    motivo VARCHAR(500),
    referencia VARCHAR(100),

    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100) NOT NULL DEFAULT CURRENT_USER,
    approved_at TIMESTAMP WITHOUT TIME ZONE,
    approved_by VARCHAR(100),
    applied_at TIMESTAMP WITHOUT TIME ZONE,
    applied_by VARCHAR(100),

    CONSTRAINT ck_pst_act_tipo CHECK (tipo_actividad IN ('AMPLIACION', 'TRASLADO')),
    CONSTRAINT ck_pst_act_estado CHECK (estado IN ('PENDIENTE', 'APROBADA', 'RECHAZADA', 'ANULADA', 'APLICADA')),
    CONSTRAINT ck_pst_act_monto CHECK (monto > 0),
    CONSTRAINT ck_pst_act_regla_tipo CHECK (
        (tipo_actividad = 'AMPLIACION' AND cuenta_origen_code IS NULL AND cuenta_destino_code IS NOT NULL)
        OR
        (tipo_actividad = 'TRASLADO' AND cuenta_origen_code IS NOT NULL AND cuenta_destino_code IS NOT NULL AND cuenta_origen_code <> cuenta_destino_code)
    ),

    CONSTRAINT fk_pst_act_company FOREIGN KEY (company_id)
        REFERENCES public.cfg_company (company_id) ON DELETE RESTRICT,

    CONSTRAINT fk_pst_act_hdr FOREIGN KEY (id_presupuesto)
        REFERENCES public.pst_config_presupuesto_hdr (id_presupuesto)
        ON UPDATE CASCADE ON DELETE RESTRICT,

    CONSTRAINT fk_pst_act_dtl_origen FOREIGN KEY (id_presupuesto, cuenta_origen_code)
        REFERENCES public.pst_config_presupuesto_dtl (id_presupuesto, con_cuenta_code)
        ON UPDATE CASCADE ON DELETE RESTRICT,

    CONSTRAINT fk_pst_act_dtl_destino FOREIGN KEY (id_presupuesto, cuenta_destino_code)
        REFERENCES public.pst_config_presupuesto_dtl (id_presupuesto, con_cuenta_code)
        ON UPDATE CASCADE ON DELETE RESTRICT
);

CREATE INDEX IF NOT EXISTS ix_pst_act_presupuesto_fecha
    ON public.pst_actividad_presupuesto (company_id, id_presupuesto, fecha_actividad DESC);

CREATE INDEX IF NOT EXISTS ix_pst_act_estado
    ON public.pst_actividad_presupuesto (company_id, estado);

CREATE INDEX IF NOT EXISTS ix_pst_act_cuentas
    ON public.pst_actividad_presupuesto (id_presupuesto, cuenta_origen_code, cuenta_destino_code);

COMMENT ON TABLE public.pst_actividad_presupuesto IS
'Historico de actividades de presupuesto para ampliaciones y traslados entre cuentas.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.actividad_id IS 'Identificador unico de la actividad.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.company_id IS 'Empresa propietaria del registro.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.id_presupuesto IS 'Id del presupuesto asociado.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.tipo_actividad IS 'Tipo de movimiento: AMPLIACION o TRASLADO.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.estado IS 'Estado del flujo de actividad.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.fecha_actividad IS 'Fecha y hora de registro de la actividad.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.cuenta_origen_code IS 'Cuenta origen en traslados (null en ampliaciones).';
COMMENT ON COLUMN public.pst_actividad_presupuesto.cuenta_destino_code IS 'Cuenta destino afectada por la actividad.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.monto IS 'Monto de la actividad presupuestaria.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.motivo IS 'Motivo o justificacion del movimiento.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.referencia IS 'Referencia externa o documento soporte.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.created_at IS 'Fecha y hora de creacion del registro.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.created_by IS 'Usuario creador.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.approved_at IS 'Fecha y hora de aprobacion.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.approved_by IS 'Usuario aprobador.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.applied_at IS 'Fecha y hora de aplicacion al presupuesto.';
COMMENT ON COLUMN public.pst_actividad_presupuesto.applied_by IS 'Usuario que aplico el movimiento.';

CREATE OR REPLACE VIEW public.vw_pst_gestion_actividad_presupuesto AS
SELECT
    a.actividad_id,
    a.company_id,
    a.id_presupuesto,
    h.rango_periodo,
    h.fecha_inicia,
    h.fecha_finaliza,
    h.estado_aprobado AS estado_presupuesto,

    a.tipo_actividad,
    a.estado AS estado_actividad,
    a.fecha_actividad,

    a.cuenta_origen_code,
    cpo.name AS cuenta_origen_nombre,
    a.cuenta_destino_code,
    cpd.name AS cuenta_destino_nombre,

    a.monto,
    d_origen.valor_proyeccion AS origen_proyeccion_actual,
    d_destino.valor_proyeccion AS destino_proyeccion_actual,

    CASE
        WHEN a.tipo_actividad = 'TRASLADO'
            THEN COALESCE(d_origen.valor_proyeccion, 0) - a.monto
        ELSE NULL
    END AS origen_proyeccion_resultante,

    COALESCE(d_destino.valor_proyeccion, 0) + a.monto AS destino_proyeccion_resultante,

    CASE
        WHEN a.tipo_actividad = 'AMPLIACION'
             AND a.estado IN ('PENDIENTE', 'APROBADA')
             AND h.estado_aprobado = TRUE
            THEN TRUE
        WHEN a.tipo_actividad = 'TRASLADO'
             AND a.estado IN ('PENDIENTE', 'APROBADA')
             AND h.estado_aprobado = TRUE
             AND COALESCE(d_origen.valor_proyeccion, 0) >= a.monto
            THEN TRUE
        ELSE FALSE
    END AS puede_aplicar,

    a.motivo,
    a.referencia,
    a.created_by,
    a.created_at,
    a.approved_by,
    a.approved_at,
    a.applied_by,
    a.applied_at
FROM public.pst_actividad_presupuesto a
JOIN public.pst_config_presupuesto_hdr h
  ON h.id_presupuesto = a.id_presupuesto
LEFT JOIN public.pst_config_presupuesto_dtl d_origen
  ON d_origen.id_presupuesto = a.id_presupuesto
 AND d_origen.con_cuenta_code = a.cuenta_origen_code
LEFT JOIN public.pst_config_presupuesto_dtl d_destino
  ON d_destino.id_presupuesto = a.id_presupuesto
 AND d_destino.con_cuenta_code = a.cuenta_destino_code
LEFT JOIN public.con_plan_cuenta cpo
  ON cpo.company_id = a.company_id
 AND UPPER(cpo.code) = UPPER(a.cuenta_origen_code)
LEFT JOIN public.con_plan_cuenta cpd
  ON cpd.company_id = a.company_id
 AND UPPER(cpd.code) = UPPER(a.cuenta_destino_code);

COMMENT ON VIEW public.vw_pst_gestion_actividad_presupuesto IS
'Vista de gestion para ampliaciones y traslados de presupuesto por cuenta.';

CREATE TABLE IF NOT EXISTS public.pst_solicitud_actividad_presupuesto (
    solicitud_id BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    company_id BIGINT NOT NULL,
    id_presupuesto VARCHAR(10) NOT NULL,

    tipo_actividad VARCHAR(20) NOT NULL, -- AMPLIACION | TRASLADO
    cuenta_origen_code VARCHAR(20),
    cuenta_destino_code VARCHAR(20) NOT NULL,
    monto NUMERIC(18, 4) NOT NULL,

    justificacion VARCHAR(1000) NOT NULL,
    prioridad SMALLINT NOT NULL DEFAULT 2, -- 1 Alta, 2 Media, 3 Baja
    estado VARCHAR(20) NOT NULL DEFAULT 'PENDIENTE', -- PENDIENTE|APROBADA|RECHAZADA|CANCELADA|ATENDIDA
    fecha_necesaria DATE,

    solicitado_por VARCHAR(100) NOT NULL DEFAULT CURRENT_USER,
    solicitado_en TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT NOW(),
    revisado_por VARCHAR(100),
    revisado_en TIMESTAMP WITHOUT TIME ZONE,
    comentario_revision VARCHAR(500),

    actividad_id BIGINT, -- Se completa cuando la solicitud genera actividad.

    CONSTRAINT ck_pst_sol_tipo CHECK (tipo_actividad IN ('AMPLIACION', 'TRASLADO')),
    CONSTRAINT ck_pst_sol_estado CHECK (estado IN ('PENDIENTE', 'APROBADA', 'RECHAZADA', 'CANCELADA', 'ATENDIDA')),
    CONSTRAINT ck_pst_sol_prioridad CHECK (prioridad BETWEEN 1 AND 3),
    CONSTRAINT ck_pst_sol_monto CHECK (monto > 0),
    CONSTRAINT ck_pst_sol_regla_tipo CHECK (
        (tipo_actividad = 'AMPLIACION' AND cuenta_origen_code IS NULL AND cuenta_destino_code IS NOT NULL)
        OR
        (tipo_actividad = 'TRASLADO' AND cuenta_origen_code IS NOT NULL AND cuenta_destino_code IS NOT NULL AND cuenta_origen_code <> cuenta_destino_code)
    ),

    CONSTRAINT fk_pst_sol_company FOREIGN KEY (company_id)
        REFERENCES public.cfg_company (company_id) ON DELETE RESTRICT,

    CONSTRAINT fk_pst_sol_hdr FOREIGN KEY (id_presupuesto)
        REFERENCES public.pst_config_presupuesto_hdr (id_presupuesto)
        ON UPDATE CASCADE ON DELETE RESTRICT,

    CONSTRAINT fk_pst_sol_dtl_origen FOREIGN KEY (id_presupuesto, cuenta_origen_code)
        REFERENCES public.pst_config_presupuesto_dtl (id_presupuesto, con_cuenta_code)
        ON UPDATE CASCADE ON DELETE RESTRICT,

    CONSTRAINT fk_pst_sol_dtl_destino FOREIGN KEY (id_presupuesto, cuenta_destino_code)
        REFERENCES public.pst_config_presupuesto_dtl (id_presupuesto, con_cuenta_code)
        ON UPDATE CASCADE ON DELETE RESTRICT,

    CONSTRAINT fk_pst_sol_actividad FOREIGN KEY (actividad_id)
        REFERENCES public.pst_actividad_presupuesto (actividad_id)
        ON UPDATE CASCADE ON DELETE SET NULL
);

CREATE INDEX IF NOT EXISTS ix_pst_sol_estado_fecha
    ON public.pst_solicitud_actividad_presupuesto (company_id, estado, solicitado_en DESC);

CREATE INDEX IF NOT EXISTS ix_pst_sol_presupuesto
    ON public.pst_solicitud_actividad_presupuesto (company_id, id_presupuesto);

CREATE INDEX IF NOT EXISTS ix_pst_sol_actividad
    ON public.pst_solicitud_actividad_presupuesto (actividad_id);

COMMENT ON TABLE public.pst_solicitud_actividad_presupuesto IS
'Solicitudes de ampliacion o traslado de presupuesto asociadas a cuentas de detalle.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.solicitud_id IS 'Identificador unico de la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.company_id IS 'Empresa propietaria de la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.id_presupuesto IS 'Id de presupuesto asociado a la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.tipo_actividad IS 'Tipo solicitado: AMPLIACION o TRASLADO.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.cuenta_origen_code IS 'Cuenta origen para traslado (null en ampliacion).';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.cuenta_destino_code IS 'Cuenta destino para aplicar la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.monto IS 'Monto solicitado.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.justificacion IS 'Justificacion detallada de la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.prioridad IS 'Prioridad operativa: 1 alta, 2 media, 3 baja.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.estado IS 'Estado del flujo de la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.fecha_necesaria IS 'Fecha en que se requiere atender la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.solicitado_por IS 'Usuario que crea la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.solicitado_en IS 'Fecha y hora de creacion de la solicitud.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.revisado_por IS 'Usuario que revisa/aprueba/rechaza.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.revisado_en IS 'Fecha y hora de revision.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.comentario_revision IS 'Comentario del revisor.';
COMMENT ON COLUMN public.pst_solicitud_actividad_presupuesto.actividad_id IS 'Actividad generada en historico al atender la solicitud.';
