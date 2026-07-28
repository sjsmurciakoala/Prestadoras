using System;
using System.Collections.Generic;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Services.Proveedores;
using Xunit;

namespace SIAD.Tests.Proveedores;

public class ProveedorContactosNormalizerTests
{
    private static ProveedorContactoDto Row(string? nombre = null, string? email = null,
        string? telefono = null, long? tipoId = null) =>
        new() { Nombre = nombre, Email = email, Telefono = telefono, TipoContactoId = tipoId };

    [Fact]
    public void Normalize_DescartaFilasVacias()
    {
        var result = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row(), Row("  "), Row("Ana") });

        Assert.Single(result);
        Assert.Equal("Ana", result[0].Nombre);
    }

    [Fact]
    public void Normalize_SinContactos_DevuelveListaVacia()
    {
        Assert.Empty(ProveedorContactosNormalizer.Normalize(new List<ProveedorContactoDto>()));
        Assert.Empty(ProveedorContactosNormalizer.Normalize(null));
    }

    [Fact]
    public void Normalize_FilaConDatosPeroSinNombre_Lanza()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row(telefono: "2222-3333") }));

        Assert.Contains("fila 1", ex.Message);
    }

    [Fact]
    public void Normalize_ReasignaOrdenConsecutivo()
    {
        var result = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row("Ana"), Row("  "), Row("Beto") });

        Assert.Equal(1, result[0].Orden);
        Assert.Equal(2, result[1].Orden);
    }

    [Fact]
    public void Normalize_RecortaEspacios()
    {
        var result = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row("  Ana  ", email: "  ana@x.com ") });

        Assert.Equal("Ana", result[0].Nombre);
        Assert.Equal("ana@x.com", result[0].Email);
    }

    [Fact]
    public void Normalize_EmailInvalido_Lanza()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row("Ana", email: "ana-arroba-x") }));

        Assert.Contains("email", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Normalize_NombresRepetidos_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row("Ana"), Row(" ana ") }));
    }

    [Fact]
    public void Normalize_NombreMuyLargo_Lanza()
    {
        Assert.Throws<ArgumentException>(() =>
            ProveedorContactosNormalizer.Normalize(
                new List<ProveedorContactoDto> { Row(new string('x', 151)) }));
    }

    [Fact]
    public void BuildLegacyFields_TomaElPrimerContacto()
    {
        var contactos = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto>
            {
                Row("Ana", "ana@x.com", "2222-3333"),
                Row("Beto", "beto@x.com", "4444-5555")
            });

        var legacy = ProveedorContactosNormalizer.BuildLegacyFields(contactos);

        Assert.Equal("Ana", legacy.NombreContacto);
        Assert.Equal("2222-3333", legacy.Telefono);
        Assert.Equal("ana@x.com", legacy.Email);
    }

    [Fact]
    public void BuildLegacyFields_TelefonoLargo_SeTruncaA20()
    {
        var contactos = ProveedorContactosNormalizer.Normalize(
            new List<ProveedorContactoDto> { Row("Ana", telefono: "2222-3333 ext 101 / 9999") });

        var legacy = ProveedorContactosNormalizer.BuildLegacyFields(contactos);

        Assert.Equal(20, legacy.Telefono!.Length);
        Assert.Equal("2222-3333 ext 101 / ", legacy.Telefono);
    }

    [Fact]
    public void BuildLegacyFields_SinContactos_DevuelveNulos()
    {
        var legacy = ProveedorContactosNormalizer.BuildLegacyFields(new List<ProveedorContactoDto>());

        Assert.Null(legacy.NombreContacto);
        Assert.Null(legacy.Telefono);
        Assert.Null(legacy.Email);
    }
}
