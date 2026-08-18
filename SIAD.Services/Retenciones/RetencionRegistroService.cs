using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Entities;
using SIAD.Core.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Retenciones;

/// <summary>
/// Consulta del registro fiscal de retenciones (F4). EF LINQ sobre prv_retencion_hdr/dtl; el filtro
/// tenant lo aplica SiadDbContext. La descripción del estado numérico se resuelve en memoria con
/// <see cref="EstadoRetencion.Descripcion"/> (no traducible en SQL).
/// </summary>
public sealed class RetencionRegistroService : IRetencionRegistroService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompany;

    public RetencionRegistroService(SiadDbContext context, ICurrentCompanyService currentCompany)
    {
        _context = context;
        _currentCompany = currentCompany;
    }

    public async Task<RetencionRegistroPagedResultDto> BuscarAsync(
        RetencionRegistroFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new RetencionRegistroFilterDto();

        var q = _context.prv_retencion_hdrs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.CodProveedor))
        {
            var cod = filtro.CodProveedor.Trim();
            q = q.Where(x => x.cod_proveedor == cod);
        }

        if (filtro.Desde.HasValue)
            q = q.Where(x => x.fecha_emision >= filtro.Desde.Value);

        if (filtro.Hasta.HasValue)
            q = q.Where(x => x.fecha_emision <= filtro.Hasta.Value);

        if (filtro.EstadoId.HasValue)
            q = q.Where(x => x.estado_id == filtro.EstadoId.Value);

        if (filtro.Folio.HasValue)
            q = q.Where(x => x.folio == filtro.Folio.Value);

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var s = $"%{filtro.Search.Trim()}%";
            q = q.Where(x =>
                (x.cod_proveedor != null && EF.Functions.ILike(x.cod_proveedor, s)) ||
                (x.rtn_proveedor != null && EF.Functions.ILike(x.rtn_proveedor, s)) ||
                (x.poliza_number != null && EF.Functions.ILike(x.poliza_number, s)));
        }

        var total = await q.CountAsync(ct);

        var desc = filtro.SortDescending;
        var ordered = (filtro.SortField?.Trim().ToLowerInvariant()) switch
        {
            "folio" => desc ? q.OrderByDescending(x => x.folio) : q.OrderBy(x => x.folio),
            "codproveedor" or "cod_proveedor" or "proveedor" =>
                desc ? q.OrderByDescending(x => x.cod_proveedor) : q.OrderBy(x => x.cod_proveedor),
            "totalretenido" or "total_retenido" =>
                desc ? q.OrderByDescending(x => x.total_retenido) : q.OrderBy(x => x.total_retenido),
            "fechaemision" or "fecha_emision" =>
                desc ? q.OrderByDescending(x => x.fecha_emision) : q.OrderBy(x => x.fecha_emision),
            _ => q.OrderByDescending(x => x.fecha_emision).ThenByDescending(x => x.folio)
        };

        var take = filtro.Take <= 0 ? 15 : filtro.Take;
        var skip = filtro.Skip < 0 ? 0 : filtro.Skip;

        var rows = await ordered
            .Skip(skip)
            .Take(take)
            .Select(x => new HdrRow
            {
                RetencionHdrId = x.retencion_hdr_id,
                Folio = x.folio,
                FechaEmision = x.fecha_emision,
                NumeroOrden = x.numero_orden,
                NumeroAbono = x.numero_abono,
                Origen = x.origen,
                CxpId = x.cxp_id,
                CodProveedor = x.cod_proveedor,
                RtnProveedor = x.rtn_proveedor,
                BaseTotal = x.base_total,
                TotalRetenido = x.total_retenido,
                PartidaId = x.partida_id,
                PolizaNumber = x.poliza_number,
                EstadoId = x.estado_id,
                MotivoAnulacion = x.motivo_anulacion
            })
            .ToListAsync(ct);

        var items = rows.Select(ToListItem).ToList();
        return new RetencionRegistroPagedResultDto { Items = items, TotalCount = total };
    }

    public async Task<RetencionRegistroDetalleDto?> GetDetalleAsync(long retencionHdrId, CancellationToken ct = default)
    {
        var row = await _context.prv_retencion_hdrs.AsNoTracking()
            .Where(x => x.retencion_hdr_id == retencionHdrId)
            .Select(x => new HdrRow
            {
                RetencionHdrId = x.retencion_hdr_id,
                Folio = x.folio,
                FechaEmision = x.fecha_emision,
                NumeroOrden = x.numero_orden,
                NumeroAbono = x.numero_abono,
                Origen = x.origen,
                CxpId = x.cxp_id,
                CodProveedor = x.cod_proveedor,
                RtnProveedor = x.rtn_proveedor,
                BaseTotal = x.base_total,
                TotalRetenido = x.total_retenido,
                PartidaId = x.partida_id,
                PolizaNumber = x.poliza_number,
                EstadoId = x.estado_id,
                MotivoAnulacion = x.motivo_anulacion
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        var lineas = await _context.prv_retencion_dtls.AsNoTracking()
            .Where(d => d.retencion_hdr_id == retencionHdrId)
            .OrderBy(d => d.retencion_dtl_id)
            .Select(d => new RetencionRegistroLineaDto
            {
                RetencionDtlId = d.retencion_dtl_id,
                RetencionId = d.retencion_id,
                Codigo = d.codigo,
                Nombre = d.nombre,
                Porcentaje = d.porcentaje,
                BaseLinea = d.base_linea,
                MontoRetenido = d.monto_retenido,
                AccountId = d.account_id
            })
            .ToListAsync(ct);

        return new RetencionRegistroDetalleDto { Cabecera = ToListItem(row), Lineas = lineas };
    }

    public async Task<ConstanciaRetencionImpresionDto?> GetDatosConstanciaAsync(
        long retencionHdrId, string? impresoPor = null, CancellationToken ct = default)
    {
        // Cabecera fiscal (entidad completa; la consume el builder puro). Tenant automático.
        var hdr = await _context.prv_retencion_hdrs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.retencion_hdr_id == retencionHdrId, ct);
        if (hdr is null)
            return null;

        var lineas = await _context.prv_retencion_dtls.AsNoTracking()
            .Where(d => d.retencion_hdr_id == retencionHdrId)
            .OrderBy(d => d.retencion_dtl_id)
            .Select(d => new RetencionRegistroLineaDto
            {
                RetencionDtlId = d.retencion_dtl_id,
                RetencionId = d.retencion_id,
                Codigo = d.codigo,
                Nombre = d.nombre,
                Porcentaje = d.porcentaje,
                BaseLinea = d.base_linea,
                MontoRetenido = d.monto_retenido,
                AccountId = d.account_id
            })
            .ToListAsync(ct);

        // Empresa (agente retenedor): cfg_company NO es tenant-scoped → filtrar por la empresa actual.
        var companyId = _currentCompany.GetCompanyId();
        var company = await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        // Nombre del proveedor + concepto + referencia del documento origen, según el origen del pago:
        // compromiso (origen=1, join a prv_compromiso_hdr) o factura de compra (origen=2, join a la CxP).
        string? nombreProveedor;
        string? concepto;
        string? documentoTexto;
        if (hdr.origen == OrigenRetencion.Compra && hdr.cxp_id is { } cxpId)
        {
            var cxp = await _context.alm_compra_cxps.AsNoTracking()
                .Where(c => c.id == cxpId)
                .Select(c => new { c.proveedor, c.numero_factura_sar, c.compra_hdr_id })
                .FirstOrDefaultAsync(ct);
            var numeroFactura = cxp is null
                ? null
                : !string.IsNullOrWhiteSpace(cxp.numero_factura_sar) ? cxp.numero_factura_sar!.Trim() : $"#{cxp.compra_hdr_id}";
            nombreProveedor = cxp?.proveedor;
            concepto = numeroFactura is null ? null : $"Factura {numeroFactura}";
            documentoTexto = numeroFactura is null
                ? $"Compra — Pago No. {hdr.numero_abono}"
                : $"Factura {numeroFactura} — Pago No. {hdr.numero_abono}";
        }
        else
        {
            var compromiso = await _context.prv_compromiso_hdrs.AsNoTracking()
                .Where(c => c.numero_orden == hdr.numero_orden)
                .Select(c => new { c.nombre_proveedor, c.concepto })
                .FirstOrDefaultAsync(ct);
            nombreProveedor = compromiso?.nombre_proveedor;
            concepto = compromiso?.concepto;
            documentoTexto = null;   // el builder compone "Orden No. … — Abono No. …"
        }

        return ConstanciaRetencionBuilder.Build(
            hdr, lineas, company, nombreProveedor, concepto, impresoPor, documentoTexto);
    }

    public async Task<IReadOnlyList<RetencionDeclaracionLineaDto>> BuscarDeclaracionAsync(
        RetencionDeclaracionFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new RetencionDeclaracionFilterDto();

        // Detalle × cabecera (ambos tenant-scoped). El nombre del proveedor se resuelve con una
        // subconsulta correlacionada al compromiso (también tenant-scoped) en la proyección final.
        var q =
            from d in _context.prv_retencion_dtls.AsNoTracking()
            join h in _context.prv_retencion_hdrs.AsNoTracking()
                on d.retencion_hdr_id equals h.retencion_hdr_id
            select new { d, h };

        if (filtro.Desde.HasValue)
            q = q.Where(x => x.h.fecha_emision >= filtro.Desde.Value);

        if (filtro.Hasta.HasValue)
            q = q.Where(x => x.h.fecha_emision <= filtro.Hasta.Value);

        if (filtro.EstadoId.HasValue)
            q = q.Where(x => x.h.estado_id == filtro.EstadoId.Value);

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var s = $"%{filtro.Search.Trim()}%";
            q = q.Where(x =>
                (x.h.cod_proveedor != null && EF.Functions.ILike(x.h.cod_proveedor, s)) ||
                (x.h.rtn_proveedor != null && EF.Functions.ILike(x.h.rtn_proveedor, s)) ||
                EF.Functions.ILike(x.d.codigo, s) ||
                EF.Functions.ILike(x.d.nombre, s));
        }

        // Orden natural del reporte: por tipo, luego proveedor, luego fecha/folio (traducible en SQL;
        // la descripción del estado se resuelve en memoria, como en BuscarAsync).
        var rows = await q
            .OrderBy(x => x.d.nombre).ThenBy(x => x.h.cod_proveedor)
            .ThenBy(x => x.h.fecha_emision).ThenBy(x => x.h.folio)
            .Select(x => new
            {
                x.h.folio,
                x.h.fecha_emision,
                x.h.numero_orden,
                x.h.numero_abono,
                x.h.origen,
                x.h.cod_proveedor,
                x.h.rtn_proveedor,
                x.h.estado_id,
                x.d.retencion_id,
                x.d.codigo,
                x.d.nombre,
                x.d.porcentaje,
                x.d.base_linea,
                x.d.monto_retenido,
                // Nombre del proveedor: del compromiso (origen=1) o de la CxP de compra (origen=2). Los
                // dos subqueries se excluyen mutuamente por el NULL (numero_orden / cxp_id).
                NombreProveedorCompromiso = _context.prv_compromiso_hdrs
                    .Where(cc => cc.numero_orden == x.h.numero_orden)
                    .Select(cc => cc.nombre_proveedor)
                    .FirstOrDefault(),
                NombreProveedorCompra = _context.alm_compra_cxps
                    .Where(cc => cc.id == x.h.cxp_id)
                    .Select(cc => (string?)cc.proveedor)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return rows.Select(r => new RetencionDeclaracionLineaDto
        {
            Folio = r.folio,
            FechaEmision = r.fecha_emision,
            NumeroOrden = r.numero_orden,
            NumeroAbono = r.numero_abono,
            Origen = r.origen,
            CodProveedor = r.cod_proveedor,
            NombreProveedor = r.NombreProveedorCompromiso ?? r.NombreProveedorCompra,
            RtnProveedor = r.rtn_proveedor,
            RetencionId = r.retencion_id,
            TipoCodigo = r.codigo,
            TipoNombre = r.nombre,
            Porcentaje = r.porcentaje,
            BaseLinea = r.base_linea,
            MontoRetenido = r.monto_retenido,
            EstadoId = r.estado_id,
            EstadoDescripcion = EstadoRetencion.Descripcion(r.estado_id)
        }).ToList();
    }

    public async Task<RetencionesDeclaracionImpresionDto> GetDatosDeclaracionImpresionAsync(
        RetencionDeclaracionFilterDto filtro, string impresoPor, CancellationToken ct = default)
    {
        filtro ??= new RetencionDeclaracionFilterDto();

        var items = (await BuscarDeclaracionAsync(filtro, ct)).ToList();

        // Empresa (agente retenedor): cfg_company NO es tenant-scoped → filtrar por la empresa actual.
        var companyId = _currentCompany.GetCompanyId();
        var company = await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        return new RetencionesDeclaracionImpresionDto
        {
            EmpresaNombre = company?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = company?.legal_name,
            EmpresaRtn = company?.tax_id,
            EmpresaDireccion = company?.address,
            EmpresaTelefono = company?.phone,
            EmpresaEmail = company?.email,
            EmpresaLogo = company?.logo,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),
            Items = items,
            FiltroTexto = BuildFiltroTexto(filtro)
        };
    }

    private static string BuildFiltroTexto(RetencionDeclaracionFilterDto f)
    {
        var rango = (f.Desde, f.Hasta) switch
        {
            ({ } d, { } h) => $"Del {d:dd/MM/yyyy} al {h:dd/MM/yyyy}",
            ({ } d, null) => $"Desde {d:dd/MM/yyyy}",
            (null, { } h) => $"Hasta {h:dd/MM/yyyy}",
            _ => "Sin límite de fechas"
        };

        var estado = f.EstadoId switch
        {
            EstadoRetencion.Vigente => "Vigentes",
            EstadoRetencion.Anulada => "Anuladas",
            _ => "Todas"
        };

        var partes = new List<string> { rango, $"Estado: {estado}" };
        if (!string.IsNullOrWhiteSpace(f.Search))
            partes.Add($"Búsqueda: {f.Search.Trim()}");

        return string.Join("  ·  ", partes);
    }

    private static RetencionRegistroListItemDto ToListItem(HdrRow x) => new()
    {
        RetencionHdrId = x.RetencionHdrId,
        Folio = x.Folio,
        FechaEmision = x.FechaEmision,
        NumeroOrden = x.NumeroOrden,
        NumeroAbono = x.NumeroAbono,
        Origen = x.Origen,
        OrigenDescripcion = OrigenRetencion.Descripcion(x.Origen),
        CodProveedor = x.CodProveedor,
        RtnProveedor = x.RtnProveedor,
        BaseTotal = x.BaseTotal,
        TotalRetenido = x.TotalRetenido,
        PartidaId = x.PartidaId,
        PolizaNumber = x.PolizaNumber,
        EstadoId = x.EstadoId,
        EstadoDescripcion = EstadoRetencion.Descripcion(x.EstadoId),
        MotivoAnulacion = x.MotivoAnulacion
    };

    // Proyección intermedia (traducible a SQL); la descripción del estado se resuelve luego en memoria.
    private sealed class HdrRow
    {
        public long RetencionHdrId { get; init; }
        public int Folio { get; init; }
        public DateOnly FechaEmision { get; init; }
        public int? NumeroOrden { get; init; }
        public int NumeroAbono { get; init; }
        public short Origen { get; init; }
        public int? CxpId { get; init; }
        public string? CodProveedor { get; init; }
        public string? RtnProveedor { get; init; }
        public decimal BaseTotal { get; init; }
        public decimal TotalRetenido { get; init; }
        public long? PartidaId { get; init; }
        public string? PolizaNumber { get; init; }
        public short EstadoId { get; init; }
        public string? MotivoAnulacion { get; init; }
    }
}
