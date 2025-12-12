using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Contabilidad;

public sealed class ConfiguracionSistemaDto : IValidatableObject
{
    [Required]
    public ConfiguracionPrincipalDto Principal { get; set; } = new();

    [Required]
    public CuentasUtilidadDto CuentasUtilidad { get; set; } = new();

    [Required]
    public EstadoSituacionFinancieraDto EstadoSituacionFinanciera { get; set; } = new();

    [MinLength(0)]
    public List<LineaResultadoDto> LineasResultado { get; set; } = new();

    [MinLength(0)]
    public List<BalanceSheetLineDto> LineasBalance { get; set; } = new();

    [MinLength(0)]
    public List<CorrelativoDto> Correlativos { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if ((CuentasUtilidad?.Seleccionadas ?? 0) == 0)
        {
            yield return new ValidationResult(
                "Selecciona al menos una cuenta de utilidad.",
                new[] { nameof(CuentasUtilidad) });
        }

        if ((EstadoSituacionFinanciera?.Seleccionadas ?? 0) == 0)
        {
            yield return new ValidationResult(
                "Selecciona al menos una cuenta en el estado de situacion financiera.",
                new[] { nameof(EstadoSituacionFinanciera) });
        }

        if (LineasResultado is not null && LineasResultado.Count > 0)
        {
            var duplicados = LineasResultado
                .Where(e => !string.IsNullOrWhiteSpace(e.CodigoCuenta))
                .GroupBy(e => e.CodigoCuenta?.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicados.Count > 0)
            {
                yield return new ValidationResult(
                    "Hay codigos repetidos en el estado de resultados.",
                    new[] { nameof(LineasResultado) });
            }
        }
    }
}

public sealed class ConfiguracionPrincipalDto : IValidatableObject
{
    [Required(ErrorMessage = "La fecha de inicio es obligatoria.")]
    public DateTime? FechaInicioEjercicio { get; set; }

    [Required(ErrorMessage = "La fecha de fin es obligatoria.")]
    public DateTime? FechaFinEjercicio { get; set; }

    public int MesesCalculados { get; set; }

    [Required]
    [RegularExpression(@"^[\.\-;]$", ErrorMessage = "Selecciona un separador valido.")]
    public string SeparadorCodigo { get; set; } = "-";

    [Required]
    [RegularExpression(@"^[#\.\-;]+$", ErrorMessage = "El formato de cuentas solo permite #, '.', '-' o ';'.")]
    public string FormatoCuentas { get; set; } = "###-###-##";

    [Required]
    [RegularExpression(@"^[#\.\-;]+$", ErrorMessage = "El formato de centros solo permite #, '.', '-' o ';'.")]
    public string FormatoCentros { get; set; } = "###-##";

    [Required]
    public string SymbolSaldoAcreedor { get; set; } = "CR";

    [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "El monto maximo debe ser igual o mayor a cero.")]
    public decimal? MontoMaximo { get; set; }

    [Required]
    public string FrecuenciaDepreciacion { get; set; } = "Mensual";

    public DateTime? UltimaDepreciacion { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FechaInicioEjercicio.HasValue && FechaFinEjercicio.HasValue && FechaFinEjercicio < FechaInicioEjercicio)
        {
            yield return new ValidationResult(
                "La fecha fin debe ser posterior a la fecha de inicio.",
                new[] { nameof(FechaFinEjercicio) });
        }

        var simbolosPermitidos = new[] { "CR", "Cr.", "()" };
        if (!string.IsNullOrWhiteSpace(SymbolSaldoAcreedor) &&
            !simbolosPermitidos.Contains(SymbolSaldoAcreedor, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Selecciona un simbolo de saldo acreedor valido.",
                new[] { nameof(SymbolSaldoAcreedor) });
        }

        var frecuenciasPermitidas = new[] { "Anual", "Mensual", "Diario" };
        if (!string.IsNullOrWhiteSpace(FrecuenciaDepreciacion) &&
            !frecuenciasPermitidas.Contains(FrecuenciaDepreciacion, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                "Selecciona una frecuencia de depreciacion valida.",
                new[] { nameof(FrecuenciaDepreciacion) });
        }
    }
}

public sealed class CuentasUtilidadDto
{
    public string? CodigoCuentaUtilAcumuladaHistorica { get; set; }
    public string? CodigoCuentaUtilAcumuladaInflacion { get; set; }
    public string? CodigoCuentaUtilEjercicioHistorica { get; set; }
    public string? CodigoCuentaUtilEjercicioInflacion { get; set; }
    public string? CodigoCuentaPerdidaAcumuladaHistorica { get; set; }
    public string? CodigoCuentaPerdidaAcumuladaInflacion { get; set; }
    public string? CodigoCuentaPerdidaEjercicioHistorica { get; set; }
    public string? CodigoCuentaPerdidaEjercicioInflacion { get; set; }

