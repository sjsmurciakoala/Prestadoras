using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Cobranza;

public class ClienteCobroFiltroDto
{
    public int? CicloId { get; set; }
    public string? BarrioCodigo { get; set; }
    public int? CategoriaId { get; set; }
    public string? Ruta { get; set; }
    public decimal? ValorMinimo { get; set; }
    public int? DiasMoraMin { get; set; }
    public bool ExcluirBloqueados { get; set; }
    public bool ExcluirNoCortables { get; set; }
    public string? Busqueda { get; set; }
}

public record ClienteCobroDto(
    string Clave, string? Nombre, string? Direccion, int? CicloId, string? BarrioCodigo,
    int? CategoriaId, string? Ruta, decimal SaldoAdeudado, int? DiasMora, DateOnly? UltimoPago,
    bool Bloqueado, bool NoCortable, int? AbogadoId);

public record RegistrarAccionLoteRequest(
    IReadOnlyList<string> Claves, int CodAccion, int? CodObservacion, int? AbogadoId,
    string? Observacion, string? EjecutadoPor);

public record GenerarCartasCobroRequest(IReadOnlyList<string> Claves, int PlazoHoras = 24);

public record CartaCobroHdrDto(int Id, string Correlativo, DateOnly FechaGeneracion, int TotalClientes, int? PlazoHoras = null);

public record CartaEmpresaDto(
    string NombreComercial, string? RazonSocial, string? Rtn, string? Direccion,
    string? Telefono, string? Email, string? LogoBase64, string? LogoMime);

public record CartaCobroClienteDto(
    string Clave, string? Nombre, string? Direccion, int? CicloId, string? Ruta,
    decimal SaldoTotal, int? DiasMora, IReadOnlyList<CobranzaSaldoDetalleDto> Detalle,
    string? Medidor = null, string? Libreta = null, string? Secuencia = null, string? Identidad = null,
    int NumeroRequerimiento = 1);

public record CartaCobroLoteDto(
    CartaCobroHdrDto Encabezado, CartaEmpresaDto Empresa, IReadOnlyList<CartaCobroClienteDto> Clientes);
