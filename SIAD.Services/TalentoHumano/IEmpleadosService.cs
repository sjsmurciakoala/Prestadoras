using SIAD.Core.DTOs.TalentoHumano;

namespace SIAD.Services.TalentoHumano;

public interface IEmpleadosService
{
    Task<IReadOnlyList<EmpleadoListItemDto>> GetAsync(EmpleadoFilterDto? filtro, CancellationToken ct = default);
    Task<EmpleadoEditDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<EmpleadoLookupDto>> GetLookupAsync(CancellationToken ct = default);
    Task<EmpleadoEditDto> CreateAsync(EmpleadoEditDto dto, string user, CancellationToken ct = default);
    Task<EmpleadoEditDto> UpdateAsync(int id, EmpleadoEditDto dto, string user, CancellationToken ct = default);
    Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default);

    /// <summary>Lee un Excel (Código, Nombre, Activo) y hace upsert por código dentro de la empresa actual.</summary>
    Task<EmpleadoImportResultDto> ImportarExcelAsync(Stream excelStream, string user, CancellationToken ct = default);

    /// <summary>Genera la plantilla en blanco (encabezados + fila de ejemplo) para la importación.</summary>
    byte[] GenerarPlantillaExcel();
}
