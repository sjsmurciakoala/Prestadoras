using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Ajustes de inventario. Ver <see cref="IAjusteInventarioService"/>.
/// <para>
/// El documento y su asiento se escriben en la MISMA transacción: un ajuste sin asiento
/// sería un papel sin efecto, y un asiento sin documento no tendría motivo ni autor.
/// </para>
/// </summary>
public sealed class AjusteInventarioService : IAjusteInventarioService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IInventarioPostingService _posting;

    public AjusteInventarioService(
        SiadDbContext context, ICurrentCompanyService company, IInventarioPostingService posting)
    {
        _context = context;
        _company = company;
        _posting = posting;
    }

    public async Task<AjusteInventarioDto> CrearYPostearAsync(AjusteInventarioDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var clase = (dto.Clase ?? string.Empty).Trim().ToUpperInvariant();
        if (!ClaseAjusteInventario.EsValida(clase))
        {
            throw new InvalidOperationException(
                $"La clase de ajuste '{dto.Clase}' no es válida. Use {string.Join(", ", ClaseAjusteInventario.Todas)}.");
        }

        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0)
        {
            throw new InvalidOperationException("El motivo del ajuste es obligatorio.");
        }
        if (motivo.Length > 120)
        {
            throw new InvalidOperationException("El motivo no puede superar los 120 caracteres.");
        }

        // Las reglas de cantidad/costo por clase se validan aquí ADEMÁS de en el motor:
        // aquí el mensaje habla el idioma del documento (clase), y el CHECK de la BD es
        // la última red.
        ValidarPorClase(clase, dto);

        // La fila del par debe existir y ser del tenant actual. El motor vuelve a
        // bloquearla con FOR UPDATE; esto es solo para fallar temprano con buen mensaje.
        var fila = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == dto.ArticuloId && u.bodega_id == dto.BodegaId)
            .Select(u => new { u.id, u.activo, u.costo_promedio })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("El artículo no tiene ubicación en esa bodega.");

        if (!fila.activo)
        {
            throw new InvalidOperationException(
                "La ubicación está deshabilitada: reactívela antes de ajustar su existencia.");
        }

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var usuario = ClasificacionNormalizer.Usuario(user);

        var documento = new alm_ajuste_inventario
        {
            articulo_id = dto.ArticuloId,
            bodega_id = dto.BodegaId,
            clase = clase,
            cantidad = dto.Cantidad,
            costo_unitario = dto.CostoUnitario,
            motivo = motivo,
            observacion = string.IsNullOrWhiteSpace(dto.Observacion) ? null : dto.Observacion.Trim(),
            posteado = false,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        _context.alm_ajuste_inventarios.Add(documento);
        // Primer guardado: necesitamos el id para que sea el documento_id del asiento.
        await _context.SaveChangesAsync(ct);

        var resultado = await _posting.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoDe(clase),
            ArticuloBodegaId = fila.id,
            Cantidad = dto.Cantidad,
            CostoUnitario = dto.CostoUnitario,
            Fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today),
            DocumentoTipo = TipoDocumentoInventario.Ajuste,
            DocumentoId = documento.id,
            Observacion = motivo
        }, user, ct);

        // F1 — Integración contable: con el módulo ALMACEN activo, el ajuste de existencia
        // (ENTRADA/SALIDA) genera su partida de doble entrada en ESTA misma transacción del
        // kardex. La ENTRADA se asienta al costo de entrada; la SALIDA, al promedio vigente
        // (que no cambia). El ajuste de VALOR se contabiliza aparte (necesita el costo anterior).
        var fechaAsiento = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        if (clase is ClaseAjusteInventario.Entrada or ClaseAjusteInventario.Salida)
        {
            var costoAsiento = clase == ClaseAjusteInventario.Entrada
                ? dto.CostoUnitario
                : resultado.CostoPromedioResultante;
            await AlmacenContabilidad.ContabilizarAjusteAsync(
                _context, _company.GetCompanyId(), dto.ArticuloId, clase,
                dto.Cantidad * costoAsiento, documento.id, $"AJ-{documento.id}",
                fechaAsiento, motivo, usuario, ct);
        }
        else if (clase == ClaseAjusteInventario.Valor)
        {
            // El valor del inventario cambia en existencia × (costo_nuevo − costo_anterior).
            // fila.costo_promedio es el costo ANTES del ajuste; en VALOR la existencia no cambia.
            var delta = resultado.ExistenciaResultante * (dto.CostoUnitario - fila.costo_promedio);
            await AlmacenContabilidad.ContabilizarAjusteValorAsync(
                _context, _company.GetCompanyId(), dto.ArticuloId, delta, documento.id,
                $"AJ-{documento.id}", fechaAsiento, motivo, usuario, ct);
        }

        documento.posteado = true;
        await _context.SaveChangesAsync(ct);

        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return new AjusteInventarioDto
        {
            Id = documento.id,
            ArticuloId = documento.articulo_id,
            BodegaId = documento.bodega_id,
            Clase = documento.clase,
            Cantidad = documento.cantidad,
            CostoUnitario = documento.costo_unitario,
            Motivo = documento.motivo,
            Observacion = documento.observacion,
            Fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today),
            Posteado = true,
            ExistenciaResultante = resultado.ExistenciaResultante,
            CostoPromedioResultante = resultado.CostoPromedioResultante,
            KardexId = resultado.KardexId
        };
    }

    public async Task<IReadOnlyList<AjusteInventarioDto>> GetPorParAsync(int articuloId, int bodegaId, CancellationToken ct = default)
        => await _context.alm_ajuste_inventarios.AsNoTracking()
            .Where(a => a.articulo_id == articuloId && a.bodega_id == bodegaId)
            .OrderByDescending(a => a.id)
            .Select(a => new AjusteInventarioDto
            {
                Id = a.id,
                ArticuloId = a.articulo_id,
                BodegaId = a.bodega_id,
                Clase = a.clase,
                Cantidad = a.cantidad,
                CostoUnitario = a.costo_unitario,
                Motivo = a.motivo,
                Observacion = a.observacion,
                Posteado = a.posteado
            })
            .ToListAsync(ct);

    private static void ValidarPorClase(string clase, AjusteInventarioDto dto)
    {
        switch (clase)
        {
            case ClaseAjusteInventario.Entrada:
                ExigirCantidadPositiva(dto);
                ExigirCostoPositivo(dto, "Un ajuste de entrada debe indicar el costo unitario de lo que ingresa.");
                break;

            case ClaseAjusteInventario.Salida:
                ExigirCantidadPositiva(dto);
                // El costo NO se exige ni se usa: la salida se valoriza al promedio vigente.
                break;

            case ClaseAjusteInventario.Valor:
                if (dto.Cantidad != 0m)
                {
                    throw new InvalidOperationException(
                        "Un ajuste de valor no mueve existencia: la cantidad debe ser 0. Para mover unidades use ENTRADA o SALIDA.");
                }
                ExigirCostoPositivo(dto, "Un ajuste de valor debe indicar el costo unitario corregido.");
                break;
        }
    }

    private static void ExigirCantidadPositiva(AjusteInventarioDto dto)
    {
        if (dto.Cantidad <= 0m)
        {
            throw new InvalidOperationException("La cantidad del ajuste debe ser mayor que cero.");
        }
    }

    private static void ExigirCostoPositivo(AjusteInventarioDto dto, string mensaje)
    {
        if (dto.CostoUnitario <= 0m)
        {
            throw new InvalidOperationException(mensaje);
        }
    }

    private static TipoMovimientoInventario TipoMovimientoDe(string clase) => clase switch
    {
        ClaseAjusteInventario.Entrada => TipoMovimientoInventario.AjustePositivo,
        ClaseAjusteInventario.Salida => TipoMovimientoInventario.AjusteNegativo,
        ClaseAjusteInventario.Valor => TipoMovimientoInventario.AjusteValor,
        _ => throw new InvalidOperationException($"Clase de ajuste no soportada: {clase}.")
    };
}
