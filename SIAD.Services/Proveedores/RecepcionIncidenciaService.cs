using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Implementación de la bitácora de incidencias de recepción. Dapper contra
/// <c>prv_recepcion_incidencia</c>, con la empresa siempre explícita (Dapper no pasa por el
/// filtro global de <see cref="SiadDbContext"/>).
/// </summary>
public sealed class RecepcionIncidenciaService : IRecepcionIncidenciaService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public RecepcionIncidenciaService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
        DapperTypeHandlers.EnsureRegistered();
    }

    public async Task<IReadOnlyList<RecepcionIncidenciaDto>> GetAsync(
        RecepcionIncidenciaFilterDto? filtro = null, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        filtro ??= new RecepcionIncidenciaFilterDto();

        var search = string.IsNullOrWhiteSpace(filtro.Search) ? null : $"%{filtro.Search.Trim()}%";
        var cod = string.IsNullOrWhiteSpace(filtro.CodProveedor) ? null : filtro.CodProveedor.Trim();

        // Los parámetros opcionales van CASTEADOS: con un filtro en NULL, Postgres no puede
        // inferir el tipo desde «@X IS NULL» y aborta con 42P08 ("no se pudo determinar el tipo
        // del parámetro"). El cast se lo dice de una vez.
        var sql = SelectBase + @"
             WHERE i.company_id = @CompanyId
               AND (@Cod::varchar    IS NULL OR h.cod_proveedor = @Cod::varchar)
               AND (@Desde::date     IS NULL OR i.fecha >= @Desde::date)
               AND (@Hasta::date     IS NULL OR i.fecha <= @Hasta::date)
               AND (@Tipo::smallint  IS NULL OR i.tipo = @Tipo::smallint)
               AND (@Compra::integer IS NULL OR i.compra_hdr_id = @Compra::integer)
               AND (@Search::text    IS NULL OR i.descripcion ILIKE @Search::text
                                             OR COALESCE(h.numero_factura_sar, '') ILIKE @Search::text
                                             OR COALESCE(h.proveedor, '') ILIKE @Search::text)
             ORDER BY i.fecha DESC, i.id DESC";

        var filas = await connection.QueryAsync<RecepcionIncidenciaDto>(new CommandDefinition(sql,
            new
            {
                CompanyId = companyId,
                CompanyIdInt = (int)companyId,
                Cod = cod,
                Desde = filtro.FechaDesde,
                Hasta = filtro.FechaHasta,
                Tipo = filtro.Tipo,
                Compra = filtro.CompraHdrId,
                Search = search
            }, TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<RecepcionIncidenciaDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        return await LeerAsync(connection, TransaccionActual(), companyId, id, ct);
    }

    public async Task<RecepcionIncidenciaDto> CrearAsync(
        RecepcionIncidenciaUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var descripcion = Validar(dto);
        var recepcion = await ExigirRecepcionAsync(connection, tx, companyId, dto.CompraHdrId, ct);
        var fecha = ResolverFecha(dto.Fecha, recepcion.Fecha);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.prv_recepcion_incidencia
                   (company_id, compra_hdr_id, fecha, tipo, articulo_id, cantidad, monto,
                    descripcion, usuariocreacion, fechacreacion)
            VALUES (@CompanyId, @CompraHdrId, @Fecha, @Tipo, @ArticuloId, @Cantidad, @Monto,
                    @Descripcion, @Usuario, @Ahora)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                dto.CompraHdrId,
                Fecha = fecha,
                dto.Tipo,
                dto.ArticuloId,
                dto.Cantidad,
                dto.Monto,
                Descripcion = descripcion,
                Usuario = Usuario(usuario),
                Ahora = Ahora()
            }, tx, cancellationToken: ct));

        return (await LeerAsync(connection, tx, companyId, id, ct))!;
    }

    public async Task<RecepcionIncidenciaDto> ActualizarAsync(
        int id, RecepcionIncidenciaUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var actual = await LeerAsync(connection, tx, companyId, id, ct)
            ?? throw new KeyNotFoundException("La incidencia no existe.");

        var descripcion = Validar(dto);
        var recepcion = await ExigirRecepcionAsync(connection, tx, companyId, dto.CompraHdrId, ct);
        var fecha = ResolverFecha(dto.Fecha, recepcion.Fecha);

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.prv_recepcion_incidencia
               SET compra_hdr_id = @CompraHdrId, fecha = @Fecha, tipo = @Tipo,
                   articulo_id = @ArticuloId, cantidad = @Cantidad, monto = @Monto,
                   descripcion = @Descripcion,
                   usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id",
            new
            {
                CompanyId = companyId,
                Id = actual.Id,
                dto.CompraHdrId,
                Fecha = fecha,
                dto.Tipo,
                dto.ArticuloId,
                dto.Cantidad,
                dto.Monto,
                Descripcion = descripcion,
                Usuario = Usuario(usuario),
                Ahora = Ahora()
            }, tx, cancellationToken: ct));

        return (await LeerAsync(connection, tx, companyId, id, ct))!;
    }

    public async Task<bool> EliminarAsync(int id, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        var filas = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.prv_recepcion_incidencia WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, TransaccionActual(), cancellationToken: ct));

        return filas > 0;
    }

    public async Task<IReadOnlyList<RecepcionIncidenciaLookupDto>> BuscarRecepcionesAsync(
        string codProveedor, string? search = null, CancellationToken ct = default)
    {
        var cod = (codProveedor ?? string.Empty).Trim();
        if (cod.Length == 0) return Array.Empty<RecepcionIncidenciaLookupDto>();

        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var like = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

        const string sql = @"
            SELECT h.id                 AS Id,
                   h.numero             AS Numero,
                   h.fecha              AS Fecha,
                   h.numero_factura_sar AS NumeroFacturaSar,
                   h.total              AS Total
              FROM public.alm_compra_hdr h
             WHERE h.company_id = @CompanyId
               AND h.cod_proveedor = @Cod
               AND h.estado <> @Anulada
               AND (@Search::text IS NULL OR COALESCE(h.numero_factura_sar, '') ILIKE @Search::text
                                          OR h.numero::text ILIKE @Search::text)
             ORDER BY h.fecha DESC, h.numero DESC
             LIMIT 200";

        var filas = await connection.QueryAsync<RecepcionIncidenciaLookupDto>(new CommandDefinition(sql,
            new
            {
                CompanyId = companyId,
                Cod = cod,
                Anulada = EstadoRecepcionCompra.Anulada,
                Search = like
            }, TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    // ── Piezas internas ──────────────────────────────────────────────────────

    /// <summary>
    /// SELECT compartido por el listado y la lectura puntual: la incidencia siempre viaja con los
    /// datos de su recepción y proveedor, que es lo que la vuelve legible en pantalla.
    /// </summary>
    private const string SelectBase = @"
            SELECT i.id                 AS Id,
                   i.compra_hdr_id      AS CompraHdrId,
                   h.numero             AS RecepcionNumero,
                   h.fecha              AS RecepcionFecha,
                   h.numero_factura_sar AS NumeroFacturaSar,
                   h.cod_proveedor      AS CodProveedor,
                   COALESCE(h.proveedor, p.nombre) AS ProveedorNombre,
                   i.fecha              AS Fecha,
                   i.tipo               AS Tipo,
                   i.articulo_id        AS ArticuloId,
                   a.descripcion        AS ArticuloDescripcion,
                   i.cantidad           AS Cantidad,
                   i.monto              AS Monto,
                   i.descripcion        AS Descripcion,
                   i.usuariocreacion    AS UsuarioCreacion,
                   i.fechacreacion      AS FechaCreacion
              FROM public.prv_recepcion_incidencia i
              JOIN public.alm_compra_hdr h
                ON h.company_id = i.company_id AND h.id = i.compra_hdr_id
              LEFT JOIN public.prv_proveedores p
                     ON p.company_id = @CompanyIdInt AND p.cod_proveedor = h.cod_proveedor
              LEFT JOIN public.alm_articulo a
                     ON a.company_id = i.company_id AND a.id = i.articulo_id";

    private static async Task<RecepcionIncidenciaDto?> LeerAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int id, CancellationToken ct)
    {
        var sql = SelectBase + " WHERE i.company_id = @CompanyId AND i.id = @Id";

        return await connection.QuerySingleOrDefaultAsync<RecepcionIncidenciaDto>(
            new CommandDefinition(sql,
                new { CompanyId = companyId, CompanyIdInt = (int)companyId, Id = id },
                tx, cancellationToken: ct));
    }

    /// <summary>Cotas de captura. Devuelve la descripción ya normalizada.</summary>
    private static string Validar(RecepcionIncidenciaUpsertDto dto)
    {
        if (dto.CompraHdrId <= 0)
        {
            throw new InvalidOperationException("Debe indicar la recepción afectada.");
        }

        var descripcion = (dto.Descripcion ?? string.Empty).Trim();
        if (descripcion.Length == 0)
        {
            throw new InvalidOperationException("Describa la incidencia.");
        }
        if (descripcion.Length > 500)
        {
            throw new InvalidOperationException("La descripción no puede superar 500 caracteres.");
        }

        if (dto.Tipo is not (TipoIncidenciaRecepcion.Devolucion
                          or TipoIncidenciaRecepcion.Dano
                          or TipoIncidenciaRecepcion.Especificacion
                          or TipoIncidenciaRecepcion.Faltante
                          or TipoIncidenciaRecepcion.Otro))
        {
            throw new InvalidOperationException("El tipo de incidencia no es válido.");
        }

        if (dto.Cantidad is < 0m) throw new InvalidOperationException("La cantidad no puede ser negativa.");
        if (dto.Monto is < 0m) throw new InvalidOperationException("El monto no puede ser negativo.");

        return descripcion;
    }

    /// <summary>
    /// La recepción debe existir en la empresa y NO estar anulada: una factura anulada sale del
    /// universo del scorecard, así que una incidencia colgada de ella nunca se contaría.
    /// </summary>
    private static async Task<RecepcionBasica> ExigirRecepcionAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int compraHdrId, CancellationToken ct)
    {
        var recepcion = await connection.QuerySingleOrDefaultAsync<RecepcionBasica>(new CommandDefinition(@"
            SELECT h.id AS Id, h.fecha AS Fecha, h.estado AS Estado
              FROM public.alm_compra_hdr h
             WHERE h.company_id = @CompanyId AND h.id = @Id",
            new { CompanyId = companyId, Id = compraHdrId }, tx, cancellationToken: ct));

        if (recepcion is null)
        {
            throw new InvalidOperationException("La recepción indicada no existe.");
        }
        if (recepcion.Estado == EstadoRecepcionCompra.Anulada)
        {
            throw new InvalidOperationException(
                "La recepción está anulada: no se le pueden registrar incidencias.");
        }

        return recepcion;
    }

    /// <summary>
    /// Fecha de detección. Vacía = la de la recepción. Nunca antes de recibir: una incidencia
    /// anterior a la entrega ensuciaría el período al que se le imputa.
    /// </summary>
    private static DateOnly ResolverFecha(DateOnly? fecha, DateOnly fechaRecepcion)
    {
        if (!fecha.HasValue) return fechaRecepcion;
        if (fecha.Value < fechaRecepcion)
        {
            throw new InvalidOperationException(
                "La fecha de la incidencia no puede ser anterior a la de la recepción.");
        }

        return fecha.Value;
    }

    private long EnsureCompanyId()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        return companyId;
    }

    private async Task<DbConnection> AbrirConexionAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }

    private DbTransaction? TransaccionActual() => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string Usuario(string? usuario)
        => string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario.Trim();

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private sealed class RecepcionBasica
    {
        public int Id { get; set; }
        public DateOnly Fecha { get; set; }
        public short Estado { get; set; }
    }
}
