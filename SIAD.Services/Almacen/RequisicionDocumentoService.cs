using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;
using SIAD.Services.Aprobaciones;

namespace SIAD.Services.Almacen;

/// <summary>
/// Documento de requisición. Ver <see cref="IRequisicionDocumentoService"/> y
/// <c>docs/plans/2026-08-04-requisiciones-descargos-fase6-plan.md</c>.
/// <para>
/// No toca el motor de kardex: una requisición es una solicitud. Sus renglones viven en la tabla
/// plana <c>alm_requisicion</c> con <c>origen='SIAD'</c> y <c>posteado=false</c> (el CHECK
/// <c>ck_alm_requisicion_no_postea</c> lo garantiza en la base). El descargo es el que postea.
/// </para>
/// </summary>
public sealed class RequisicionDocumentoService : IRequisicionDocumentoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IAprobacionService _aprobacion;

    public RequisicionDocumentoService(
        SiadDbContext context, ICurrentCompanyService company, IAprobacionService aprobacion)
    {
        _context = context;
        _company = company;
        _aprobacion = aprobacion;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lectura
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RequisicionDocumentoListItemDto>> GetAsync(
        RequisicionDocumentoFilterDto? filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new RequisicionDocumentoFilterDto();

        var query = from h in _context.alm_requisicion_hdrs.AsNoTracking()
                    join b in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals b.id
                    select new { h, b };

        if (f.BodegaId is > 0) query = query.Where(x => x.h.bodega_id == f.BodegaId);
        if (f.Estado.HasValue) query = query.Where(x => x.h.estado == f.Estado.Value);
        if (f.Tipo.HasValue) query = query.Where(x => x.h.tipo == f.Tipo.Value);
        if (f.Desde.HasValue) query = query.Where(x => x.h.fecha >= f.Desde.Value);
        if (f.Hasta.HasValue) query = query.Where(x => x.h.fecha <= f.Hasta.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            var numero = ClasificacionNormalizer.NumeroBuscado(f.Search);
            query = query.Where(x =>
                EF.Functions.ILike(x.h.solicitante, $"%{s}%")
                || (x.h.departamento != null && EF.Functions.ILike(x.h.departamento, $"%{s}%"))
                || (x.h.observacion != null && EF.Functions.ILike(x.h.observacion, $"%{s}%"))
                || x.h.numero.ToString().Contains(numero));
        }

        var rows = await query
            .OrderByDescending(x => x.h.fecha).ThenByDescending(x => x.h.numero)
            .Select(x => new
            {
                x.h.id, x.h.numero, x.h.fecha, x.h.tipo, x.h.estado,
                BodegaId = x.b.id, BodegaNombre = x.b.nombre,
                x.h.departamento, x.h.solicitante, x.h.total, x.h.usuariocreacion,
                Renglones = x.h.lineas.Count,
                Solicitado = x.h.lineas.Sum(l => (decimal?)l.cantidad) ?? 0m,
                Despachado = x.h.lineas.Sum(l => (decimal?)l.cantidad_despachada) ?? 0m
            })
            .ToListAsync(ct);

        return rows.Select(r => new RequisicionDocumentoListItemDto
        {
            Id = r.id,
            Numero = r.numero,
            Fecha = r.fecha,
            Tipo = r.tipo,
            Estado = r.estado,
            BodegaId = r.BodegaId,
            BodegaNombre = r.BodegaNombre,
            Departamento = r.departamento,
            Solicitante = r.solicitante,
            Total = r.total,
            Renglones = r.Renglones,
            PorcentajeDespachado = r.Solicitado > 0m
                ? Math.Round(r.Despachado / r.Solicitado * 100m, 1, MidpointRounding.AwayFromZero) : 0m,
            UsuarioCreacion = r.usuariocreacion
        }).ToList();
    }

    public async Task<RequisicionDocumentoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var fila = await (from h in _context.alm_requisicion_hdrs.AsNoTracking()
                          join b in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals b.id
                          where h.id == id
                          select new { h, b }).FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        var bodegaId = fila.h.bodega_id;
        var detalles = await (from l in _context.alm_requisicions.AsNoTracking()
                              join a in _context.alm_articulos.AsNoTracking() on l.articulo_id equals a.id into aj
                              from a in aj.DefaultIfEmpty()
                              from u in _context.alm_articulo_bodegas.AsNoTracking()
                                  .Where(u => u.articulo_id == l.articulo_id && u.bodega_id == bodegaId).DefaultIfEmpty()
                              where l.requisicion_hdr_id == id
                              orderby l.id
                              select new RequisicionDocumentoDetalleDto
                              {
                                  Id = l.id,
                                  ArticuloId = l.articulo_id ?? 0,
                                  CodigoArticulo = l.codigo_articulo,
                                  NombreArticulo = a == null ? null : a.descripcion,
                                  Descripcion = l.descripcion,
                                  Cantidad = l.cantidad,
                                  CantidadDespachada = l.cantidad_despachada,
                                  PrecioUnitario = l.precio_unitario,
                                  Total = l.total,
                                  CuentaContable = l.cuenta_contable,
                                  ExistenciaBodega = u == null ? null : u.existencia
                              }).ToListAsync(ct);

        return new RequisicionDocumentoDto
        {
            Id = fila.h.id,
            Numero = fila.h.numero,
            Tipo = fila.h.tipo,
            Estado = fila.h.estado,
            Fecha = fila.h.fecha,
            FechaRequerida = fila.h.fecha_requerida,
            BodegaId = bodegaId,
            BodegaNombre = fila.b.nombre,
            Departamento = fila.h.departamento,
            Solicitante = fila.h.solicitante,
            CargoSolicitante = fila.h.cargo_solicitante,
            Aplicacion = fila.h.aplicacion,
            Observacion = fila.h.observacion,
            AprobadoPor = fila.h.aprobado_por,
            FechaAprobacion = fila.h.fecha_aprobacion,
            RechazadoPor = fila.h.rechazado_por,
            FechaRechazo = fila.h.fecha_rechazo,
            MotivoRechazo = fila.h.motivo_rechazo,
            AnuladoPor = fila.h.anulado_por,
            FechaAnulacion = fila.h.fecha_anulacion,
            MotivoAnulacion = fila.h.motivo_anulacion,
            Total = fila.h.total,
            Uuid = fila.h.uuid,
            UsuarioCreacion = fila.h.usuariocreacion,
            FechaCreacion = fila.h.fechacreacion,
            Detalles = detalles
        };
    }

    public async Task<RequisicionImpresionDto?> GetDatosImpresionAsync(int id, string impresoPor, CancellationToken ct = default)
    {
        var doc = await GetByIdAsync(id, ct);
        if (doc is null) return null;

        var empresa = await CargarEmpresaAsync(ct);
        return new RequisicionImpresionDto
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = ClasificacionNormalizer.Usuario(impresoPor),
            Documento = doc,
            MontoEnLetras = doc.Total > 0m ? NumerosALetras.Convertir(doc.Total) : string.Empty
        };
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Alta y edición del borrador
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<RequisicionDocumentoDto> CrearAsync(RequisicionDocumentoDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        if (!dto.Uuid.HasValue || dto.Uuid.Value == Guid.Empty)
            throw new InvalidOperationException("La requisición debe traer un identificador de operación estable (uuid).");

        var ya = await _context.alm_requisicion_hdrs.AsNoTracking().FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
        if (ya is not null) return (await GetByIdAsync(ya.id, ct))!;

        var (solicitante, _, pares) = await ValidarCabeceraYRenglonesAsync(dto, ct);

        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        int nuevoId;
        try
        {
            await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

            var numero = await SiguienteNumeroAsync(companyId, ct);

            var hdr = new alm_requisicion_hdr
            {
                numero = numero,
                tipo = 1,   // solo salida de bodega en esta entrega (el tipo 2 se rechazó en la validación)
                estado = EstadoRequisicionHdr.Borrador,
                fecha = fecha,
                fecha_requerida = dto.FechaRequerida,
                bodega_id = dto.BodegaId,
                departamento = Limpiar(dto.Departamento, 80),
                solicitante = solicitante,
                cargo_solicitante = Limpiar(dto.CargoSolicitante, 80),
                usuario_solicita = usuario,
                aplicacion = Limpiar(dto.Aplicacion, 254),
                observacion = Limpiar(dto.Observacion, 1000),
                total = 0m,
                uuid = dto.Uuid.Value,
                usuariocreacion = usuario,
                fechacreacion = ahora
            };
            _context.alm_requisicion_hdrs.Add(hdr);
            await _context.SaveChangesAsync(ct);   // necesita id para las líneas

            hdr.total = CrearLineas(dto, hdr, pares, numero);
            await _context.SaveChangesAsync(ct);

            await TransaccionAmbiente.ConfirmarAsync(tx, ct);
            nuevoId = hdr.id;
        }
        catch (DbUpdateException)
        {
            // Carrera: entre el corte de idempotencia y el INSERT, otra petición creó la requisición
            // con este uuid (índice único). Si ya existe, es idempotente: devolver la existente.
            var existente = await _context.alm_requisicion_hdrs.AsNoTracking()
                .FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
            if (existente is null) throw;
            return (await GetByIdAsync(existente.id, ct))!;
        }
        return (await GetByIdAsync(nuevoId, ct))!;
    }

    public async Task<RequisicionDocumentoDto> ActualizarAsync(int id, RequisicionDocumentoDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        var hdr = await _context.alm_requisicion_hdrs.FirstOrDefaultAsync(h => h.id == id, ct)
            ?? throw new InvalidOperationException("La requisición no existe en la empresa actual.");
        // Editable mientras no se haya aprobado/despachado: borrador o en revisión (aún sin salida de inventario).
        if (hdr.estado != EstadoRequisicionHdr.Borrador && hdr.estado != EstadoRequisicionHdr.EnRevision)
            throw new InvalidOperationException("Solo se puede editar una requisición en borrador o en revisión.");

        var (solicitante, _, pares) = await ValidarCabeceraYRenglonesAsync(dto, ct);
        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        hdr.tipo = dto.Tipo == 2 ? (short)2 : (short)1;
        hdr.fecha = dto.Fecha ?? hdr.fecha;
        hdr.fecha_requerida = dto.FechaRequerida;
        hdr.bodega_id = dto.BodegaId;
        hdr.departamento = Limpiar(dto.Departamento, 80);
        hdr.solicitante = solicitante;
        hdr.cargo_solicitante = Limpiar(dto.CargoSolicitante, 80);
        hdr.aplicacion = Limpiar(dto.Aplicacion, 254);
        hdr.observacion = Limpiar(dto.Observacion, 1000);
        hdr.usuariomodificacion = usuario;
        hdr.fechamodificacion = ahora;

        // Reemplazo de renglones: en borrador y en revisión aún no hay descargos, así que borrar e insertar es seguro.
        var viejas = await _context.alm_requisicions.Where(l => l.requisicion_hdr_id == id).ToListAsync(ct);
        _context.alm_requisicions.RemoveRange(viejas);
        await _context.SaveChangesAsync(ct);

        hdr.total = CrearLineas(dto, hdr, pares, hdr.numero);
        await _context.SaveChangesAsync(ct);

        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return (await GetByIdAsync(hdr.id, ct))!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Transiciones de estado
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<RequisicionDocumentoDto> EnviarARevisionAsync(int id, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);
        var hdr = await BloquearHdrAsync(id, companyId, ct);   // FOR UPDATE: re-lee el estado bajo candado
        if (hdr.estado != EstadoRequisicionHdr.Borrador)
            throw new InvalidOperationException("Solo se puede enviar a revisión una requisición en borrador.");
        if (!await _context.alm_requisicions.AsNoTracking().AnyAsync(l => l.requisicion_hdr_id == id, ct))
            throw new InvalidOperationException("La requisición no tiene renglones.");

        hdr.estado = EstadoRequisicionHdr.EnRevision;
        SellarModificacion(hdr, user);
        await _context.SaveChangesAsync(ct);

        // Con la aprobación por monto encendida, "en revisión" queda esperando UNA autorización
        // de quien tenga límite suficiente. Apagada, el flujo es el histórico y esto no hace nada.
        if (await _aprobacion.RequiereAprobacionAsync(DocumentosAprobacion.Requisicion, ct))
        {
            await _aprobacion.IniciarAsync(
                DocumentosAprobacion.Requisicion, hdr.id, hdr.numero.ToString("00000"),
                hdr.total, hdr.usuario_solicita,
                EstadoRequisicionHdr.Borrador, EstadoRequisicionHdr.EnRevision, ct);
        }

        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<RequisicionDocumentoDto> AprobarAsync(int id, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);
        var hdr = await BloquearHdrAsync(id, companyId, ct);
        if (hdr.estado != EstadoRequisicionHdr.EnRevision)
            throw new InvalidOperationException("Solo se puede aprobar una requisición en revisión.");

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Con el control encendido, aprobar es AUTORIZAR por monto: el motor exige que el límite
        // del usuario cubra el total y que no sea su propia requisición. NO hay cascada, así que
        // esa única autorización la deja aprobada.
        if (await _aprobacion.RequiereAprobacionAsync(DocumentosAprobacion.Requisicion, ct))
        {
            await _aprobacion.AutorizarAsync(
                DocumentosAprobacion.Requisicion, id, hdr.total, null,
                EstadoRequisicionHdr.EnRevision, EstadoRequisicionHdr.Aprobada, ct);
        }

        hdr.estado = EstadoRequisicionHdr.Aprobada;
        hdr.aprobado_por = usuario;
        hdr.fecha_aprobacion = ahora;
        hdr.usuariomodificacion = usuario;
        hdr.fechamodificacion = ahora;
        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<RequisicionDocumentoDto> RechazarAsync(int id, string motivo, string user, CancellationToken ct = default)
    {
        var motivoR = (motivo ?? string.Empty).Trim();
        if (motivoR.Length == 0) throw new InvalidOperationException("Debe indicar el motivo del rechazo.");
        if (motivoR.Length > 500) throw new InvalidOperationException("El motivo no puede superar los 500 caracteres.");

        var companyId = _company.GetCompanyId();
        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);
        var hdr = await BloquearHdrAsync(id, companyId, ct);
        if (hdr.estado != EstadoRequisicionHdr.EnRevision)
            throw new InvalidOperationException("Solo se puede rechazar una requisición en revisión.");

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Rechazar exige la misma capacidad que aprobar: quien no podría autorizar el monto
        // tampoco puede tumbar la requisición. Sin el control encendido, el rechazo es el de siempre.
        if (await _aprobacion.RequiereAprobacionAsync(DocumentosAprobacion.Requisicion, ct))
        {
            await _aprobacion.RechazarAsync(
                DocumentosAprobacion.Requisicion, id, hdr.total, motivoR,
                EstadoRequisicionHdr.EnRevision, EstadoRequisicionHdr.Rechazada, ct);
        }

        hdr.estado = EstadoRequisicionHdr.Rechazada;
        hdr.rechazado_por = usuario;
        hdr.fecha_rechazo = ahora;
        hdr.motivo_rechazo = motivoR;
        hdr.usuariomodificacion = usuario;
        hdr.fechamodificacion = ahora;
        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return (await GetByIdAsync(id, ct))!;
    }

    public async Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default)
    {
        var motivoA = (motivo ?? string.Empty).Trim();
        if (motivoA.Length == 0) throw new InvalidOperationException("Debe indicar el motivo de la anulación.");
        if (motivoA.Length > 500) throw new InvalidOperationException("El motivo no puede superar los 500 caracteres.");

        var companyId = _company.GetCompanyId();
        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);
        var hdr = await BloquearHdrOrNullAsync(id, companyId, ct);   // FOR UPDATE: estado y despacho bajo candado
        if (hdr is null) return false;
        if (hdr.estado == EstadoRequisicionHdr.Anulada) return true;   // idempotente

        // No se anula una requisición con material ya entregado: eso se corrige anulando el descargo.
        if (await _context.alm_requisicions.AsNoTracking()
                .AnyAsync(l => l.requisicion_hdr_id == id && l.cantidad_despachada > 0m, ct))
            throw new InvalidOperationException(
                "No se puede anular: la requisición ya tiene entregas. Anule primero los descargos.");

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        hdr.estado = EstadoRequisicionHdr.Anulada;
        hdr.anulado_por = usuario;
        hdr.fecha_anulacion = ahora;
        hdr.motivo_anulacion = motivoA;
        hdr.usuariomodificacion = usuario;
        hdr.fechamodificacion = ahora;
        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Apoyo
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record ParArticulo(int ArticuloId, string? Codigo);

    private async Task<(string Solicitante, alm_bodega Bodega, Dictionary<int, ParArticulo> Pares)>
        ValidarCabeceraYRenglonesAsync(RequisicionDocumentoDto dto, CancellationToken ct)
    {
        // El reabastecimiento (tipo 2 → orden de compra) está fuera de alcance de esta entrega
        // (decisión D-12): rechazarlo evita documentos varados que luego chocarían con el CHECK al
        // intentar despacharse.
        if (dto.Tipo == 2)
            throw new InvalidOperationException(
                "El reabastecimiento (tipo 2) no está habilitado en esta entrega; use una requisición de salida de bodega (tipo 1).");

        var solicitante = (dto.Solicitante ?? string.Empty).Trim();
        if (solicitante.Length == 0) throw new InvalidOperationException("El solicitante es obligatorio.");
        if (solicitante.Length > 120) throw new InvalidOperationException("El solicitante no puede superar los 120 caracteres.");

        if (dto.BodegaId <= 0) throw new InvalidOperationException("Debe indicar la bodega de la que saldrá la mercadería.");
        var bodega = await _context.alm_bodegas.AsNoTracking().FirstOrDefaultAsync(b => b.id == dto.BodegaId, ct)
            ?? throw new InvalidOperationException("La bodega no existe en la empresa actual.");
        if (!bodega.activo) throw new InvalidOperationException($"La bodega '{bodega.nombre}' está inactiva.");

        if (dto.Detalles is null || dto.Detalles.Count == 0)
            throw new InvalidOperationException("La requisición debe tener al menos un renglón.");
        for (var i = 0; i < dto.Detalles.Count; i++)
        {
            var d = dto.Detalles[i];
            if (d.ArticuloId <= 0) throw new InvalidOperationException($"El renglón {i + 1} no tiene artículo.");
            if (d.Cantidad <= 0m) throw new InvalidOperationException($"El renglón {i + 1}: la cantidad debe ser mayor que cero.");
        }

        // Se permiten artículos repetidos (cada renglón es una línea de solicitud independiente; su
        // parcialidad se controla por línea). Se valida que el artículo exista, esté activo y esté
        // ASIGNADO a la bodega.
        var ids = dto.Detalles.Select(d => d.ArticuloId).Distinct().ToList();
        var arts = await _context.alm_articulos.AsNoTracking()
            .Where(a => ids.Contains(a.id))
            .Select(a => new { a.id, a.codigo_articulo, a.activo })
            .ToListAsync(ct);

        // Asignado = tiene una ubicación activa en esa bodega (alm_articulo_bodega), aunque su
        // existencia sea 0. La requisición es una SOLICITUD, no la salida: no se exige stock.
        var asignadosBodega = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.activo && u.bodega_id == dto.BodegaId && ids.Contains(u.articulo_id))
            .Select(u => u.articulo_id)
            .ToListAsync(ct);

        var pares = new Dictionary<int, ParArticulo>();
        foreach (var artId in ids)
        {
            var a = arts.FirstOrDefault(x => x.id == artId)
                ?? throw new InvalidOperationException($"El artículo {artId} no existe en la empresa actual.");
            if (!a.activo) throw new InvalidOperationException($"El artículo {a.codigo_articulo ?? artId.ToString()} está inactivo.");
            if (!asignadosBodega.Contains(artId))
                throw new InvalidOperationException($"El artículo {a.codigo_articulo ?? artId.ToString()} no está asignado a la bodega '{bodega.nombre}'.");
            pares[artId] = new ParArticulo(artId, a.codigo_articulo);
        }
        return (solicitante, bodega, pares);
    }

    private decimal CrearLineas(RequisicionDocumentoDto dto, alm_requisicion_hdr hdr,
        Dictionary<int, ParArticulo> pares, int numero)
    {
        decimal total = 0m;
        foreach (var d in dto.Detalles)
        {
            var totalLinea = Math.Round(d.Cantidad * d.PrecioUnitario, 2, MidpointRounding.AwayFromZero);
            total += totalLinea;
            _context.alm_requisicions.Add(new alm_requisicion
            {
                numero = numero,
                codigo_articulo = pares[d.ArticuloId].Codigo,
                articulo_id = d.ArticuloId,
                descripcion = Limpiar(d.Descripcion, 200),
                aplicacion = hdr.aplicacion,
                cantidad = d.Cantidad,
                precio_unitario = d.PrecioUnitario,
                valor = totalLinea,
                total = totalLinea,
                tipo_requisicion = hdr.tipo,
                departamento = hdr.departamento,
                solicitante = hdr.solicitante,
                cargo_solicitante = hdr.cargo_solicitante,
                cuenta_contable = Limpiar(d.CuentaContable, 30),
                fecha_requisicion = hdr.fecha,
                estatus = EstadoRequisicion.Pendiente,
                aprobado = false,
                rechazado = false,
                descargado = false,
                bodega_id = hdr.bodega_id,
                requisicion_hdr_id = hdr.id,
                cantidad_despachada = 0m,
                aplicado_en_oc = false,
                uuid = Guid.NewGuid(),
                origen = OrigenDocumento.Siad,
                posteado = false    // la requisición NUNCA postea (ck_alm_requisicion_no_postea)
            });
        }
        return Math.Round(total, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Bloquea la cabecera con <c>FOR UPDATE</c> (o null si no existe): las transiciones de
    /// estado releen el estado bajo candado, evitando que aprobar y rechazar concurrentes dejen un
    /// registro contradictorio. El <c>company_id</c> va dentro del SQL (filtro de tenant).</summary>
    private async Task<alm_requisicion_hdr?> BloquearHdrOrNullAsync(int id, long companyId, CancellationToken ct)
        => await _context.alm_requisicion_hdrs
            .FromSqlInterpolated($@"SELECT * FROM alm_requisicion_hdr WHERE company_id = {companyId} AND id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(ct);

    private async Task<alm_requisicion_hdr> BloquearHdrAsync(int id, long companyId, CancellationToken ct)
        => await BloquearHdrOrNullAsync(id, companyId, ct)
           ?? throw new InvalidOperationException("La requisición no existe en la empresa actual.");

    private static void SellarModificacion(alm_requisicion_hdr hdr, string user)
    {
        hdr.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        hdr.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    private async Task<int> SiguienteNumeroAsync(long companyId, CancellationToken ct)
    {
        // ON CONFLICT DO NOTHING: si el correlativo ya fue sembrado desde el histórico (17124), lo
        // respeta; no lo pisa a 0. Luego SELECT ... FOR UPDATE serializa dos altas concurrentes.
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alm_requisicion_correlativo (company_id, ultimo_numero)
            VALUES ({companyId}, 0)
            ON CONFLICT (company_id) DO NOTHING", ct);

        var contador = await _context.alm_requisicion_correlativos
            .FromSqlInterpolated($@"
                SELECT * FROM alm_requisicion_correlativo
                 WHERE company_id = {companyId}
                 FOR UPDATE")
            .FirstAsync(ct);

        contador.ultimo_numero += 1;
        return contador.ultimo_numero;
    }

    private static string? Limpiar(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        return limpio.Length > maximo ? limpio[..maximo] : limpio;
    }
}
