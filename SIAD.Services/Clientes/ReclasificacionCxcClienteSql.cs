using System.Data;
using System.Data.Common;
using Dapper;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Clientes;

/// <summary>
/// Reclasificación contable de CxC por cambio de categoría del cliente
/// (pruebas operativas jul-2026, p.ej. Doméstico → Comercial).
///
/// Solo aplica cuando la integración contable lleva la CxC en modo
/// POR_SERVICIO_CATEGORIA con facturación activa: el saldo pendiente de las
/// facturas vivas está debitado en las cuentas de la categoría anterior, así
/// que se genera DEBE CxC nueva / HABER CxC vieja por servicio y las facturas
/// vivas actualizan su snapshot de categoría para que los cobros futuros
/// acrediten la cuenta nueva. Cuotas de convenio y ND quedan fuera: su posteo
/// no usa la categoría del cliente (desglose porcentual / CxC general).
/// </summary>
internal static class ReclasificacionCxcClienteSql
{
    internal sealed record Resultado(long EventoId, long? PolizaId, decimal MontoReclasificado, int FacturasActualizadas);

    // Clase con setters (no record posicional): Dapper materializa por
    // propiedad y los tipos calzan con las columnas (factura.id es integer).
    private sealed class PendienteRow
    {
        public int FacturaId { get; set; }
        public int? CategoriaServicioId { get; set; }
        public bool? ConMedicion { get; set; }
        public string? Servicio { get; set; }
        public decimal Pendiente { get; set; }
    }

    /// <summary>
    /// Ejecuta la reclasificación dentro de la transacción recibida. Devuelve
    /// null cuando la config contable no depende de la categoría (sin config,
    /// facturación inactiva o modo distinto de POR_SERVICIO_CATEGORIA).
    /// </summary>
    internal static async Task<Resultado?> ReclasificarPorCambioCategoriaAsync(
        DbConnection connection,
        long companyId,
        int clienteId,
        string clienteClave,
        int? categoriaAnterior,
        int? categoriaNueva,
        string usuario,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        var config = await IntegracionContableConfigSql.ObtenerConfigAsync(connection, companyId, transaction, ct);
        if (config is null
            || !config.ActivoFacturacion
            || !string.Equals(config.ModoCxc, IntegracionContableModos.PorServicioCategoria, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        // Facturas vivas del cliente bloqueadas contra cobros concurrentes.
        // Pendiente por línea (montovalor_saldo ?? montovalor, igual que el
        // cobro); las facturas sin desglose caen al saldototal contra la fila
        // general (servicio NULL), también igual que el cobro.
        const string sqlPendientes = @"
            WITH vivas AS (
                SELECT f.id, f.categoria_servicio_id, f.con_medicion, f.saldototal
                FROM public.factura f
                WHERE f.company_id = @CompanyId
                  AND f.clientecodigo = @Clave
                  AND f.estado_id IN (1, 4)  -- Activa / ParcialmenteAbonada (EstadoDocumentoComercial)
                FOR UPDATE
            ),
            lineas AS (
                SELECT v.id,
                       v.categoria_servicio_id,
                       v.con_medicion,
                       CASE WHEN d.tiposervicio IS NULL OR btrim(d.tiposervicio) = ''
                            THEN d.codigo ELSE d.tiposervicio END AS servicio,
                       COALESCE(d.montovalor_saldo, d.montovalor, 0) AS pendiente
                FROM vivas v
                JOIN public.factura_detalle d ON d.factura_id = v.id
                WHERE COALESCE(d.montovalor_saldo, d.montovalor, 0) > 0
            )
            SELECT id AS FacturaId, categoria_servicio_id AS CategoriaServicioId,
                   con_medicion AS ConMedicion, servicio AS Servicio, pendiente AS Pendiente
            FROM lineas
            UNION ALL
            SELECT v.id, v.categoria_servicio_id, v.con_medicion, NULL, v.saldototal
            FROM vivas v
            WHERE COALESCE(v.saldototal, 0) > 0
              AND NOT EXISTS (SELECT 1 FROM lineas l WHERE l.id = v.id);
        ";

        var pendientes = (await connection.QueryAsync<PendienteRow>(new CommandDefinition(
            sqlPendientes,
            new { CompanyId = companyId, Clave = clienteClave },
            transaction,
            cancellationToken: ct))).ToList();

        // La cuenta vieja se resuelve con el snapshot de cada factura (lo que
        // realmente se debitó al emitir); la nueva, con la categoría nueva y
        // la misma dimensión de medición.
        var debePorCuenta = new Dictionary<long, decimal>();
        var haberPorCuenta = new Dictionary<long, decimal>();

        foreach (var grupo in pendientes.GroupBy(p => (p.CategoriaServicioId, p.ConMedicion)))
        {
            var codigos = grupo.Select(g => g.Servicio).ToList();
            var cuentasViejas = await IntegracionContableConfigSql.ResolverCuentasCxcPorServicioAsync(
                connection, companyId, config.ModoCxc, codigos, grupo.Key.CategoriaServicioId, grupo.Key.ConMedicion, transaction, ct);
            var cuentasNuevas = await IntegracionContableConfigSql.ResolverCuentasCxcPorServicioAsync(
                connection, companyId, config.ModoCxc, codigos, categoriaNueva, grupo.Key.ConMedicion, transaction, ct);

            foreach (var linea in grupo)
            {
                var monto = Math.Round(linea.Pendiente, 2, MidpointRounding.AwayFromZero);
                if (monto <= 0)
                {
                    continue;
                }

                var clave = IntegracionContableConfigSql.NormalizarCodigo(linea.Servicio);
                var vieja = cuentasViejas[clave];
                var nueva = cuentasNuevas[clave];
                if (vieja == nueva)
                {
                    continue;
                }

                haberPorCuenta[vieja] = haberPorCuenta.GetValueOrDefault(vieja) + monto;
                debePorCuenta[nueva] = debePorCuenta.GetValueOrDefault(nueva) + monto;
            }
        }

        var total = debePorCuenta.Values.Sum();

        var eventoId = await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.cln_cliente_recategorizacion
                (company_id, maestro_cliente_id, cliente_clave, categoria_anterior_id,
                 categoria_nueva_id, monto_reclasificado, usuario)
            VALUES (@CompanyId, @ClienteId, @Clave, @CategoriaAnterior, @CategoriaNueva, @Monto, @Usuario)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                ClienteId = clienteId,
                Clave = clienteClave,
                CategoriaAnterior = categoriaAnterior,
                CategoriaNueva = categoriaNueva,
                Monto = total,
                Usuario = usuario
            },
            transaction,
            cancellationToken: ct));

