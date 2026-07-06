using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.CondicionesLectura;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.CondicionesLectura;

/// <summary>
/// Implementación del ABM de condiciones de lectura por empresa. Usa el filtro
/// global de tenant de SiadDbContext: todas las consultas quedan acotadas al
/// tenant actual; el companyId explícito refuerza el scope y valida entrada.
/// </summary>
public sealed class CondicionesLecturaService : ICondicionesLecturaService
{
    private static readonly string[] FlagsValidos = { "S", "N" };

    private readonly SiadDbContext _context;

    public CondicionesLecturaService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<CondicionesLecturaCatalogoDto> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var tipos = await _context.adm_condicion_lectura_tipos
            .AsNoTracking()
            .OrderBy(t => t.tipo == "N" ? 0 : 1)
            .ThenBy(t => t.tipo)
            .Select(t => new CondicionLecturaTipoDto
            {
                Tipo = t.tipo,
                Descripcion = t.descripcion,
                RequiereLectura = t.requiere_lectura,
            })
            .ToListAsync(ct);

        var condiciones = await _context.adm_condicion_lecturas
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .OrderBy(c => c.orden)
            .ThenBy(c => c.codigo)
            .Select(c => new CondicionLecturaAdminDto
            {
                CondicionLecturaId = c.condicion_lectura_id,
                Codigo = c.codigo,
                Descripcion = c.descripcion,
                Tipo = c.tipo,
                Facturacion = c.facturacion,
                AplicaDescuento = c.aplica_descuento,
                Orden = c.orden,
                Activo = c.activo,
            })
            .ToListAsync(ct);

        return new CondicionesLecturaCatalogoDto { Tipos = tipos, Condiciones = condiciones };
    }

    public async Task<CondicionesLecturaCatalogoDto> GuardarAsync(long companyId, List<CondicionLecturaAdminDto> condiciones, string usuario, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        ArgumentNullException.ThrowIfNull(condiciones);
        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

        // Normaliza y valida la entrada antes de tocar la BD.
        var entrada = Normalizar(condiciones);
        var tiposValidos = await _context.adm_condicion_lectura_tipos
            .AsNoTracking()
            .Select(t => t.tipo)
            .ToListAsync(ct);
        Validar(entrada, tiposValidos);

        var ahora = DateTime.UtcNow;
        // Abre transacción propia solo si no hay una ambiente (los tests de
        // integración corren dentro de la transacción del fixture — reusarla).
        var txPropia = _context.Database.CurrentTransaction is null
            ? await _context.Database.BeginTransactionAsync(ct)
            : null;

        var existentes = await _context.adm_condicion_lecturas
            .Where(c => c.company_id == companyId)
            .ToListAsync(ct);
        var existentesPorId = existentes.ToDictionary(c => c.condicion_lectura_id);

        var idsEntrantes = new HashSet<long>(entrada.Where(c => c.CondicionLecturaId > 0).Select(c => c.CondicionLecturaId));

        // Borra las que ya no vienen (acotado al tenant por el Where de arriba).
        foreach (var borrar in existentes.Where(c => !idsEntrantes.Contains(c.condicion_lectura_id)))
        {
            _context.adm_condicion_lecturas.Remove(borrar);
        }

        foreach (var dto in entrada)
        {
            if (dto.CondicionLecturaId > 0 && existentesPorId.TryGetValue(dto.CondicionLecturaId, out var fila))
            {
                fila.codigo = dto.Codigo;
                fila.descripcion = dto.Descripcion;
                fila.tipo = dto.Tipo;
                fila.facturacion = dto.Facturacion;
                fila.aplica_descuento = dto.AplicaDescuento;
                fila.orden = dto.Orden;
                fila.activo = dto.Activo;
                fila.updated_at = ahora;
                fila.updated_by = usuario;
            }
            else
            {
                _context.adm_condicion_lecturas.Add(new adm_condicion_lectura
                {
                    company_id = companyId,
                    codigo = dto.Codigo,
                    descripcion = dto.Descripcion,
                    tipo = dto.Tipo,
                    facturacion = dto.Facturacion,
                    aplica_descuento = dto.AplicaDescuento,
                    orden = dto.Orden,
                    activo = dto.Activo,
                    created_at = ahora,
                    created_by = usuario,
                });
            }
        }

        await _context.SaveChangesAsync(ct);
        if (txPropia is not null)
        {
            await txPropia.CommitAsync(ct);
            await txPropia.DisposeAsync();
        }

        return await ObtenerAsync(companyId, ct);
    }

    private static List<CondicionLecturaAdminDto> Normalizar(List<CondicionLecturaAdminDto> condiciones)
    {
        return condiciones.Select(c => new CondicionLecturaAdminDto
        {
            CondicionLecturaId = c.CondicionLecturaId,
            Codigo = (c.Codigo ?? string.Empty).Trim().ToUpperInvariant(),
            Descripcion = (c.Descripcion ?? string.Empty).Trim(),
            Tipo = (c.Tipo ?? string.Empty).Trim().ToUpperInvariant(),
            Facturacion = string.IsNullOrWhiteSpace(c.Facturacion) ? "S" : c.Facturacion.Trim().ToUpperInvariant(),
            AplicaDescuento = string.IsNullOrWhiteSpace(c.AplicaDescuento) ? "N" : c.AplicaDescuento.Trim().ToUpperInvariant(),
            Orden = c.Orden,
            Activo = c.Activo,
        }).ToList();
    }

    private static void Validar(List<CondicionLecturaAdminDto> condiciones, ICollection<string> tiposValidos)
    {
        foreach (var c in condiciones)
        {
            if (string.IsNullOrWhiteSpace(c.Codigo))
            {
                throw new InvalidOperationException("El código de la condición es obligatorio.");
            }

            if (c.Codigo.Length > 10)
            {
                throw new InvalidOperationException($"El código '{c.Codigo}' excede 10 caracteres.");
            }

            if (string.IsNullOrWhiteSpace(c.Descripcion))
            {
                throw new InvalidOperationException($"La descripción de '{c.Codigo}' es obligatoria.");
            }

            if (!tiposValidos.Contains(c.Tipo))
            {
                throw new InvalidOperationException($"El tipo '{c.Tipo}' de la condición '{c.Codigo}' no es válido.");
            }

            if (!FlagsValidos.Contains(c.Facturacion))
            {
                throw new InvalidOperationException($"Facturación de '{c.Codigo}' debe ser 'S' o 'N'.");
            }

            if (!FlagsValidos.Contains(c.AplicaDescuento))
            {
                throw new InvalidOperationException($"Aplica descuento de '{c.Codigo}' debe ser 'S' o 'N'.");
            }
        }

        var duplicado = condiciones
            .GroupBy(c => c.Codigo)
            .FirstOrDefault(g => g.Count() > 1);
        if (duplicado is not null)
        {
            throw new InvalidOperationException($"El código '{duplicado.Key}' está duplicado en el catálogo.");
        }
    }

    private static void ValidarCompanyId(long companyId)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }
    }
}
