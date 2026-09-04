using System;
using SIAD.Core.Utilities;
using Xunit;

namespace SIAD.Tests.Almacen;

/// <summary>
/// El UUIDv5 es la pieza de la que depende TODA la idempotencia del motor de inventario:
/// si dejara de ser determinista, un reintento de posteo duplicaría asientos en un libro
/// que la BD hace inmutable (no se pueden borrar). Por eso se fija con vectores conocidos
/// del RFC 4122, no solo con "dos llamadas dan lo mismo".
///
/// No requieren base de datos: son puros.
/// </summary>
public class UuidV5Tests
{
    // Vector canónico del RFC 4122 / erratas: UUIDv5(ns:DNS, "www.example.org").
    private static readonly Guid NamespaceDns = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public void VectorConocido_RfcDns_ProduceElUuidEsperado()
    {
        var uuid = UuidV5.Create(NamespaceDns, "www.example.org");

        Assert.Equal(new Guid("74738ff5-5367-5958-9aee-98fffdcd1876"), uuid);
    }

    [Fact]
    public void VectorConocido_PythonOrg_ProduceElUuidEsperado()
    {
        // Segundo vector independiente, el que documenta la librería uuid de Python:
        // uuid5(NAMESPACE_DNS, "python.org").
        var uuid = UuidV5.Create(NamespaceDns, "python.org");

        Assert.Equal(new Guid("886313e1-3b8a-5372-9b90-0c9aee199e5d"), uuid);
    }

    [Fact]
    public void EsDeterminista_MismoNombreMismoUuid()
    {
        const string nombre = "CARGA_INICIAL|2|1234|7|1";

        Assert.Equal(UuidV5.CreateInventario(nombre), UuidV5.CreateInventario(nombre));
    }

    [Fact]
    public void NombresDistintos_ProducenUuidsDistintos()
    {
        // El discriminador de intento es lo que permite re-abrir un par tras una reversa:
        // si no cambiara el uuid, el índice único bloquearía la segunda apertura.
        var intento1 = UuidV5.CreateInventario("CARGA_INICIAL|2|1234|7|1");
        var intento2 = UuidV5.CreateInventario("CARGA_INICIAL|2|1234|7|2");

        Assert.NotEqual(intento1, intento2);
    }

    [Fact]
    public void DistintaEmpresa_ProduceUuidDistinto()
    {
        // El company_id va dentro del nombre: dos empresas con el mismo par
        // (articulo, bodega) no pueden colisionar.
        Assert.NotEqual(
            UuidV5.CreateInventario("CARGA_INICIAL|2|1234|7|1"),
            UuidV5.CreateInventario("CARGA_INICIAL|3|1234|7|1"));
    }

    [Fact]
    public void MarcaVersion5YVarianteRfc()
    {
        var bytes = UuidV5.CreateInventario("cualquier-cosa").ToByteArray();

        // Guid.ToByteArray() devuelve los 3 primeros campos en little-endian:
        // el octeto de versión (índice 6 en big-endian) cae en el 7.
        Assert.Equal(0x50, bytes[7] & 0xF0);
        Assert.Equal(0x80, bytes[8] & 0xC0);
    }

    [Fact]
    public void NombreNulo_Lanza()
    {
        Assert.Throws<ArgumentNullException>(() => UuidV5.CreateInventario(null!));
    }

    [Fact]
    public void NamespaceDeInventario_NoCambia()
    {
        // Este valor NO se puede cambiar nunca: de él dependen todos los uuid posteados.
        Assert.Equal(new Guid("6f9619ff-8b86-d011-b42d-00c04fc964ff"), UuidV5.NamespaceInventario);
    }
}
