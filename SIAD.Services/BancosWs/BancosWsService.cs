using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.BancosWs;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.BancosWs;

public sealed class BancosWsService : IBancosWsService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public BancosWsService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<BancosWsCredencialDto?> AutenticarAsync(string? banco, string? llave, CancellationToken ct = default)
    {
        var bancoNormalizado = banco?.Trim();
        var llaveNormalizada = llave?.Trim();
        if (string.IsNullOrEmpty(bancoNormalizado) || string.IsNullOrEmpty(llaveNormalizada))
        {
            return null;
        }

        var connection = await AbrirAsync(ct);
        // Consulta global (sin tenant): la credencial ES la que resuelve la empresa.
        const string sql = @"
            SELECT company_id AS CompanyId, banco AS Banco,
                   banco_cuenta_id AS BancoCuentaId, vigencia AS Vigencia
            FROM public.ban_ws_credencial
            WHERE banco = @Banco AND llave = @Llave AND activo
            ORDER BY credencial_id
            LIMIT 1;";

        return await connection.QueryFirstOrDefaultAsync<BancosWsCredencialDto>(
            new CommandDefinition(sql, new { Banco = bancoNormalizado, Llave = llaveNormalizada }, cancellationToken: ct));
    }

    public async Task<BancosWsConsultaDto> ConsultarAsync(string clave, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var claveNormalizada = (clave ?? string.Empty).Trim();
        var connection = await AbrirAsync(ct);

        var cliente = await connection.QueryFirstOrDefaultAsync<(string Clave, string Nombre, string Direccion)>(
            new CommandDefinition(
                "SELECT clave, nombre, direccion FROM public.fn_ban_ws_cliente(@CompanyId, @Clave);",
                new { CompanyId = companyId, Clave = claveNormalizada },
                cancellationToken: ct));

        if (cliente.Clave is null)
        {
            return new BancosWsConsultaDto { Resultado = BancosWsConsultaResultado.SinRegistro };
        }

        var lineas = (await connection.QueryAsync<PendienteRow>(
            new CommandDefinition(
                @"SELECT factura_id AS FacturaId, numrecibo AS NumRecibo, fechaemision AS FechaEmision,
                         fechavence AS FechaVence, detalle_id AS DetalleId, codigo AS Codigo,
                         tiposervicio AS TipoServicio, descripcion AS Descripcion, saldo AS Saldo
                  FROM public.fn_ban_ws_pendientes(@CompanyId, @Clave);",
                new { CompanyId = companyId, Clave = claveNormalizada },
                cancellationToken: ct))).ToList();

        var total = lineas.Sum(l => l.Saldo);
        if (lineas.Count == 0 || total <= 0)
        {
            return new BancosWsConsultaDto { Resultado = BancosWsConsultaResultado.SinPendientes };
        }

        // Regla SIMAFI replicada (contrato §5.2): la vigencia del cobro es la de
        // la factura VIGENTE (la más reciente — en SIMAFI la factura del mes
        // arrastra el saldo anterior y su `vence` manda). Vencida ⇒ 400.
        var fechaVenceVigente = lineas
            .OrderByDescending(l => l.FechaEmision)
            .ThenByDescending(l => l.NumRecibo)
            .Select(l => l.FechaVence)
            .FirstOrDefault();
        var hoy = DateTime.Today;
        if (fechaVenceVigente.HasValue && fechaVenceVigente.Value.Date < hoy)
        {
            return new BancosWsConsultaDto { Resultado = BancosWsConsultaResultado.Vencidas };
        }

        return new BancosWsConsultaDto
        {
            Resultado = BancosWsConsultaResultado.Ok,
            Clave = cliente.Clave,
            Nombre = cliente.Nombre ?? string.Empty,
            Direccion = cliente.Direccion ?? string.Empty,
            Total = Math.Round(total, 2, MidpointRounding.AwayFromZero),
            FechaVence = "N",
            Detalles = lineas.Select(l => new BancosWsDetalleDto
            {
                Id = l.DetalleId,
                CodigoConcepto = string.IsNullOrWhiteSpace(l.Codigo) ? (l.TipoServicio ?? string.Empty).Trim() : l.Codigo.Trim(),
                Concepto = (l.Descripcion ?? string.Empty).Trim(),
                Valor = Math.Round(l.Saldo, 2, MidpointRounding.AwayFromZero)
            }).ToList()
        };
    }

    public async Task<BancosWsPagoResultDto> PagarAsync(BancosWsPagoRequestDto pago, long? bancoCuentaId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pago);
        var companyId = _currentCompanyService.GetCompanyId();
        var connection = await AbrirAsync(ct);

        const string sql = @"
            SELECT status AS Status, pago_id AS PagoId, poliza_id AS PolizaId,
                   ban_kardex_id AS BanKardexId, total_pendiente AS TotalPendiente
            FROM public.sp_ban_ws_pagar(
                @CompanyId, @Banco, @Referencia, @Clave, @Monto,
                @FechaRegistro::date, @HoraRegistro::time, @FechaEfectiva::date, @Sucursal, @Cajero,
                @BancoCuentaId, @Tipo, @ValidarMonto, @Usuario);";

        var resultado = await connection.QueryFirstAsync<BancosWsPagoResultDto>(
            new CommandDefinition(sql, new
            {
                CompanyId = companyId,
                Banco = pago.Banco?.Trim(),
                Referencia = pago.Referencia?.Trim(),
                Clave = pago.Clave?.Trim(),
                Monto = pago.Monto,
                FechaRegistro = pago.FechaRegistro.ToDateTime(TimeOnly.MinValue),
                HoraRegistro = pago.HoraRegistro?.ToTimeSpan(),
                FechaEfectiva = pago.FechaEfectiva?.ToDateTime(TimeOnly.MinValue),
                Sucursal = pago.Sucursal,
                Cajero = pago.Cajero,
                BancoCuentaId = bancoCuentaId,
                Tipo = pago.Tipo,
                ValidarMonto = pago.ValidarMonto,
                Usuario = "wsbanco"
            }, cancellationToken: ct));

        return resultado;
    }

    public async Task<BancosWsReversionResultDto> ReversarAsync(string referencia, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var connection = await AbrirAsync(ct);

        const string sql = @"
            SELECT status AS Status, pago_id AS PagoId, poliza_reverso_id AS PolizaReversoId,
                   ban_kardex_reverso_id AS BanKardexReversoId
            FROM public.sp_ban_ws_reversar(@CompanyId, @Referencia, @Usuario);";

        return await connection.QueryFirstAsync<BancosWsReversionResultDto>(
            new CommandDefinition(sql, new
            {
                CompanyId = companyId,
                Referencia = (referencia ?? string.Empty).Trim(),
                Usuario = "wsbanco"
            }, cancellationToken: ct));
    }

    public async Task<bool> GenerarLlaveAsync(string? banco, string? vigencia, CancellationToken ct = default)
    {
        var bancoNormalizado = banco?.Trim();
        if (string.IsNullOrEmpty(bancoNormalizado))
        {
            return false;
        }

        // Semántica SIMAFI (SrvAutorizacion/Control.registrarNuevaClaveBanco):
        // llave = 40 hex MAYÚSCULAS; vigencia se guarda tal cual (NULL si vacía)
        // y NUNCA se valida. La llave nueva NO viaja en la respuesta: se lee de
        // la BD y se comunica fuera de línea.
        DateTime? vigenciaFecha = null;
        if (!string.IsNullOrWhiteSpace(vigencia) && DateTime.TryParse(vigencia.Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed))
        {
            vigenciaFecha = parsed.Date;
        }

        var llave = Convert.ToHexString(RandomNumberGenerator.GetBytes(20)); // 40 hex uppercase

        var connection = await AbrirAsync(ct);

        // genkey no trae llave: solo aplica si el banco identifica UNA sola
        // credencial activa (con más de un tenant con el mismo código no se
        // puede desambiguar y se responde el 400 del contrato).
        const string sql = @"
            WITH candidatas AS (
                SELECT credencial_id FROM public.ban_ws_credencial
                WHERE banco = @Banco AND activo
            )
            UPDATE public.ban_ws_credencial c
            SET llave = @Llave,
                vigencia = @Vigencia::date,
                updated_at = now(),
                updated_by = 'ws-genkey'
            FROM candidatas
            WHERE c.credencial_id = candidatas.credencial_id
              AND (SELECT COUNT(*) FROM candidatas) = 1;";

        var afectadas = await connection.ExecuteAsync(
            new CommandDefinition(sql, new { Banco = bancoNormalizado, Llave = llave, Vigencia = vigenciaFecha }, cancellationToken: ct));

        return afectadas == 1;
    }

    public async Task<(bool Existe, DateTime? Vigencia)> ObtenerVigenciaAsync(string? banco, CancellationToken ct = default)
    {
        var bancoNormalizado = banco?.Trim();
        if (string.IsNullOrEmpty(bancoNormalizado))
        {
            return (false, null);
        }

        var connection = await AbrirAsync(ct);
        var filas = (await connection.QueryAsync<DateTime?>(
            new CommandDefinition(
                "SELECT vigencia FROM public.ban_ws_credencial WHERE banco = @Banco AND activo ORDER BY credencial_id LIMIT 1;",
                new { Banco = bancoNormalizado }, cancellationToken: ct))).ToList();

        return filas.Count == 0 ? (false, null) : (true, filas[0]);
    }

    private async Task<System.Data.Common.DbConnection> AbrirAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }

    // Fechas como DateTime: Dapper materializa date de PostgreSQL como DateTime.
    private sealed class PendienteRow
    {
        public long FacturaId { get; init; }
        public int NumRecibo { get; init; }
        public DateTime? FechaEmision { get; init; }
        public DateTime? FechaVence { get; init; }
        public long DetalleId { get; init; }
        public string? Codigo { get; init; }
        public string? TipoServicio { get; init; }
        public string? Descripcion { get; init; }
        public decimal Saldo { get; init; }
    }
}
