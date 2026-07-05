using System.Text;
using apc.BancosWs.Contrato;
using SIAD.Core.DTOs.BancosWs;

namespace SIAD.Tests;

// F8 — pruebas de equivalencia del contrato del WS bancario (D8: contrato
// CONGELADO). Los golden files de GoldenFiles/BancosWs se construyeron A MANO
// desde el Java del WS viejo, los logs de producción y la BD SIMAFI real
// (docs/f8-contrato-ws-bancario.md) — NO se regeneran desde ContractXml.
// La comparación es por BYTES UTF-8 (sin BOM): un espacio o un orden distinto
// es un fallo de contrato con el banco.
public sealed class BancosWsGoldenTests
{
    private static byte[] Golden(string nombre) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "GoldenFiles", "BancosWs", nombre));

    private static void AssertBytes(string goldenFile, string actual)
    {
        var esperado = Golden(goldenFile);
        var reales = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(actual);
        if (!esperado.AsSpan().SequenceEqual(reales))
        {
            // xunit muestra strings legibles en el fallo; la comparación real es por bytes.
            Assert.Equal(Encoding.UTF8.GetString(esperado), actual);
            Assert.Fail($"Los bytes no coinciden con {goldenFile} (misma cadena, distinta codificación).");
        }
    }

    [Fact]
    public void Mensaje_pago_exitoso_es_byte_compatible() =>
        AssertBytes("mensaje-pago-exitoso.xml", ContractXml.Mensaje(200, error: false, ContractXml.MsgPagoExitoso));

    [Fact]
    public void Mensaje_reversion_exitosa_es_byte_compatible() =>
        AssertBytes("mensaje-reversion-exitosa.xml", ContractXml.Mensaje(200, error: false, ContractXml.MsgReversionExitosa));

    [Fact]
    public void Mensaje_no_existe_registro_es_byte_compatible() =>
        AssertBytes("mensaje-no-existe-registro.xml", ContractXml.Mensaje(400, error: false, ContractXml.MsgNoExisteRegistro));

    [Fact]
    public void Mensaje_no_hay_pagos_pendientes_es_byte_compatible() =>
        AssertBytes("mensaje-no-hay-pagos-pendientes.xml", ContractXml.Mensaje(400, error: false, ContractXml.MsgNoHayPagosPendientes));

    [Fact]
    public void Mensaje_facturas_vencidas_es_byte_compatible() =>
        AssertBytes("mensaje-facturas-vencidas.xml", ContractXml.Mensaje(400, error: false, ContractXml.MsgFacturasVencidas));

    [Fact]
    public void Mensaje_no_autorizado_es_byte_compatible() =>
        AssertBytes("mensaje-no-autorizado.xml", ContractXml.Mensaje(400, error: false, ContractXml.MsgNoAutorizado));

    [Fact]
    public void Mensaje_problema_servidor_es_byte_compatible() =>
        AssertBytes("mensaje-problema-servidor.xml", ContractXml.Mensaje(400, error: true, ContractXml.MsgProblemaServidor));

    [Fact]
    public void Mensaje_total_no_coincide_es_byte_compatible() =>
        AssertBytes("mensaje-total-no-coincide.xml", ContractXml.Mensaje(400, error: false, ContractXml.MsgTotalNoCoincide));

    [Fact]
    public void Mensaje_no_se_puede_pagar_es_byte_compatible() =>
        AssertBytes("mensaje-no-se-puede-pagar.xml", ContractXml.Mensaje(400, error: true, ContractXml.MsgNoSePuedePagar));

    [Fact]
    public void Rechazo_del_filtro_de_auth_replica_el_toString_del_ws_viejo() =>
        AssertBytes("filtro-no-autorizado.txt", ContractXml.MensajeFiltroNoAutorizado());

    [Fact]
    public void Pago_dummy_es_byte_compatible() =>
        AssertBytes("pago-dummy.xml", ContractXml.PagoDummy());

    [Fact]
    public void Consulta_replica_la_factura_real_de_produccion_simafi()
    {
        // Caso REAL verificado en bdsimafi.pagovariostemp (clave 090504129,
        // recibo 4164766, período 2026/06): 8 conceptos con líneas negativas
        // (Saldo Anterior, Pagos) y cero (Descuentos) — el total neto 272.11
        // es la suma. El golden replica el XML que emitía el WS viejo.
        var consulta = new BancosWsConsultaDto
        {
            Resultado = BancosWsConsultaResultado.Ok,
            Clave = "090504129",
            Nombre = "MERINO BAIDE JOSE CECILIO",
            Direccion = "PBLO.NVO.4CLL.ANT.ESC.11 JUNIO",
            Total = 272.11m,
            FechaVence = "N",
            Detalles =
            [
                new BancosWsDetalleDto { Id = 12049117, CodigoConcepto = "01", Concepto = "Saldo Anterior", Valor = -778.67m },
                new BancosWsDetalleDto { Id = 12049118, CodigoConcepto = "02", Concepto = "Agua Potable", Valor = 163.67m },
                new BancosWsDetalleDto { Id = 12049119, CodigoConcepto = "03", Concepto = "Alcantarillado Sanitario", Valor = 98.20m },
                new BancosWsDetalleDto { Id = 12049120, CodigoConcepto = "04", Concepto = "Fondo Fuentes de Agua y p.c.", Valor = 5.00m },
                new BancosWsDetalleDto { Id = 12049121, CodigoConcepto = "05", Concepto = "Tasa ERSAPS", Valor = 5.24m },
                new BancosWsDetalleDto { Id = 12049122, CodigoConcepto = "12", Concepto = "Convenio de Pago", Valor = 1051.08m },
                new BancosWsDetalleDto { Id = 12049123, CodigoConcepto = "16", Concepto = "Pagos", Valor = -272.41m },
                new BancosWsDetalleDto { Id = 12049124, CodigoConcepto = "17", Concepto = "Descuentos o Rebajas", Valor = 0.00m },
            ],
        };

        AssertBytes("consulta-servicios-simafi.xml", ContractXml.Factura(consulta));
    }

    [Fact]
    public void Consulta_omite_direccion_nula_y_escapa_contenido()
    {
        var consulta = new BancosWsConsultaDto
        {
            Resultado = BancosWsConsultaResultado.Ok,
            Clave = "090000001",
            Nombre = "PEREZ & GOMEZ <SUCESORES>",
            Direccion = string.Empty,
            Total = 10.00m,
            FechaVence = "N",
            Detalles = [new BancosWsDetalleDto { Id = 1, CodigoConcepto = "02", Concepto = "Agua", Valor = 10.00m }],
        };

        var xml = ContractXml.Factura(consulta);

        Assert.DoesNotContain("<direccion>", xml);
        Assert.Contains("PEREZ &amp; GOMEZ &lt;SUCESORES&gt;", xml);
        Assert.Contains("<estado></estado>", xml);   // forma larga, nunca <estado />
    }

    [Fact]
    public void Montos_usan_dos_decimales_y_punto_invariante()
    {
        Assert.Equal("0.00", ContractXml.Monto(0m));
        Assert.Equal("-778.67", ContractXml.Monto(-778.67m));
        Assert.Equal("1051.08", ContractXml.Monto(1051.08m));
        Assert.Equal("272.10", ContractXml.Monto(272.1m));
    }
}
