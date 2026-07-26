using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Caja;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Caja;

public class CajaService : ICajaService
{
    private readonly SiadDbContext _context;

    public CajaService(SiadDbContext context)
    {
        _context = context;
    }

    // ------------------------------------------------------------------
    public async Task<SesionCajaDto?> ObtenerSesionActivaAsync(string usuario)
    {
        var sesion = await _context.sesion_cajas
            .Where(s => s.usuario_apertura == usuario && s.estado == "ABIERTA")
            .FirstOrDefaultAsync();

        return sesion is null ? null : MapSesion(sesion);
    }

    // ------------------------------------------------------------------
    public async Task<CajaResponseDto> AbrirCajaAsync(AbrirCajaRequestDto request)
    {
        // Regla: el usuario solo puede tener una sesión abierta a la vez por empresa
        var yaAbierta = await _context.sesion_cajas
            .AnyAsync(s => s.usuario_apertura == request.UsuarioApertura && s.estado == "ABIERTA");

        if (yaAbierta)
            return new CajaResponseDto(false, "El usuario ya tiene una sesión de caja abierta.");

        // F3: la caja NO se elige — el sistema resuelve la caja ASIGNADA al
        // usuario (adm_caja_usuario). Sin asignación no se puede cobrar. Una
        // sola sesión ABIERTA por caja (índice único parcial en BD).
        var asignacion = await (
            from cu in _context.adm_caja_usuarios
            join c in _context.adm_cajas on cu.caja_id equals c.caja_id
            where cu.usuario == request.UsuarioApertura
            select new { c.caja_id, c.nombre, c.activo })
            .FirstOrDefaultAsync();

        if (asignacion is null)
            return new CajaResponseDto(false,
                "No tiene una caja asignada. Solicite al administrador que le asigne una en el mantenimiento de cajas.");
        if (!asignacion.activo)
            return new CajaResponseDto(false, $"La caja {asignacion.nombre} está inactiva.");

        var ocupadaPor = await _context.sesion_cajas
            .Where(s => s.caja_fisica_id == asignacion.caja_id && s.estado == "ABIERTA")
            .Select(s => s.usuario_apertura)
            .FirstOrDefaultAsync();
        if (ocupadaPor is not null)
            return new CajaResponseDto(false,
                $"La caja {asignacion.nombre} ya tiene una sesión abierta (cajero: {ocupadaPor}).");

        var sesion = new sesion_caja
        {
            usuario_apertura = request.UsuarioApertura,
            fecha_apertura   = DateTime.UtcNow,
            estado           = "ABIERTA",
            caja_fisica_id   = asignacion.caja_id
        };

        _context.sesion_cajas.Add(sesion);
        await _context.SaveChangesAsync();

        return new CajaResponseDto(true, $"Caja {asignacion.nombre} abierta correctamente.", sesion.id);
    }