    [JsonIgnore]
    public int Seleccionadas => new string?[]
    {
        CodigoCuentaUtilAcumuladaHistorica,
        CodigoCuentaUtilAcumuladaInflacion,
        CodigoCuentaUtilEjercicioHistorica,
        CodigoCuentaUtilEjercicioInflacion,
        CodigoCuentaPerdidaAcumuladaHistorica,
        CodigoCuentaPerdidaAcumuladaInflacion,
        CodigoCuentaPerdidaEjercicioHistorica,
        CodigoCuentaPerdidaEjercicioInflacion
    }.Count(id => !string.IsNullOrWhiteSpace(id));
}

public sealed class EstadoSituacionFinancieraDto
{
    // Activos - ahora con c�digos
    public string? CodigoActivoCortoPlazo1 { get; set; }
    public string? CodigoActivoCortoPlazo2 { get; set; }
    public string? CodigoActivoLargoPlazo1 { get; set; }
    public string? CodigoActivoLargoPlazo2 { get; set; }
    
    // Pasivos - ahora con c�digos
    public string? CodigoPasivoCortoPlazo1 { get; set; }
    public string? CodigoPasivoCortoPlazo2 { get; set; }
    public string? CodigoPasivoLargoPlazo1 { get; set; }
    public string? CodigoPasivoLargoPlazo2 { get; set; }
    
    // Pasivo y Capital
    public string? CodigoPasivoyCapital { get; set; }
    
    // Capital
    public string? CodigoCapitalAportado { get; set; }
    public string? CodigoResultadosAcumulados { get; set; }
    public string? CodigoUtilidadPerdidaEjercicio { get; set; }
    public string? CodigoSobrevaluaciones { get; set; }
    public bool MostrarOrden { get; set; }
    public bool MostrarPercontra { get; set; }

    [JsonIgnore]
    public int Seleccionadas => new string?[]
    {
        CodigoActivoCortoPlazo1,
        CodigoActivoCortoPlazo2,
        CodigoActivoLargoPlazo1,
        CodigoActivoLargoPlazo2,
        CodigoPasivoCortoPlazo1,
        CodigoPasivoCortoPlazo2,
        CodigoPasivoLargoPlazo1,
        CodigoPasivoLargoPlazo2,
        CodigoPasivoyCapital,
        CodigoCapitalAportado,
        CodigoResultadosAcumulados,
        CodigoUtilidadPerdidaEjercicio,
        CodigoSobrevaluaciones
    }.Count(id => !string.IsNullOrWhiteSpace(id));
}

public sealed class LineaResultadoDto
{
    [JsonIgnore]
    public Guid RowId { get; set; } = Guid.NewGuid();

    [Required]
    [RegularExpression(@"^(Ingreso|Costo|Gasto)$", ErrorMessage = "Selecciona un tipo valido.")]
    public string Tipo { get; set; } = "Ingreso";

    [StringLength(30, ErrorMessage = "El codigo de cuenta no debe superar 30 caracteres.")]
    public string? CodigoCuenta { get; set; }

    [Required]
    [StringLength(200, ErrorMessage = "La descripcion no debe superar 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    public byte NivelIndentacion { get; set; } = 0;

    public bool MostrarSubtotal { get; set; } = false;
}

public sealed class BalanceSheetLineDto
{
    [JsonIgnore]
    public Guid RowId { get; set; } = Guid.NewGuid();

    [Required]
    [Range(1, 8, ErrorMessage = "La clase debe estar entre 1 y 8.")]
    public byte Clase { get; set; }

    [StringLength(30, ErrorMessage = "El codigo de cuenta no debe superar 30 caracteres.")]
    public string? CodigoCuenta { get; set; }

    [Required]
    [StringLength(200, ErrorMessage = "La descripcion no debe superar 200 caracteres.")]
    public string Descripcion { get; set; } = string.Empty;

    public decimal? PorcentajeActivo { get; set; }

    public bool MostrarEnReporte { get; set; } = true;
}

public sealed class CorrelativoDto
{
    [JsonIgnore]
    public Guid RowId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(80, ErrorMessage = "El tipo no debe superar 80 caracteres.")]
    public string Tipo { get; set; } = string.Empty;

    [Required]
    [StringLength(80, ErrorMessage = "El numerador no debe superar 80 caracteres.")]
    public string Numerador { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "El siguiente numero debe ser mayor a cero.")]
    public int SiguienteNumero { get; set; } = 1;

    [StringLength(80, ErrorMessage = "El formato no debe superar 80 caracteres.")]
    public string? Formato { get; set; }
}