        long? polizaId = null;
        if (total > 0)
        {
            var descripcion = $"Reclasificación CxC por cambio de categoría del cliente {clienteClave}";
            var lineasComprobante = new List<IntegracionContableConfigSql.ComprobanteLinea>();
            lineasComprobante.AddRange(debePorCuenta
                .OrderBy(k => k.Key)
                .Select(k => new IntegracionContableConfigSql.ComprobanteLinea(k.Key, k.Value, 0m, descripcion)));
            lineasComprobante.AddRange(haberPorCuenta
                .OrderBy(k => k.Key)
                .Select(k => new IntegracionContableConfigSql.ComprobanteLinea(k.Key, 0m, k.Value, descripcion)));

            polizaId = await IntegracionContableConfigSql.GenerarComprobanteAsync(
                connection, companyId, "VENTAS", "RECLASIFICACION_CXC", eventoId, clienteClave,
                DateOnly.FromDateTime(DateTime.Today), descripcion, usuario, lineasComprobante, transaction, ct);

            if (polizaId is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE public.cln_cliente_recategorizacion SET poliza_id = @PolizaId WHERE id = @Id",
                    new { PolizaId = polizaId, Id = eventoId }, transaction, cancellationToken: ct));
            }
        }

        // El snapshot de categoría de las facturas VIVAS sigue al cliente para
        // que los cobros futuros acrediten la cuenta nueva; las pagadas y
        // anuladas conservan su historia.
        var facturasVivas = pendientes.Select(p => p.FacturaId).Distinct().ToArray();
        var actualizadas = 0;
        if (facturasVivas.Length > 0)
        {
            actualizadas = await connection.ExecuteAsync(new CommandDefinition(@"
                UPDATE public.factura
                SET categoria_servicio_id = @CategoriaNueva, updated_at = now()
                WHERE company_id = @CompanyId AND id = ANY(@Ids)",
                new { CategoriaNueva = categoriaNueva, CompanyId = companyId, Ids = facturasVivas },
                transaction,
                cancellationToken: ct));

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE public.cln_cliente_recategorizacion SET facturas_actualizadas = @N WHERE id = @Id",
                new { N = actualizadas, Id = eventoId }, transaction, cancellationToken: ct));
        }

        return new Resultado(eventoId, polizaId, total, actualizadas);
    }
}
