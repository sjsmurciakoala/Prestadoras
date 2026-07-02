using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.DTOs.Rutas;
using SIAD.Data;

namespace SIAD.Reports;

public sealed class InformesConsultaService : IInformesConsultaService
{
    private readonly SiadDbContext _context;

    public InformesConsultaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<PartidasInformeResultadoDto> ConsultarPartidasAsync(long companyId, PartidasInformeFiltroDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (companyId <= 0)
        {
            return new PartidasInformeResultadoDto(Array.Empty<PartidasInformeItemDto>(), 0, 0m, 0m);
        }

        var skip = Math.Max(0, filtro.Skip);
        var take = filtro.Take <= 0 ? 100 : Math.Min(filtro.Take, 500);
        var fechaHastaExclusive = filtro.FechaHasta?.Date.AddDays(1);
        var search = string.IsNullOrWhiteSpace(filtro.Search) ? null : filtro.Search.Trim();

        var query = _context.con_partida_hdrs
            .AsNoTracking()
            .Where(x => x.company_id == companyId);

        if (filtro.PeriodId.HasValue)
        {
            query = query.Where(x => x.period_id == filtro.PeriodId.Value);
        }

        if (filtro.JournalId.HasValue)
        {
            query = query.Where(x => x.journal_id == filtro.JournalId.Value);
        }

        if (filtro.TypeId.HasValue)
        {
            query = query.Where(x => x.type_id == filtro.TypeId.Value);
        }

        if (filtro.Status.HasValue)
        {
            query = query.Where(x => x.status == filtro.Status.Value);
        }

        if (filtro.FechaDesde.HasValue)
        {
            var fechaDesde = filtro.FechaDesde.Value.Date;
            query = query.Where(x => x.poliza_date >= fechaDesde);
        }

        if (fechaHastaExclusive.HasValue)
        {
            query = query.Where(x => x.poliza_date < fechaHastaExclusive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.poliza_number, pattern) ||
                (x.document_number != null && EF.Functions.ILike(x.document_number, pattern)) ||
                (x.description != null && EF.Functions.ILike(x.description, pattern)) ||
                (x.source_reference != null && EF.Functions.ILike(x.source_reference, pattern)));
        }

        var totalCount = await query.CountAsync(ct);

        var summaryRows = await query
            .Select(x => new
            {
                TotalDebit = x.lineas.Sum(l => l.debit_amount),
                TotalCredit = x.lineas.Sum(l => l.credit_amount)
            })
            .ToListAsync(ct);

        var rawItems = await query
            .OrderByDescending(x => x.poliza_date)
            .ThenByDescending(x => x.poliza_id)
            .Skip(skip)
            .Take(take)
            .Select(x => new
            {
                x.poliza_id,
                x.poliza_number,
                x.poliza_date,
                PeriodoCode = x.period != null ? x.period.code : null,
                DiarioCode = x.journal != null ? x.journal.code : null,
                TipoCode = x.tipo_transaccion != null ? x.tipo_transaccion.code : null,
                TipoNombre = x.tipo_transaccion != null ? x.tipo_transaccion.name : null,
                x.module,
                x.document_type,
                x.document_number,
                x.description,
                x.status,
                TotalDebit = x.lineas.Sum(l => l.debit_amount),
                TotalCredit = x.lineas.Sum(l => l.credit_amount),
                x.created_at,
                x.created_by
            })
            .ToListAsync(ct);

        var items = rawItems
            .Select(x => new PartidasInformeItemDto(
                x.poliza_id,
                x.poliza_number,
                x.poliza_date,
                x.PeriodoCode,
                x.DiarioCode,
                x.TipoCode,
                x.TipoNombre,
                x.module,
                x.document_type,
                x.document_number,
                x.description,
                x.status,
                x.TotalDebit,
                x.TotalCredit,
                Math.Abs(x.TotalDebit - x.TotalCredit) < 0.01m,
                x.created_at,
                x.created_by))
            .ToList();

        return new PartidasInformeResultadoDto(
            items,
            totalCount,
            summaryRows.Sum(x => x.TotalDebit),
            summaryRows.Sum(x => x.TotalCredit));
    }

    public async Task<IReadOnlyList<ServicioCategoriaLookupDto>> ListarCategoriasServicioAsync(CancellationToken ct = default)
    {
        return await _context.categoria_servicios
            .AsNoTracking()
            .Where(x => x.estado)
            .OrderBy(x => x.descripcion)
            .Select(x => new ServicioCategoriaLookupDto(
                x.categoria_servicio_id,
                x.descripcion))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CicloLookupDto>> ListarCiclosAsync(CancellationToken ct = default)
    {
        return await _context.ciclos
            .AsNoTracking()
            .Where(x => x.estado)
            .OrderBy(x => x.ciclos_descripcioncorta)
            .ThenBy(x => x.ciclos_codigo)
            .Select(x => new CicloLookupDto(
                x.ciclos_id,
                string.IsNullOrWhiteSpace(x.ciclos_descripcioncorta)
                    ? x.ciclos_codigo
                    : x.ciclos_descripcioncorta))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<UsuarioInformeLookupDto>> ListarUsuariosRecibosAsync(CancellationToken ct = default)
    {
        return await _context.pagovariostemps
            .AsNoTracking()
            .Select(x => string.IsNullOrWhiteSpace(x.cajero)
                ? (string.IsNullOrWhiteSpace(x.usuario) ? null : x.usuario.Trim())
                : x.cajero.Trim())
            .Where(x => x != null && x != string.Empty)
            .Distinct()
            .OrderBy(x => x)
            .Select(x => new UsuarioInformeLookupDto(x!, x!))
            .ToListAsync(ct);
    }
}