    // ------------------------------------------------------------------
    public async Task<MiCajaDto?> ObtenerMiCajaAsync(string usuario)
    {
        // La caja asignada al usuario, con su disponibilidad actual.
        return await (
            from cu in _context.adm_caja_usuarios
            join c in _context.adm_cajas on cu.caja_id equals c.caja_id
            where cu.usuario == usuario
            select new MiCajaDto(
                c.caja_id,
                c.codigo,
                c.nombre,
                c.activo,
                _context.sesion_cajas.Any(s => s.caja_fisica_id == c.caja_id && s.estado == "ABIERTA"),
                _context.sesion_cajas
                    .Where(s => s.caja_fisica_id == c.caja_id && s.estado == "ABIERTA")
                    .Select(s => s.usuario_apertura)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync();
    }

    // ------------------------------------------------------------------
    public async Task<IReadOnlyList<CajaFisicaDto>> ListarCajasAsync()
    {
        // Cajas de la empresa con su disponibilidad (Ocupada = sesión ABIERTA).
        return await _context.adm_cajas
            .AsNoTracking()
            .Where(c => c.activo)
            .OrderBy(c => c.codigo)
            .Select(c => new CajaFisicaDto(
                c.caja_id,
                c.codigo,
                c.nombre,
                c.activo,
                _context.sesion_cajas.Any(s => s.caja_fisica_id == c.caja_id && s.estado == "ABIERTA")))
            .ToListAsync();
    }

    // ------------------------------------------------------------------
    // Mantenimiento de cajas + asignación de cajeros (F3)
    // ------------------------------------------------------------------

    public async Task<IReadOnlyList<CajaAdminDto>> ListarCajasAdminAsync()
    {
        var cajas = await _context.adm_cajas
            .AsNoTracking()
            .OrderBy(c => c.codigo)
            .Select(c => new
            {
                c.caja_id,
                c.codigo,
                c.nombre,
                c.activo,
                Ocupada = _context.sesion_cajas.Any(s => s.caja_fisica_id == c.caja_id && s.estado == "ABIERTA"),
                Asignados = _context.adm_caja_usuarios
                    .Where(cu => cu.caja_id == c.caja_id)
                    .OrderBy(cu => cu.usuario)
                    .Select(cu => cu.usuario)
                    .ToList()
            })
            .ToListAsync();

        return cajas
            .Select(c => new CajaAdminDto(c.caja_id, c.codigo, c.nombre, c.activo, c.Ocupada, c.Asignados))
            .ToList();
    }

    public async Task<CajaResponseDto> GuardarCajaAsync(CajaGuardarDto dto, string usuario)
    {
        if (string.IsNullOrWhiteSpace(dto.Codigo) || string.IsNullOrWhiteSpace(dto.Nombre))
            return new CajaResponseDto(false, "El código y el nombre de la caja son requeridos.");

        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        var duplicada = await _context.adm_cajas
            .AnyAsync(c => c.codigo == codigo && c.caja_id != (dto.CajaId ?? 0));
        if (duplicada)
            return new CajaResponseDto(false, $"Ya existe una caja con el código {codigo}.");

        if (dto.CajaId is > 0)
        {
            var caja = await _context.adm_cajas.FirstOrDefaultAsync(c => c.caja_id == dto.CajaId.Value);
            if (caja is null)
                return new CajaResponseDto(false, "No se encontró la caja indicada.");

            caja.codigo = codigo;
            caja.nombre = dto.Nombre.Trim();
            caja.activo = dto.Activo;
            caja.actualizado_en = DateTime.UtcNow;
            caja.updated_by = usuario;
            await _context.SaveChangesAsync();
            return new CajaResponseDto(true, "Caja actualizada.", caja.caja_id);
        }

        var nueva = new adm_caja
        {
            codigo = codigo,
            nombre = dto.Nombre.Trim(),
            activo = dto.Activo,
            updated_by = usuario
        };
        _context.adm_cajas.Add(nueva);
        await _context.SaveChangesAsync();
        return new CajaResponseDto(true, "Caja creada.", nueva.caja_id);
    }

    public async Task<CajaResponseDto> AsignarCajeroAsync(AsignarCajeroDto dto, string usuario)
    {
        if (string.IsNullOrWhiteSpace(dto.Usuario))
            return new CajaResponseDto(false, "Debe indicar el usuario a asignar.");

        var caja = await _context.adm_cajas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.caja_id == dto.CajaId);
        if (caja is null)
            return new CajaResponseDto(false, "No se encontró la caja indicada.");

        var cajero = dto.Usuario.Trim();
        var existente = await _context.adm_caja_usuarios
            .FirstOrDefaultAsync(cu => cu.usuario == cajero);
        if (existente is not null)
        {
            // Un usuario pertenece a UNA caja: reasignar lo mueve de caja.
            existente.caja_id = dto.CajaId;
            existente.updated_by = usuario;
        }
        else
        {
            _context.adm_caja_usuarios.Add(new adm_caja_usuario
            {
                caja_id = dto.CajaId,
                usuario = cajero,
                updated_by = usuario
            });
        }
        await _context.SaveChangesAsync();
        return new CajaResponseDto(true, $"{cajero} asignado a {caja.nombre}.");
    }

    public async Task<CajaResponseDto> QuitarCajeroAsync(string cajero)
    {
        var existente = await _context.adm_caja_usuarios
            .FirstOrDefaultAsync(cu => cu.usuario == cajero);
        if (existente is null)
            return new CajaResponseDto(false, "El usuario no tiene caja asignada.");

        _context.adm_caja_usuarios.Remove(existente);
        await _context.SaveChangesAsync();
        return new CajaResponseDto(true, $"Asignación de {cajero} eliminada.");
    }

    // ------------------------------------------------------------------
    public async Task<CajaResponseDto> CerrarCajaAsync(CerrarCajaRequestDto request)
    {
        var sesion = await _context.sesion_cajas
            .FirstOrDefaultAsync(s => s.id == request.SesionId && s.estado == "ABIERTA");

        if (sesion is null)
            return new CajaResponseDto(false, "Sesión no encontrada o ya cerrada.");

        // Total = créditos de transacciones que referencian esta sesión (caja_id = sesion.id)
        var totalCreditos = await _context.transaccion_abonados
            .Where(t => t.caja_id == sesion.id && t.estado != "N")
            .SumAsync(t => t.creditos) ?? 0m;

        sesion.estado         = "CERRADA";
        sesion.usuario_cierre = request.UsuarioCierre;
        sesion.fecha_cierre   = DateTime.UtcNow;
        sesion.total_cobrado  = totalCreditos;
        sesion.observacion    = request.Observacion;

        await _context.SaveChangesAsync();

        return new CajaResponseDto(true, "Caja cerrada correctamente.", sesion.id);
    }

    // ------------------------------------------------------------------
    public async Task<ResumenCajaDto?> ObtenerResumenAsync(int sesionId)
    {
        var existe = await _context.sesion_cajas.AnyAsync(s => s.id == sesionId);
        if (!existe) return null;

        // transaccion_abonado.caja_id almacena sesion_caja.id como referencia libre
        var grupos = await _context.transaccion_abonados
            .Where(t => t.caja_id == sesionId && t.estado != "N")
            .GroupBy(t => t.tipotransaccion ?? "SIN TIPO")
            .Select(g => new ResumenPorTipoDto(
                g.Key,
                g.Sum(t => t.creditos ?? 0),
                g.Sum(t => t.debitos ?? 0),
                g.Count()))
            .ToListAsync();

        return new ResumenCajaDto(
            grupos.Sum(g => g.Creditos),
            grupos.Sum(g => g.Debitos),
            grupos.Sum(g => g.Cantidad),
            grupos);
    }

    // ------------------------------------------------------------------
    public async Task<IReadOnlyList<HistorialCierreDto>> ListarHistorialAsync(string usuario)
    {
        return await _context.sesion_cajas
            .Where(s => s.usuario_apertura == usuario && s.estado == "CERRADA")
            .OrderByDescending(s => s.fecha_cierre)
            .Select(s => new HistorialCierreDto(
                s.id,
                s.fecha_apertura,
                s.fecha_cierre,
                s.usuario_apertura,
                s.usuario_cierre,
                s.total_cobrado))
            .ToListAsync();
    }

    // ------------------------------------------------------------------
    private static SesionCajaDto MapSesion(sesion_caja s) => new(
        s.id,
        s.usuario_apertura,
        s.fecha_apertura,
        s.usuario_cierre,
        s.fecha_cierre,
        s.estado,
        s.total_cobrado,
        s.caja_fisica_id);
}
