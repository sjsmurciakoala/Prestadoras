using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.MobileApi;
using SIAD.Data;

namespace SIAD.Services.MobileApi;

/// <summary>
/// Implementación del servicio de la API móvil de lectores (L3). Puente Dapper
/// sobre los MISMOS SPs V3 del WS viejo; la orquestación (snapshot por cliente,
/// flujo CAI de la subida de lectura) replica la del WS. Auth propia
/// (adm_lector_credencial/_sesion, bcrypt vía pgcrypto).
/// </summary>
public sealed class LectoresMobileService : ILectoresMobileService
{
    private const string Usuario = "mobileapi";
    private const int CaiBloqueCantidad = 250;

    private static readonly JsonSerializerOptions JsonSnake = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
    };

    private readonly SiadDbContext _context;

    public LectoresMobileService(SiadDbContext context)
    {
        _context = context;
    }

    // -------------------------------------------------------------------------
    // Autenticación / sesión
    // -------------------------------------------------------------------------

    public async Task<LectorSesionContexto?> ValidarSesionAsync(string? token, CancellationToken ct = default)
    {
        var t = token?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return null;
        }

        var conn = await AbrirAsync(ct);
        const string sql = @"
            SELECT s.company_id AS CompanyId, s.credencial_id AS CredencialId,
                   c.codigo AS Codigo, c.lector_nombre AS Nombre,
                   coalesce(c.ruta, '') AS Ruta, c.codciclo AS CodCiclo
            FROM public.adm_lector_sesion s
            JOIN public.adm_lector_credencial c ON c.credencial_id = s.credencial_id
            WHERE s.token = @Token AND s.revocada_at IS NULL
              AND s.expira_at > now() AND c.activo
            LIMIT 1;";

        return await conn.QueryFirstOrDefaultAsync<LectorSesionContexto>(
            new CommandDefinition(sql, new { Token = t }, cancellationToken: ct));
    }

    public async Task<LoginRespuesta?> LoginAsync(LoginRequest request, TimeSpan duracion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var codigo = request.Codigo?.Trim();
        var clave = request.Clave;
        if (string.IsNullOrEmpty(codigo) || string.IsNullOrEmpty(clave))
        {
            return null;
        }

        var conn = await AbrirAsync(ct);
        // Validación global (sin tenant): la credencial resuelve la empresa (A6).
        // bcrypt: crypt(@Clave, clave_hash) == clave_hash sólo si la clave es correcta.
        const string sqlCred = @"
            SELECT credencial_id AS CredencialId, company_id AS CompanyId, codigo AS Codigo,
                   coalesce(lector_nombre, '') AS Nombre, coalesce(ruta, '') AS Ruta, codciclo AS CodCiclo
            FROM public.adm_lector_credencial
            WHERE lower(codigo) = lower(@Codigo) AND activo
              AND clave_hash = crypt(@Clave, clave_hash);";

        var candidatas = (await conn.QueryAsync<CredencialRow>(
            new CommandDefinition(sqlCred, new { Codigo = codigo, Clave = clave }, cancellationToken: ct))).ToList();

        // Cero o ambigua (mismo codigo+clave en dos tenants): no se autentica.
        if (candidatas.Count != 1)
        {
            return null;
        }

        var cred = candidatas[0];
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); // 64 hex

        const string sqlSesion = @"
            INSERT INTO public.adm_lector_sesion
                (company_id, credencial_id, token, dispositivo, expira_at)
            VALUES (@CompanyId, @CredencialId, @Token, @Dispositivo, now() + @Duracion)
            RETURNING expira_at;";

        var expira = await conn.ExecuteScalarAsync<DateTime>(new CommandDefinition(sqlSesion, new
        {
            cred.CompanyId,
            cred.CredencialId,
            Token = token,
            Dispositivo = string.IsNullOrWhiteSpace(request.Dispositivo) ? null : request.Dispositivo.Trim(),
            Duracion = duracion,
        }, cancellationToken: ct));

        return new LoginRespuesta
        {
            Token = token,
            ExpiraAt = expira,
            Lector = new LectorPerfilDto
            {
                Codigo = cred.Codigo,
                Nombre = cred.Nombre,
                Ruta = cred.Ruta,
                CodCiclo = cred.CodCiclo,
            },
        };
    }

    public async Task LogoutAsync(string? token, CancellationToken ct = default)
    {
        var t = token?.Trim();
        if (string.IsNullOrEmpty(t))
        {
            return;
        }

        var conn = await AbrirAsync(ct);
        await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE public.adm_lector_sesion SET revocada_at = now() WHERE token = @Token AND revocada_at IS NULL;",
            new { Token = t }, cancellationToken: ct));
    }

    // -------------------------------------------------------------------------
    // Ciclo (paridad GetCiclo: CTE V3 + fallback sp_informacion_ciclo)
    // -------------------------------------------------------------------------

    public async Task<InformacionCicloDto> GetCicloAsync(string ruta, CancellationToken ct = default)
    {
        var rutaNorm = NormalizarRuta(ruta);
        var conn = await AbrirAsync(ct);

        const string sqlV3 = @"
            with ruta_clientes as (
                select distinct
                    cm.ciclos_id,
                    coalesce(nullif(btrim(c.ciclos_codigo), ''), lpad(cm.ciclos_id::text, 2, '0')) as ciclo_codigo
                from cliente_maestro cm
                left join ciclos c on c.ciclos_id = cm.ciclos_id
                where cm.estado = true
                  and (
                        split_part(cm.maestro_cliente_indicativo_ruta, '-', 3) = @p_ruta
                        or cm.maestro_cliente_indicativo_ruta = @p_ruta
                      )
            ),
            periodos_abiertos as (
                select hm.ano, hm.mes, btrim(hm.ciclo) as ciclo,
                       coalesce(hm.fechaperiodo, hm.fecha::date, now()::date) as fecha_periodo
                from historialmes hm
                where hm.cerrarperiodo = 'P' and hm.cerrado = 'A'
            )
            select pa.ano as r_ano, pa.mes as r_mes, pa.ciclo as r_ciclo
            from periodos_abiertos pa
            inner join ruta_clientes rc
                on (
                    pa.ciclo = rc.ciclo_codigo
                    or pa.ciclo = ltrim(rc.ciclo_codigo, '0')
                    or lpad(pa.ciclo, 2, '0') = rc.ciclo_codigo
                    or (pa.ciclo ~ '^[0-9]+$' and rc.ciclos_id is not null and pa.ciclo::int = rc.ciclos_id)
                )
            order by pa.ano desc, pa.mes desc, pa.fecha_periodo desc
            limit 1;";

        var row = await conn.QueryFirstOrDefaultAsync<CicloRow>(
            new CommandDefinition(sqlV3, new { p_ruta = rutaNorm }, cancellationToken: ct));

        if (row is null)
        {
            // Fallback legacy: sp_informacion_ciclo(ruta).
            row = await conn.QueryFirstOrDefaultAsync<CicloRow>(new CommandDefinition(
                "select r_ciclo, r_ano, r_mes from public.sp_informacion_ciclo(@p_ruta);",
                new { p_ruta = rutaNorm }, cancellationToken: ct));
        }

        if (row is null)
        {
            return new InformacionCicloDto { Encontrado = false };
        }

        return new InformacionCicloDto
        {
            Encontrado = true,
            Ciclo = row.R_ciclo ?? 0,
            Anio = row.R_ano ?? 0,
            Mes = row.R_mes ?? 0,
        };
    }

    // -------------------------------------------------------------------------
    // Ruta (paridad GetRuta: sp_medidores_por_ruta_ws)
    // -------------------------------------------------------------------------

    public async Task<List<MedidorDto>> GetRutaAsync(string ruta, int ciclo, int anio, int mes, CancellationToken ct = default)
    {
        var conn = await AbrirAsync(ct);
        var rutaParam = (ruta ?? string.Empty).Trim();

        var rows = await conn.QueryAsync(new CommandDefinition(
            "select * from public.sp_medidores_por_ruta_ws(@p_ruta, @p_ciclo, @p_anio, @p_mes);",
            new { p_ruta = rutaParam, p_ciclo = ciclo, p_anio = anio, p_mes = mes }, cancellationToken: ct));

        var medidores = rows
            .Cast<IDictionary<string, object?>>()
            .Select(MapMedidor)
            .ToList();

        // El WS registra un log de descarga por ruta (best-effort, no bloquea).
        try
        {
            await conn.ExecuteAsync(new CommandDefinition(
                "call public.sp_generar_log_app_ciclo_descarga(@p_anio, @p_mes, @p_ciclo, @p_ruta);",
                new { p_anio = anio, p_mes = mes, p_ciclo = ciclo, p_ruta = rutaParam }, cancellationToken: ct));
        }
        catch (Exception)
        {
            // Log no crítico: la descarga no debe fallar por el log.
        }

        return medidores;
    }

    // -------------------------------------------------------------------------
    // Condiciones de lectura (catálogo administrable por empresa)
    // -------------------------------------------------------------------------

    public async Task<List<CondicionLecturaDto>> GetCondicionesAsync(long companyId, CancellationToken ct = default)
    {
        var conn = await AbrirAsync(ct);
        // Scopeado por la empresa de la sesión (A6). requiereLectura se deriva del
        // tipo (adm_condicion_lectura_tipo.requiere_lectura, true solo para N), no
        // del ABM, para que nunca se desincronice de la semántica del motor.
        const string sql = @"
            select a.codigo AS Codigo, a.descripcion AS Descripcion, a.tipo AS Tipo,
                   a.facturacion AS Facturacion, a.aplica_descuento AS AplicaDescuento,
                   coalesce(t.requiere_lectura, a.tipo = 'N') AS RequiereLectura, a.orden AS Orden
            from public.adm_condicion_lectura a
            left join public.adm_condicion_lectura_tipo t on t.tipo = a.tipo
            where a.company_id = @CompanyId and a.activo
            order by a.orden, a.codigo;";

        var filas = await conn.QueryAsync<CondicionLecturaDto>(
            new CommandDefinition(sql, new { CompanyId = companyId }, cancellationToken: ct));
        return filas.ToList();
    }

    // -------------------------------------------------------------------------
    // Snapshot offline V3 (paridad GetOfflineSnapshotV3)
    // -------------------------------------------------------------------------

    public async Task<OfflineSnapshotRutaDto> GetSnapshotAsync(string ruta, int ciclo, int anio, int mes, CancellationToken ct = default)
    {
        var respuesta = new OfflineSnapshotRutaDto
        {
            Ruta = (ruta ?? string.Empty).Trim(),
            Ciclo = ciclo,
            Anio = anio,
            Mes = mes,
            GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        List<MedidorDto> medidores;
        try
        {
            medidores = await GetRutaAsync(respuesta.Ruta, ciclo, anio, mes, ct);
        }
        catch (Exception ex)
        {
            respuesta.Success = false;
            respuesta.Codigo = "SNAPSHOT_OFFLINE_V3_ERROR";
            respuesta.Mensaje = "No se pudo obtener la ruta.";
            respuesta.Warnings.Add(ex.Message);
            return respuesta;
        }

        if (medidores.Count == 0)
        {
            respuesta.Success = true;
            respuesta.Codigo = "SNAPSHOT_OFFLINE_V3_SIN_DATOS";
            respuesta.Mensaje = "La ruta no tiene medidores pendientes.";
            return respuesta;
        }

        var conn = await AbrirAsync(ct);
        var fechaFactura = DateTime.Today;

        foreach (var medidor in medidores)
        {
            ct.ThrowIfCancellationRequested();
            var item = new OfflineSnapshotClienteDto { Clave = medidor.Clave, Medidor = medidor.Medidor };

            try
            {
                var identidad = await ResolverIdentidadAsync(conn, medidor.Clave, ct);
                if (identidad is null)
                {
                    item.Success = false;
                    item.Codigo = "SNAPSHOT_CLIENTE_ERROR";
                    item.Mensaje = $"No se encontró el cliente {medidor.Clave}.";
                    respuesta.Items.Add(item);
                    respuesta.Warnings.Add(item.Mensaje);
                    continue;
                }

                item.CompanyId = identidad.CompanyId;
                item.ClienteId = identidad.ClienteId;

                var bloque = await ReservarBloqueCaiAsync(conn, identidad.CompanyId, respuesta.Ruta, ct);
                var packageJson = await GenerarSnapshotClienteAsync(conn, identidad, anio, mes, fechaFactura, ct);

                if (string.IsNullOrWhiteSpace(packageJson))
                {
                    item.Success = false;
                    item.Codigo = "SNAPSHOT_VACIO";
                    item.Mensaje = "El snapshot del cliente vino vacío.";
                    respuesta.Items.Add(item);
                    respuesta.Warnings.Add($"{medidor.Clave}: snapshot vacío.");
                    continue;
                }

                item.PackageJson = InyectarCaiOffline(packageJson, bloque, respuesta.Ruta);
                item.Success = true;
                item.Codigo = "OK";
            }
            catch (Exception ex)
            {
                item.Success = false;
                item.Codigo = "SNAPSHOT_CLIENTE_ERROR";
                item.Mensaje = ex.Message;
                respuesta.Warnings.Add($"{medidor.Clave}: {ex.Message}");
            }

            respuesta.Items.Add(item);
        }

        respuesta.Success = respuesta.Items.Any(i => i.Success);
        respuesta.Codigo = respuesta.Success ? "OK" : "SNAPSHOT_OFFLINE_V3_ERROR";
        respuesta.Mensaje = respuesta.Success ? "Snapshot generado." : "No se pudo generar el snapshot.";
        return respuesta;
    }

    // -------------------------------------------------------------------------
    // Actualizar lectura V3 (paridad ActualizarLecturaV3)
    // -------------------------------------------------------------------------

    public async Task<LecturaV3Respuesta> ActualizarLecturaAsync(LecturaV3Request request, long companyId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Clave))
        {
            return Fail("CLAVE_REQUERIDA", "La clave del cliente es obligatoria.");
        }

        var conn = await AbrirAsync(ct);
        var identidad = await ResolverIdentidadAsync(conn, request.Clave, ct);
        if (identidad is null)
        {
            return Fail("CLIENTE_NO_ENCONTRADO", $"No se encontró el cliente {request.Clave}.");
        }

        // A6: la lectura sólo puede afectar al tenant de la sesión.
        if (identidad.CompanyId != companyId)
        {
            return Fail("TENANT_MISMATCH", "El cliente no pertenece a la empresa de la sesión.");
        }

        var requiereCai = request.IdCai > 0 || request.CorrelativoCai > 0 || !string.IsNullOrWhiteSpace(request.NumeroFactura);

        if (requiereCai)
        {
            if (request.IdCai <= 0 || request.CorrelativoCai <= 0 || string.IsNullOrWhiteSpace(request.NumeroFactura))
            {
                return Fail("CAI_FORMAL_REQUERIDO", "IdCai, CorrelativoCai y NumeroFactura deben venir juntos.");
            }

            // Prepara el correlativo CAI (idempotente en el SP); avanza el estado de sync.
            await PrepararCorrelativoAsync(conn, companyId, identidad, request, ct);

            // Idempotencia: si la factura ya existe (mismo tenant/cliente/número), la
            // subida es un reintento → se devuelve la existente sin re-postear (evita
            // el "Ya existe factura" de sp_lectura_v3). numfactura es único por cliente.
            var facturaExistente = await FacturaRegistradaAsync(conn, companyId, identidad.ClienteClave, request.NumeroFactura!, ct);
            if (facturaExistente is not null)
            {
                var confirmIdem = await ConfirmarCorrelativoAsync(conn, companyId, identidad, request, facturaExistente.FacturaId, ct);
                var existente = ConstruirRespuestaExistente(identidad, facturaExistente, request);
                if (!confirmIdem.Success)
                {
                    existente.Warnings.Add("La factura ya existía pero la confirmación del correlativo CAI falló.");
                }

                return existente;
            }

            // Preflight de total: si el dispositivo envía un total, debe coincidir con el
            // del servidor (si no lo envía, sp_lectura_v3 calcula el suyo, que es el válido).
            if (request.Total.HasValue)
            {
                var totalServidor = await CalcularTotalPreflightAsync(conn, companyId, identidad, request, ct);
                if (totalServidor.HasValue && Math.Abs(totalServidor.Value - request.Total.Value) > 0.05m)
                {
                    return Fail("SYNC_CONFLICT_TOTAL",
                        $"El total del dispositivo ({request.Total.Value:0.00}) no coincide con el del servidor ({totalServidor.Value:0.00}).");
                }
            }
        }

        var respuesta = await EjecutarLecturaV3Async(conn, companyId, request, ct);

        if (respuesta.Success && requiereCai && respuesta.FacturaId > 0)
        {
            var confirm = await ConfirmarCorrelativoAsync(conn, companyId, identidad, request, respuesta.FacturaId, ct);
            if (!confirm.Success)
            {
                respuesta.Codigo = "OK_WITH_SYNC_CONFLICT";
                respuesta.Warnings.Add("La lectura se registró pero falló la confirmación del correlativo CAI.");
            }
        }

        return respuesta;
    }

    // -------------------------------------------------------------------------
    // Helpers de datos
    // -------------------------------------------------------------------------

    private async Task<ClienteIdentidad?> ResolverIdentidadAsync(DbConnection conn, string clave, CancellationToken ct)
    {
        const string sql = @"
            select company_id AS CompanyId, maestro_cliente_id AS ClienteId,
                   maestro_cliente_clave AS ClienteClave, coalesce(maestro_cliente_nombre, '') AS ClienteNombre,
                   coalesce(contador, '') AS Contador
            from public.cliente_maestro
            where maestro_cliente_clave = @Clave and estado = true
            limit 1;";
        return await conn.QueryFirstOrDefaultAsync<ClienteIdentidad>(
            new CommandDefinition(sql, new { Clave = (clave ?? string.Empty).Trim() }, cancellationToken: ct));
    }

    private async Task<CaiBloqueRow?> ReservarBloqueCaiAsync(DbConnection conn, long companyId, string ruta, CancellationToken ct)
    {
        const string sql = @"
            select cai_bloque_id AS CaiBloqueId, cai_id AS CaiId, codigo_cai AS CodigoCai,
                   prefijo_documento AS PrefijoDocumento, correlativo_desde AS CorrelativoDesde,
                   correlativo_hasta AS CorrelativoHasta, correlativo_actual AS CorrelativoActual,
                   correlativo_siguiente AS CorrelativoSiguiente, estado_codigo AS EstadoCodigo
            from public.sp_adm_obtener_o_reservar_bloque_cai_ruta(
                p_company_id => @CompanyId, p_ruta_codigo => @Ruta,
                p_cantidad => @Cantidad, p_usuario => @Usuario);";
        return await conn.QueryFirstOrDefaultAsync<CaiBloqueRow>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            Ruta = ruta,
            Cantidad = CaiBloqueCantidad,
            Usuario,
        }, cancellationToken: ct));
    }

    private async Task<string?> GenerarSnapshotClienteAsync(DbConnection conn, ClienteIdentidad identidad, int anio, int mes, DateTime fechaFactura, CancellationToken ct)
    {
        const string sql = @"
            select snapshot_json::text
            from public.sp_adm_generar_snapshot_offline_cliente_lectura(
                p_company_id => @CompanyId, p_cliente_id => @ClienteId,
                p_anio => @Anio, p_mes => @Mes, p_fecha_factura => @Fecha::date);";
        return await conn.ExecuteScalarAsync<string?>(new CommandDefinition(sql, new
        {
            identidad.CompanyId,
            identidad.ClienteId,
            Anio = anio,
            Mes = mes,
            Fecha = fechaFactura,
        }, cancellationToken: ct));
    }

    private static string InyectarCaiOffline(string packageJson, CaiBloqueRow? bloque, string ruta)
    {
        if (bloque is null)
        {
            return packageJson;
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(packageJson);
        }
        catch (JsonException)
        {
            return packageJson;
        }

        if (root is not JsonObject obj)
        {
            return packageJson;
        }

        obj["cai_offline"] = new JsonObject
        {
            ["cai_id"] = bloque.CaiId,
            ["cai_bloque_id"] = bloque.CaiBloqueId,
            ["codigo_cai"] = bloque.CodigoCai,
            ["prefijo_documento"] = bloque.PrefijoDocumento,
            ["correlativo_desde"] = bloque.CorrelativoDesde,
            ["correlativo_hasta"] = bloque.CorrelativoHasta,
            ["correlativo_actual"] = bloque.CorrelativoActual,
            ["correlativo_siguiente"] = bloque.CorrelativoSiguiente,
            ["ruta_codigo"] = ruta,
            ["estado_codigo"] = bloque.EstadoCodigo,
        };

        return obj.ToJsonString();
    }

    private async Task<CaiSyncRow> PrepararCorrelativoAsync(DbConnection conn, long companyId, ClienteIdentidad identidad, LecturaV3Request req, CancellationToken ct)
    {
        const string sql = @"
            select success AS Success, estado_codigo AS EstadoCodigo, cai_bloque_id AS CaiBloqueId,
                   factura_id AS FacturaId, mensaje AS Mensaje
            from public.sp_adm_prepare_correlativo_cai_sync(
                p_company_id => @CompanyId, p_cliente_id => @ClienteId, p_id_cai => @IdCai,
                p_correlativo => @Correlativo, p_numero_factura => @NumeroFactura,
                p_lectura_uuid => @LecturaUuid, p_usuario => @Usuario);";
        return await conn.QueryFirstAsync<CaiSyncRow>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            identidad.ClienteId,
            IdCai = (long)req.IdCai,
            Correlativo = (long)req.CorrelativoCai,
            NumeroFactura = req.NumeroFactura,
            LecturaUuid = NullIfEmpty(req.LecturaUuid),
            Usuario = req.Usuario ?? Usuario,
        }, cancellationToken: ct));
    }

    private async Task<CaiSyncRow> ConfirmarCorrelativoAsync(DbConnection conn, long companyId, ClienteIdentidad identidad, LecturaV3Request req, long facturaId, CancellationToken ct)
    {
        const string sql = @"
            select success AS Success, estado_codigo AS EstadoCodigo, cai_bloque_id AS CaiBloqueId,
                   factura_id AS FacturaId, mensaje AS Mensaje
            from public.sp_adm_confirmar_correlativo_cai_sync(
                p_company_id => @CompanyId, p_cliente_id => @ClienteId, p_id_cai => @IdCai,
                p_correlativo => @Correlativo, p_numero_factura => @NumeroFactura,
                p_lectura_uuid => @LecturaUuid, p_factura_id => @FacturaId, p_usuario => @Usuario);";
        return await conn.QueryFirstAsync<CaiSyncRow>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            identidad.ClienteId,
            IdCai = (long)req.IdCai,
            Correlativo = (long)req.CorrelativoCai,
            NumeroFactura = req.NumeroFactura,
            LecturaUuid = NullIfEmpty(req.LecturaUuid),
            FacturaId = facturaId,
            Usuario = req.Usuario ?? Usuario,
        }, cancellationToken: ct));
    }

    private async Task<FacturaRegistrada?> FacturaRegistradaAsync(DbConnection conn, long companyId, string clave, string numeroFactura, CancellationToken ct)
    {
        // Acotado al tenant de la sesión (A6): clientecodigo puede repetirse entre empresas.
        const string sql = @"
            select f.id AS FacturaId, coalesce(f.numrecibo, 0) AS NumRecibo, coalesce(f.numfactura, '') AS NumeroFactura
            from public.factura f
            where f.company_id = @CompanyId and f.clientecodigo = @Clave and f.numfactura = @NumeroFactura
            order by f.id desc limit 1;";
        return await conn.QueryFirstOrDefaultAsync<FacturaRegistrada>(
            new CommandDefinition(sql, new { CompanyId = companyId, Clave = clave, NumeroFactura = numeroFactura }, cancellationToken: ct));
    }

    private async Task<decimal?> CalcularTotalPreflightAsync(DbConnection conn, long companyId, ClienteIdentidad identidad, LecturaV3Request req, CancellationToken ct)
    {
        const string sql = @"
            select total_factura
            from public.sp_adm_calcular_factura_lectura(
                p_company_id => @CompanyId, p_anio => @Anio, p_mes => @Mes, p_cliente_id => @ClienteId,
                p_contador => @Contador, p_fecha_lectura => @Fecha::date, p_lectura_actual => @LecturaActual::numeric,
                p_condicion_lectura => @Condicion, p_lectura_promedio => @Promedio::numeric, p_usuario => @Usuario,
                p_observacion => @Observacion, p_id_cai => @IdCai::int, p_correlativo_cai => @Correlativo::int,
                p_numero_factura => @NumeroFactura, p_informativo => @Informativo);";
        return await conn.ExecuteScalarAsync<decimal?>(new CommandDefinition(sql, new
        {
            CompanyId = companyId,
            req.Anio,
            req.Mes,
            identidad.ClienteId,
            Contador = NullIfEmpty(req.Contador) ?? NullIfEmpty(identidad.Contador),
            Fecha = req.FechaLecturaActual ?? DateTime.Today,
            req.LecturaActual,
            Condicion = CondicionOrDefault(req.CondicionLectura),
            Promedio = req.LecturaPromedio,
            Usuario = req.Usuario ?? Usuario,
            Observacion = NullIfEmpty(req.Observacion),
            IdCai = (int?)req.IdCai,
            Correlativo = (int?)req.CorrelativoCai,
            NumeroFactura = NullIfEmpty(req.NumeroFactura),
            Informativo = NullIfEmpty(req.Informativo),
        }, cancellationToken: ct));
    }

    private async Task<LecturaV3Respuesta> EjecutarLecturaV3Async(DbConnection conn, long companyId, LecturaV3Request req, CancellationToken ct)
    {
        const string sql = @"
            select * from public.sp_lectura_v3(
                p_company_id => @CompanyId, p_anio => @Anio, p_mes => @Mes, p_ciclo => NULL,
                p_clave => @Clave, p_contador => @Contador, p_fecha_lectura => @Fecha::date,
                p_usuario => @Usuario, p_lectura_actual => @LecturaActual::numeric,
                p_ser3 => @Ser3::char, p_ser4 => @Ser4::char, p_observacion => @Observacion,
                p_condicion_lectura => @Condicion, p_lectura_promedio => @Promedio::numeric,
                p_numero_factura => @NumeroFactura, p_correlativo_cai => @Correlativo::int,
                p_id_cai => @IdCai::int, p_tienemedidor => @TieneMedidor::char, p_informativo => @Informativo,
                p_imagen => @Imagen::bytea, p_categoria => @Categoria::char, p_lectura_uuid => @LecturaUuid);";

        var parametros = new DynamicParameters();
        parametros.Add("CompanyId", companyId, DbType.Int64);
        parametros.Add("Anio", req.Anio, DbType.Int32);
        parametros.Add("Mes", req.Mes, DbType.Int32);
        parametros.Add("Clave", req.Clave, DbType.String);
        parametros.Add("Contador", NullIfEmpty(req.Contador), DbType.String);
        parametros.Add("Fecha", req.FechaLecturaActual ?? DateTime.Today, DbType.Date);
        parametros.Add("Usuario", req.Usuario ?? Usuario, DbType.String);
        parametros.Add("LecturaActual", req.LecturaActual, DbType.Decimal);
        parametros.Add("Ser3", FirstCharOr(req.Ser3, "N"), DbType.String);
        parametros.Add("Ser4", FirstCharOr(req.Ser4, "N"), DbType.String);
        parametros.Add("Observacion", NullIfEmpty(req.Observacion), DbType.String);
        parametros.Add("Condicion", CondicionOrDefault(req.CondicionLectura), DbType.String);
        parametros.Add("Promedio", req.LecturaPromedio, DbType.Decimal);
        parametros.Add("NumeroFactura", NullIfEmpty(req.NumeroFactura), DbType.String);
        parametros.Add("Correlativo", req.CorrelativoCai > 0 ? req.CorrelativoCai : (int?)null, DbType.Int32);
        parametros.Add("IdCai", req.IdCai > 0 ? req.IdCai : (int?)null, DbType.Int32);
        parametros.Add("TieneMedidor", FirstCharOr(req.TieneMedidor, "N"), DbType.String);
        parametros.Add("Informativo", NullIfEmpty(req.Informativo), DbType.String);
        parametros.Add("Imagen", DecodeImagen(req.Imagen), DbType.Binary);
        parametros.Add("Categoria", FirstCharOr(req.Categoria, "0"), DbType.String);
        parametros.Add("LecturaUuid", NullIfEmpty(req.LecturaUuid), DbType.String);

        IDictionary<string, object?>? row;
        try
        {
            row = await conn.QueryFirstOrDefaultAsync(new CommandDefinition(sql, parametros, cancellationToken: ct))
                as IDictionary<string, object?>;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Npgsql.PostgresException ex)
        {
            // sp_lectura_v3 valida reglas de negocio con RAISE (número de factura
            // requerido, duplicado, etc.): se devuelven como error de negocio,
            // igual que el ERROR_LECTURA_V3 del WS viejo.
            return Fail("ERROR_LECTURA_V3", ex.MessageText);
        }

        if (row is null)
        {
            return Fail("ERROR_LECTURA_V3", "El SP no devolvió resultado.");
        }

        return new LecturaV3Respuesta
        {
            Success = Bool(row, "success"),
            Codigo = Str(row, "codigo"),
            Mensaje = Str(row, "mensaje"),
            FacturaId = Long(row, "factura_id"),
            NumRecibo = Long(row, "numrecibo"),
            NumeroFactura = Str(row, "numero_factura"),
            ClienteId = Long(row, "cliente_id"),
            ClienteClave = Str(row, "cliente_clave"),
            ClienteNombre = Str(row, "cliente_nombre"),
            LecturaActual = req.LecturaActual,
            Consumo = Dec(row, "consumo"),
            Subtotal = Dec(row, "subtotal"),
            SubtotalAjustes = Dec(row, "subtotal_ajustes"),
            SaldosAnteriores = Dec(row, "saldos_anteriores"),
            Recargos = Dec(row, "recargos"),
            Total = Dec(row, "total"),
            Taservi1 = Dec(row, "taservi1"),
            Taservi2 = Dec(row, "taservi2"),
            Taservi3 = Dec(row, "taservi3"),
            Taservi4 = Dec(row, "taservi4"),
            DetalleServicios = ParseDetalle(Str(row, "detalle_servicios_json")),
            Warnings = ParseWarnings(Str(row, "warnings_json")),
        };
    }

    private LecturaV3Respuesta ConstruirRespuestaExistente(ClienteIdentidad identidad, FacturaRegistrada factura, LecturaV3Request req)
    {
        return new LecturaV3Respuesta
        {
            Success = true,
            Codigo = "IDEMPOTENTE",
            Mensaje = "La factura ya estaba registrada (idempotente).",
            FacturaId = factura.FacturaId,
            NumRecibo = factura.NumRecibo,
            NumeroFactura = string.IsNullOrEmpty(factura.NumeroFactura) ? (req.NumeroFactura ?? string.Empty) : factura.NumeroFactura,
            ClienteId = identidad.ClienteId,
            ClienteClave = identidad.ClienteClave,
            ClienteNombre = identidad.ClienteNombre,
            LecturaActual = req.LecturaActual,
        };
    }

    // -------------------------------------------------------------------------
    // Mapeo / utilidades
    // -------------------------------------------------------------------------

    private static MedidorDto MapMedidor(IDictionary<string, object?> row)
    {
        var tipo = Str(row, "tipo");
        return new MedidorDto
        {
            Clave = Str(row, "maestro_cliente_clave"),
            Medidor = Str(row, "maestro_medidor_numero"),
            Identidad = Str(row, "maestro_cliente_identidad"),
            NombreInquilino = Str(row, "maestro_cliente_nombre"),
            Descuento = Str(row, "descuento_valor"),
            TieneDescuento = Sn(row, "tiene_descuento"),
            Ciclo = Str(row, "ciclo"),
            Ruta = Str(row, "ruta"),
            Secuencia = Str(row, "secuencia"),
            Categoria = Str(row, "categoria"),
            Tipo = string.IsNullOrEmpty(tipo) ? "1" : tipo,
            TieneMedidor = Sn(row, "tiene_med"),
            LecturaAnterior = Str(row, "lect_ant"),
            FechaLecturaAnterior = DateStr(row, "fecha_lect_ant"),
            LecturaActual = Str(row, "lect_act"),
            FechaLecturaActual = DateStr(row, "fecha_lect_act"),
            DireccionCliente = Str(row, "direccion"),
            Codigo = Str(row, "codigo"),
            Rtn = Str(row, "rtn"),
            Ser1 = Sn(row, "ser1"),
            Ser2 = Sn(row, "ser2"),
            Ser3 = Sn(row, "ser3"),
            Ser4 = Sn(row, "ser4"),
            Ser5 = Sn(row, "ser5"),
            Ser6 = Sn(row, "ser6"),
            Ser7 = Sn(row, "ser7"),
            Ser8 = Sn(row, "ser8"),
            Ser9 = Sn(row, "ser9"),
            Ser10 = Sn(row, "ser10"),
            SdoAgua = Dec(row, "agua_anterior"),
            SdoAlcantarillado = Dec(row, "alcantarillado_anterior"),
            SdoAmbiental = Dec(row, "ambiental_anterior"),
            SdoConvenio = Dec(row, "convenio_anterior"),
            SdoOtros = Dec(row, "otro_anterior"),
            SdoErsap = Dec(row, "ersap_anterior"),
            SdoGestionLegal = Dec(row, "gestion_legal_anterior"),
            SdoSer6 = Dec(row, "sdo_ser6_anterior"),
            SdoSer7 = Dec(row, "sdo_ser7_anterior"),
            SdoSer8 = Dec(row, "sdo_ser8_anterior"),
            SdoSer9 = Dec(row, "sdo_ser9_anterior"),
            SdoSer10 = Dec(row, "sdo_ser10_anterior"),
            RecargoSdoAgua = Dec(row, "agua_recargo"),
            RecargoSdoAmbiental = Dec(row, "ambiental_recargo"),
            RecargoSdoConvenio = Dec(row, "convenio_recargo"),
            RecargoSdoOtros = Dec(row, "otro_recargo"),
            RecargoSdoErsap = Dec(row, "ersap_recargo"),
            RecargoSdoGestionLegal = Dec(row, "gestion_legal_recargo"),
            RecargoSdoSer6 = Dec(row, "sdo_ser6_recargo"),
            RecargoSdoSer7 = Dec(row, "sdo_ser7_recargo"),
            RecargoSdoSer8 = Dec(row, "sdo_ser8_recargo"),
            RecargoSdoSer9 = Dec(row, "sdo_ser9_recargo"),
            RecargoSdoSer10 = Dec(row, "sdo_ser10_recargo"),
            Promedio6Meses = Dec(row, "promedio"),
        };
    }

    private static List<LecturaServicioDetalleDto> ParseDetalle(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<LecturaServicioDetalleDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<LecturaServicioDetalleDto>>(json, JsonSnake)
                   ?? new List<LecturaServicioDetalleDto>();
        }
        catch (JsonException)
        {
            return new List<LecturaServicioDetalleDto>();
        }
    }

    private static List<string> ParseWarnings(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<string>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json, JsonSnake) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }

    private static LecturaV3Respuesta Fail(string codigo, string mensaje) =>
        new() { Success = false, Codigo = codigo, Mensaje = mensaje };

    private static string NormalizarRuta(string? ruta)
    {
        var t = (ruta ?? string.Empty).Trim();
        if (t.Length == 0)
        {
            return t;
        }

        return t.All(char.IsDigit) && t.Length < 5 ? t.PadLeft(5, '0') : t;
    }

    private static byte[]? DecodeImagen(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string CondicionOrDefault(string? s) => string.IsNullOrWhiteSpace(s) ? "N" : s.Trim();

    private static string FirstCharOr(string? s, string def) =>
        string.IsNullOrWhiteSpace(s) ? def : s.Trim().Substring(0, 1);

    private static string Str(IDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v is not null and not DBNull ? v.ToString()!.Trim() : string.Empty;

    private static decimal Dec(IDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v is not null and not DBNull ? Convert.ToDecimal(v) : 0m;

    private static long Long(IDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v is not null and not DBNull ? Convert.ToInt64(v) : 0L;

    private static bool Bool(IDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v is bool b && b;

    private static string Sn(IDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v is bool b && b ? "S" : "N";

    private static string DateStr(IDictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var v) && v is DateTime d ? d.ToString("yyyy-MM-dd") : string.Empty;

    private async Task<DbConnection> AbrirAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }

    // -------------------------------------------------------------------------
    // Filas internas (Dapper)
    // -------------------------------------------------------------------------

    private sealed class CredencialRow
    {
        public long CredencialId { get; init; }
        public long CompanyId { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;
        public string Ruta { get; init; } = string.Empty;
        public int? CodCiclo { get; init; }
    }

    private sealed class CicloRow
    {
        public int? R_ciclo { get; init; }
        public int? R_ano { get; init; }
        public int? R_mes { get; init; }
    }

    private sealed class ClienteIdentidad
    {
        public long CompanyId { get; init; }
        public long ClienteId { get; init; }
        public string ClienteClave { get; init; } = string.Empty;
        public string ClienteNombre { get; init; } = string.Empty;
        public string Contador { get; init; } = string.Empty;
    }

    private sealed class CaiBloqueRow
    {
        public long CaiBloqueId { get; init; }
        public long CaiId { get; init; }
        public string? CodigoCai { get; init; }
        public string? PrefijoDocumento { get; init; }
        public long CorrelativoDesde { get; init; }
        public long CorrelativoHasta { get; init; }
        public long CorrelativoActual { get; init; }
        public long CorrelativoSiguiente { get; init; }
        public string? EstadoCodigo { get; init; }
    }

    private sealed class CaiSyncRow
    {
        public bool Success { get; init; }
        public string? EstadoCodigo { get; init; }
        public long? CaiBloqueId { get; init; }
        public long? FacturaId { get; init; }
        public string? Mensaje { get; init; }
    }

    private sealed class FacturaRegistrada
    {
        public long FacturaId { get; init; }
        public long NumRecibo { get; init; }
        public string NumeroFactura { get; init; } = string.Empty;
    }
}
