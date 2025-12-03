namespace SIAD.Core.DTOs.Ordenes;

public sealed record UsuarioMiOrdenDto(
    int Id,
    string Nombre,
    string Usuario,
    string Clave,
    int Tipo,
    string DescripcionTipo,
    bool Estado,
    string DescripcionEstado);
