GoldenFiles del contrato del WS bancario (docs/f8-contrato-ws-bancario.md).
Construidos A MANO desde el Java/logs/produccion SIMAFI - NO regenerarlos desde ContractXml
(el golden debe ser independiente del codigo que valida). El caso consulta-servicios-simafi.xml
replica la factura real de la clave 090504129 verificada en bdsimafi el 2026-07-04.
Si la captura real del WS vivo (cutover) difiere en el ORDEN de elementos, ajustar
ContractXml y estos archivos juntos.
