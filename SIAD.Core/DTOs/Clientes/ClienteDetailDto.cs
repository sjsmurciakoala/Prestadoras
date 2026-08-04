using System;

namespace SIAD.Core.DTOs.Clientes;

public sealed record ClienteDetailDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string? Apellidos { get; init; }
    public string? Dni { get; init; }
    public string? Rtn { get; init; }
    public string? Acueducto { get; init; }
    public DateTime? FechaNacimiento { get; init; }
    public bool? TerceraEdad { get; init; }
    public bool? BloqueadoCobranza { get; init; }
    public int? AbogadoId { get; init; }
    public string? AbogadoNombre { get; init; }
    public string? Telefono { get; init; }
    public string? TelefonoMovil { get; init; }
    public string? Email { get; init; }
    public string? Direccion { get; init; }
    public string? BarrioCodigo { get; init; }
    public string? BarrioNombre { get; init; }
    public string? ColorCasa { get; init; }

    public string? EmpresaNombre { get; init; }
    public string? EmpresaRtn { get; init; }
    public string? EmpresaTelefono { get; init; }
    public string? EmpresaDireccion { get; init; }

    public int? ServicioId { get; init; }
    public string? ServicioNombre { get; init; }
    public int? CategoriaServicioId { get; init; }
    public string? CategoriaServicioNombre { get; init; }
    public string? LetraCodigo { get; init; }
    public int? CicloId { get; init; }
    public string? CicloDescripcion { get; init; }
    public string? Libreta { get; init; }
    public string? Secuencia { get; init; }
    public string? IndicativoRuta { get; init; }
    public string? TipoUsoCodigo { get; init; }
    public string? ClaveCatastral { get; init; }
    public string? ClaveSure { get; init; }
    public string? Contador { get; init; }
    public bool? TieneMedidor { get; init; }
    public int? MedidorId { get; init; }
    public string? MedidorNumero { get; init; }
    public bool? TieneConvenio { get; init; }
    public string? NumeroConvenio { get; init; }
    public bool? TieneContrato { get; init; }
    public string? NumeroContrato { get; init; }
    public string? Observaciones { get; init; }

    public string? UsuarioCreacion { get; init; }
    public DateTime? FechaCreacion { get; init; }
    public double? DescuentoTerceraEdad { get; init; }

    public bool? EstudioSocioeconomico { get; init; }
    public bool? NoCortable { get; init; }

    public bool Activo { get; init; }

    /// <summary>
    /// Solo viene poblado en la respuesta de ACTUALIZAR cuando el cambio de
    /// categoría reclasificó CxC (partida por config POR_SERVICIO_CATEGORIA).
    /// Null en consultas normales.
    /// </summary>
    public ClienteReclasificacionInfoDto? Reclasificacion { get; init; }
}

/// <summary>Resultado visible de la reclasificación contable por cambio de categoría.</summary>
public sealed record ClienteReclasificacionInfoDto
{
    public decimal MontoReclasificado { get; init; }
    public long? PolizaId { get; init; }
    public int FacturasActualizadas { get; init; }
}
