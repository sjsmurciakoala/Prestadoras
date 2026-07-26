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

        // Varias cajas físicas simultáneas (unificación cobranza F2): la sesión
        // se abre EN una caja concreta y solo puede haber una sesión abierta por
        // caja (respaldado además por índice único parcial en BD).
        if (request.CajaFisicaId.HasValue)
        {
            var caja = await _context.adm_cajas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.caja_id == request.CajaFisicaId.Value && c.activo);
            if (caja is null)
                return new CajaResponseDto(false, "La caja indicada no existe o está inactiva.");

            var cajaOcupada = await _context.sesion_cajas
                .AnyAsync(s => s.caja_fisica_id == request.CajaFisicaId.Value && s.estado == "ABIERTA");
            if (cajaOcupada)
                return new CajaResponseDto(false, $"La caja {caja.nombre} ya tiene una sesión abierta.");
        }

        var sesion = new sesion_caja
        {
            usuario_apertura = request.UsuarioApertura,
            fecha_apertura   = DateTime.UtcNow,
            estado           = "ABIERTA",
            caja_fisica_id   = request.CajaFisicaId
        };

        _context.sesion_cajas.Add(sesion);
        await _context.SaveChangesAsync();

        return new CajaResponseDto(true, "Caja abierta correctamente.", sesion.id);
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
