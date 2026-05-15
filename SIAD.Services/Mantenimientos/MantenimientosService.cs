using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Mantenimientos;

// Mantenimientos de catálogos / configuración (Sprint 3, 2026-05-14).
// Recargo por mora (cfg_recargo_mora) y ajustes tarifarios (adm_ajuste_tarifario).
// Usa EF Core sobre las entidades mapeadas en SiadDbContext.Mantenimientos.cs.
public class MantenimientosService : IMantenimientosService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public MantenimientosService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    private long CompanyId
    {
        get
        {
            var id = _currentCompanyService.GetCompanyId();
            if (id <= 0)
            {
                throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
            }
            return id;
        }
    }

    // ── Recargo por mora ──

    public async Task<RecargoMoraDto> ObtenerRecargoMoraAsync(CancellationToken ct = default)
    {
        var companyId = CompanyId;

        var entity = await _context.cfg_recargo_moras
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.company_id == companyId, ct);

        if (entity is null)
        {
            return new RecargoMoraDto { CompanyId = companyId, TasaMensual = 0, DiasGracia = 0, Activo = false };
        }

        return new RecargoMoraDto
        {
            CompanyId = entity.company_id,
            TasaMensual = entity.tasa_mensual,
            DiasGracia = entity.dias_gracia,
            Descripcion = entity.descripcion,
            Activo = entity.activo
        };
    }

    public async Task<ResponseModelDto> GuardarRecargoMoraAsync(RecargoMoraDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = CompanyId;

        if (dto.TasaMensual < 0 || dto.TasaMensual > 1)
        {
            return ResponseModelDto.Fail("La tasa mensual debe estar entre 0 y 1 (fracción: 0.001667 = 2% anual).");
        }
        if (dto.DiasGracia < 0)
        {
            return ResponseModelDto.Fail("Los días de gracia no pueden ser negativos.");
        }

        try
        {
            var entity = await _context.cfg_recargo_moras
                .FirstOrDefaultAsync(r => r.company_id == companyId, ct);

            if (entity is null)
            {
                entity = new cfg_recargo_mora
                {
                    company_id = companyId,
                    tasa_mensual = dto.TasaMensual,
                    dias_gracia = dto.DiasGracia,
                    descripcion = dto.Descripcion,
                    activo = dto.Activo,
                    created_at = DateTime.UtcNow,
                    created_by = "portal"
                };
                _context.cfg_recargo_moras.Add(entity);
            }
            else
            {
                entity.tasa_mensual = dto.TasaMensual;
                entity.dias_gracia = dto.DiasGracia;
                entity.descripcion = dto.Descripcion;
                entity.activo = dto.Activo;
                entity.updated_at = DateTime.UtcNow;
                entity.updated_by = "portal";
            }

            await _context.SaveChangesAsync(ct);
            return ResponseModelDto.Ok(dto, "Configuración de recargo por mora guardada.");
        }
        catch (Exception ex)
        {
            return ResponseModelDto.Fail($"No se pudo guardar el recargo por mora: {ex.Message}");
        }
    }

    // ── Ajustes tarifarios ──

    public async Task<IReadOnlyList<AjusteTarifarioDto>> ListarAjustesTarifariosAsync(CancellationToken ct = default)
    {
        var companyId = CompanyId;

        var rows = await _context.adm_ajuste_tarifarios
            .AsNoTracking()
            .Where(a => a.company_id == companyId)
            .Include(a => a.cuadro_tarifario)
            .Include(a => a.tipo_ajuste_tarifario)
            .OrderBy(a => a.cuadro_tarifario.codigo)
            .ThenBy(a => a.orden)
            .ThenBy(a => a.ajuste_tarifario_id)
            .ToListAsync(ct);

        return rows.Select(a => new AjusteTarifarioDto
        {
            AjusteTarifarioId = a.ajuste_tarifario_id,
            CuadroTarifarioId = a.cuadro_tarifario_id,
            CuadroCodigo = a.cuadro_tarifario?.codigo ?? string.Empty,
            CuadroNombre = a.cuadro_tarifario?.nombre ?? string.Empty,
            TipoAjusteCodigo = a.tipo_ajuste_tarifario?.codigo ?? string.Empty,
            TipoAjusteNombre = a.tipo_ajuste_tarifario?.nombre ?? string.Empty,
            CondicionCodigo = a.condicion_codigo,
            Porcentaje = a.porcentaje,
            MontoFijo = a.monto_fijo,
            TopeMaximo = a.tope_maximo,
            TopeMensual = LeerTopeMensual(a.parametros),
            Activo = a.status_id == 1
        }).ToList();
    }

    public async Task<ResponseModelDto> GuardarAjusteTarifarioAsync(AjusteTarifarioSaveRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = CompanyId;

        if (dto.AjusteTarifarioId <= 0)
        {
            return ResponseModelDto.Fail("Ajuste tarifario no válido.");
        }
        if (dto.Porcentaje is < 0 or > 100)
        {
            return ResponseModelDto.Fail("El porcentaje debe estar entre 0 y 100.");
        }
        if (dto.MontoFijo is < 0 || dto.TopeMaximo is < 0 || dto.TopeMensual is < 0)
        {
            return ResponseModelDto.Fail("Los montos y topes no pueden ser negativos.");
        }

        try
        {
            var entity = await _context.adm_ajuste_tarifarios
                .FirstOrDefaultAsync(a => a.ajuste_tarifario_id == dto.AjusteTarifarioId
                                          && a.company_id == companyId, ct);

            if (entity is null)
            {
                return ResponseModelDto.Fail("El ajuste tarifario no existe para la empresa actual.");
            }

            entity.porcentaje = dto.Porcentaje;
            entity.monto_fijo = dto.MontoFijo;
            entity.tope_maximo = dto.TopeMaximo;
            entity.parametros = EscribirTopeMensual(entity.parametros, dto.TopeMensual);
            entity.status_id = (short)(dto.Activo ? 1 : 0);
            entity.updated_at = DateTime.UtcNow;
            entity.updated_by = "portal";

            await _context.SaveChangesAsync(ct);
            return ResponseModelDto.Ok(dto, "Ajuste tarifario actualizado.");
        }
        catch (Exception ex)
        {
            return ResponseModelDto.Fail($"No se pudo guardar el ajuste tarifario: {ex.Message}");
        }
    }

    // ── Helpers JSON para parametros.tope_mensual ──

    private static decimal? LeerTopeMensual(string? parametrosJson)
    {
        if (string.IsNullOrWhiteSpace(parametrosJson))
        {
            return null;
        }

        try
        {
            var node = JsonNode.Parse(parametrosJson);
            var valor = node?["tope_mensual"];
            if (valor is null)
            {
                return null;
            }

            return decimal.TryParse(valor.ToString(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var d)
                ? d
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string EscribirTopeMensual(string? parametrosJson, decimal? topeMensual)
    {
        JsonObject obj;
        try
        {
            obj = string.IsNullOrWhiteSpace(parametrosJson)
                ? new JsonObject()
                : JsonNode.Parse(parametrosJson) as JsonObject ?? new JsonObject();
        }
        catch
        {
            obj = new JsonObject();
        }

        if (topeMensual.HasValue)
        {
            obj["tope_mensual"] = topeMensual.Value;
        }
        else
        {
            obj.Remove("tope_mensual");
        }

        return obj.ToJsonString();
    }
}
