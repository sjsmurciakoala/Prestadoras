using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Xunit;
using SIAD.Core.DTOs.Clientes;

namespace SIAD.Tests;

/// <summary>
/// Regresión del bug de edición (2026-07-16): ClienteCreateDto se corrigió para
/// aceptar libretas de catálogo (00L2) pero ClienteUpdateDto conservó la regex
/// numérica vieja — el formulario pasaba la validación local y el PUT moría en
/// el servidor con el genérico "One or more validation errors occurred".
/// Ambos DTOs deben aceptar exactamente lo mismo. No requiere BD.
/// </summary>
public class ClienteDtoValidacionTests
{
    private static List<ValidationResult> Validar(object dto)
    {
        var resultados = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), resultados, validateAllProperties: true);
        return resultados;
    }

    [Theory]
    [InlineData("00L2")]
    [InlineData("00l5")]
    [InlineData("70")]
    [InlineData("20070")]
    public void Update_dto_acepta_libretas_de_catalogo_y_legacy(string libreta)
    {
        var dto = new ClienteUpdateDto
        {
            Clave = "TST",
            Nombre = "PRUEBA",
            Dni = "0000-0000-00000",
            Libreta = libreta,
        };

        Assert.Empty(Validar(dto));
    }

    [Theory]
    [InlineData("00L2")]
    [InlineData("00l5")]
    [InlineData("70")]
    public void Create_dto_acepta_libretas_de_catalogo_y_legacy(string libreta)
    {
        var dto = new ClienteCreateDto
        {
            Clave = "TST",
            Nombre = "PRUEBA",
            Dni = "0000-0000-00000",
            Libreta = libreta,
        };

        Assert.Empty(Validar(dto));
    }

    [Fact]
    public void Libreta_con_guion_es_rechazada_en_ambos_dtos()
    {
        // El indicativo se separa por '-': un guion dentro de la libreta
        // rompería el split_part de toda la cadena de lectura.
        var update = new ClienteUpdateDto { Clave = "TST", Nombre = "P", Dni = "1", Libreta = "00-L2" };
        var create = new ClienteCreateDto { Clave = "TST", Nombre = "P", Dni = "1", Libreta = "00-L2" };

        Assert.NotEmpty(Validar(update));
        Assert.NotEmpty(Validar(create));
    }
}
