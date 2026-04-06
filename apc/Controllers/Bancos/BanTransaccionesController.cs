using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using SIAD.Services;
using SIAD.Services.Bancos;
using System.IO;
using System.Security.Claims;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/transacciones")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BanTransaccionesController : ControllerBase
{
    private readonly IBanTransaccionesService transaccionesService;
    private readonly ICurrentCompanyService currentCompanyService;

    public BanTransaccionesController(
        IBanTransaccionesService transaccionesService,
        ICurrentCompanyService currentCompanyService)
    {
        this.transaccionesService = transaccionesService;
        this.currentCompanyService = currentCompanyService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BanTransaccionListDto>>> GetTransacciones(
        long companyId,
        [FromQuery] long? bancoId = null,
        [FromQuery] long? bancoCuentaId = null,
        [FromQuery] DateOnly? fechaDesde = null,
        [FromQuery] DateOnly? fechaHasta = null,
        [FromQuery] bool incluirAnuladas = false,
        CancellationToken ct = default)
    {
        try
        {
            // Validar que la empresa sea válida
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compañía Inválida",
                    "El ID de la compañía debe ser un número positivo."));
            }

            var transacciones = await transaccionesService.GetTransaccionesAsync(
                companyId,
                bancoId,
                bancoCuentaId,
                fechaDesde,
                fechaHasta,
                incluirAnuladas,
                ct);

            return Ok(transacciones);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parámetro Inválido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible obtener las transacciones bancarias: {ex.Message}"));
        }
    }

    [HttpGet("{banKardexId}")]
    public async Task<ActionResult<BanTransaccionListDto>> GetTransaccionById(
        long banKardexId,
        [FromQuery] long companyId,
        CancellationToken ct = default)
    {
        try
        {
            if (banKardexId <= 0 || companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parámetros Inválidos",
                    "Los IDs deben ser números positivos."));
            }

            var transaccion = await transaccionesService.GetTransaccionByIdAsync(
                banKardexId,
                companyId,
                ct);

            if (transaccion == null)
            {
                return NotFound(CrearProblemDetalle(
                    "No Encontrada",
                    "La transacción bancaria especificada no existe."));
            }

            return Ok(transaccion);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parámetro Inválido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible obtener la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpGet("{banKardexId}/detalle")]
    public async Task<ActionResult<BanTransaccionDetalleDto>> GetTransaccionDetalle(
        long banKardexId,
        [FromQuery] long companyId,
        CancellationToken ct = default)
    {
        try
        {
            if (banKardexId <= 0 || companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parámetros Inválidos",
                    "Los IDs deben ser números positivos."));
            }

            var transaccion = await transaccionesService.GetTransaccionDetalleAsync(
                banKardexId,
                companyId,
                ct);

            if (transaccion == null)
            {
                return NotFound(CrearProblemDetalle(
                    "No Encontrada",
                    "La transacción bancaria especificada no existe."));
            }

            return Ok(transaccion);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parámetro Inválido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible obtener el detalle de la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpGet("{banKardexId}/reporte")]
    public async Task<IActionResult> GetReporteTransaccion(
        long banKardexId,
        CancellationToken ct = default)
    {
        try
        {
            if (banKardexId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Parametros Invalidos",
                    "El ID de la transaccion debe ser un numero positivo."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compania Invalida",
                    "No fue posible determinar la empresa actual."));
            }

            var transaccion = await transaccionesService.GetTransaccionByIdAsync(
                banKardexId,
                companyId,
                ct);

            if (transaccion is null)
            {
                return NotFound(CrearProblemDetalle(
                    "No Encontrada",
                    "La transaccion bancaria especificada no existe."));
            }

            var reporte = new Rpt_DE_Transacciones_Bancarias
            {
                DataSource = new[] { transaccion }
            };

            using var stream = new MemoryStream();
            reporte.ExportToPdf(stream);

            return File(stream.ToArray(), "application/pdf");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Parametro Invalido", ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible generar el reporte: {ex.Message}"));
        }
    }

    [HttpPost]
    public async Task<ActionResult<(long, decimal)>> RegistrarMovimiento(
        [FromBody] BanTransaccionCreateDto dto,
        CancellationToken ct = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(CrearProblemDetalle(
                    "Validación Fallida",
                    "Los datos proporcionados no son válidos."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compañía Inválida",
                    "El ID de la compañía debe ser un número positivo."));
            }

            var usuario = User.Identity?.Name 
                          ?? User.FindFirst(ClaimTypes.Email)?.Value 
                          ?? "Sistema";

            var contraLineas = dto.ContraCuentas?
                .Where(l => l != null && l.CuentaId > 0 && l.Monto > 0)
                .ToList()
                ?? new List<BanTransaccionContraLineaDto>();

            if (contraLineas.Count == 0 && dto.ContraCuentaId.HasValue && dto.ContraCuentaId.Value > 0)
            {
                contraLineas.Add(new BanTransaccionContraLineaDto
                {
                    CuentaId = dto.ContraCuentaId.Value,
                    Monto = dto.Monto,
                    Descripcion = dto.Descripcion,
                    SourceDocument = string.IsNullOrWhiteSpace(dto.SourceDocument) ? dto.Referencia : dto.SourceDocument
                });
            }

            if (contraLineas.Count == 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Validación Fallida",
                    "Agregue al menos una contracuenta válida."));
            }

            var resultado = await transaccionesService.RegistrarMovimientoAsync(
                dto.BancoCuentaId,
                dto.IdTipoTransaccion,
                dto.FechaMovimiento,
                dto.Descripcion,
                dto.Referencia,
                dto.SourceDocument,
                dto.TasaCambio,
                dto.Monto,
                contraLineas,
                usuario,
                ct);

            return Ok(resultado);
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(CrearProblemDetalle(
                "Validación Fallida",
                $"Campo requerido: {ex.ParamName}"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Validación Fallida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(CrearProblemDetalle(
                "Recurso No Encontrado",
                ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible registrar la transacción bancaria: {ex.Message}"));
        }
    }

    [HttpPost("anular")]
    public async Task<ActionResult<(long, decimal)>> AnularMovimiento(
        [FromBody] BanTransaccionAnularDto dto,
        CancellationToken ct = default)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(CrearProblemDetalle(
                    "Validación Fallida",
                    "Los datos proporcionados no son válidos."));
            }

            var companyId = currentCompanyService.GetCompanyId();
            if (companyId <= 0)
            {
                return BadRequest(CrearProblemDetalle(
                    "Compañía Inválida",
                    "El ID de la compañía debe ser un número positivo."));
            }

            var usuario = User.Identity?.Name 
                          ?? User.FindFirst(ClaimTypes.Email)?.Value 
                          ?? "Sistema";

            var resultado = await transaccionesService.AnularMovimientoAsync(
                dto.BancoCuentaId,
                dto.BanKardexIdOriginal,
                dto.Motivo,
                usuario,
                ct);

            return Ok(resultado);
        }
        catch (ArgumentNullException ex)
        {
            return BadRequest(CrearProblemDetalle(
                "Validación Fallida",
                $"Campo requerido: {ex.ParamName}"));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(CrearProblemDetalle("Validación Fallida", ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(CrearProblemDetalle(
                "Recurso No Encontrado",
                ex.Message));
        }
        catch (PostgresException ex)
        {
            var detalle = string.IsNullOrWhiteSpace(ex.MessageText) ? ex.Message : ex.MessageText;
            return BadRequest(CrearProblemDetalle(
                "Error de Base de Datos",
                detalle));
        }
        catch (Exception ex)
        {
            return StatusCode(500, CrearProblemDetalle(
                "Error del Servidor",
                $"No fue posible anular la transacción bancaria: {ex.Message}"));
        }
    }

    private ProblemDetails CrearProblemDetalle(string titulo, string detalle)
    {
        return new ProblemDetails
        {
            Title = titulo,
            Detail = detalle,
            Status = StatusCodes.Status400BadRequest
        };
    }
}




