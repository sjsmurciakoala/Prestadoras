# Historico legacy

Scripts SQL que crearon o modificaron tablas/SP del modelo legacy
(tarifas, tarifas_contador, configuracion_tasas, configuracion_tasas_detalle,
servicios_roles_ws, condicion_lectura, informativo,
configuracion_app_lectura_medidores, cai, letras, letracodigo).

**No reaplicar.** Las tablas y SPs que tocan ya no existen en la BD desde el
corte legacy del **2026-04-28** (drops manuales del usuario) y del
**2026-05-05** (eliminacion del codigo C# / Blazor / Java + drop de
`fn_generar_codigo_cliente`).

Se conservan aqui solo como referencia historica del flujo previo. Si se
necesita reproducir el modelo legacy en otro ambiente, partir de un dump
anterior al 28-abr-2026.

Para el modelo V3 actual ver `Database/ddl_v3/`.
