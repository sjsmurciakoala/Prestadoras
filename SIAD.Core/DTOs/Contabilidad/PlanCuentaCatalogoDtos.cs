namespace SIAD.Core.DTOs.Contabilidad;

public record PlanCuentaCatalogoItemDto(
    long AccountId,
    string Code,
    string Name,
    short Level);

public record PlanCuentaCatalogoLookupDto(
    string? Grupo,
    string? SubGrupo,
    string? CuentaMayor,
    string? SubCuenta,
    string? Detalle);

public record PlanCuentaCatalogoFilterDto(
    string? Term);
