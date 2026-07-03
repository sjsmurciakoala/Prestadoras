using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// Usos de cuenta de la matriz de integración contable (plan 2026-07-02 F1).
/// Deben coincidir con el CHECK ck_con_integracion_cuenta_uso.
/// </summary>
public static class IntegracionContableUsos
{
    public const string Cxc = "CXC";
    public const string Ingreso = "INGRESO";
    public const string Caja = "CAJA";
    public const string BancoDefault = "BANCO_DEFAULT";
    public const string Isv = "ISV";
    public const string Descuento = "DESCUENTO";
    public const string RecargoMora = "RECARGO_MORA";
    public const string PrevisionIncobrable = "PREVISION_INCOBRABLE";
    public const string GastoIncobrable = "GASTO_INCOBRABLE";
    public const string ResultadoEjercicio = "RESULTADO_EJERCICIO";
    public const string ResultadoAcumulado = "RESULTADO_ACUMULADO";
    public const string DevolucionNc = "DEVOLUCION_NC";
    public const string Transitoria = "TRANSITORIA";

    public static readonly string[] Todos =
    [
        Cxc, Ingreso, Caja, BancoDefault, Isv, Descuento, RecargoMora,
        PrevisionIncobrable, GastoIncobrable, ResultadoEjercicio,
        ResultadoAcumulado, DevolucionNc, Transitoria
    ];

    /// <summary>Usos que aceptan dimensiones servicio × categoría × medición.</summary>
    public static readonly string[] Dimensionables =
    [
        Cxc, Ingreso, PrevisionIncobrable, Descuento, RecargoMora
    ];

    /// <summary>Usos generales cuya fila comodín es obligatoria para operar.</summary>
    public static readonly string[] GeneralesRequeridos = [Cxc, Ingreso, Caja];
}

/// <summary>Modos de granularidad de con_integracion_config.</summary>
public static class IntegracionContableModos
{
    public const string General = "GENERAL";
    public const string PorServicio = "POR_SERVICIO";
    public const string PorServicioCategoria = "POR_SERVICIO_CATEGORIA";

    public static readonly string[] Todos = [General, PorServicio, PorServicioCategoria];
}

/// <summary>Módulos comerciales que generan partidas (con_integracion_asiento.module).</summary>
public static class IntegracionContableModulos
{
    public const string Facturacion = "FACTURACION";
    public const string Caja = "CAJA";
    public const string Bancos = "BANCOS";
    public const string Notas = "NOTAS";
    public const string Miscelaneos = "MISCELANEOS";

    public static readonly string[] Todos = [Facturacion, Caja, Bancos, Notas, Miscelaneos];
}

/// <summary>Cabecera de configuración (con_integracion_config).</summary>
public sealed class IntegracionConfigDto
{
    public string ModoVentas { get; set; } = IntegracionContableModos.General;
    public string ModoCxc { get; set; } = IntegracionContableModos.General;
    public bool EncolarSinPeriodo { get; set; } = true;
}

/// <summary>Fila de la matriz de cuentas (con_integracion_cuenta).</summary>
public sealed class IntegracionCuentaDto
{
    /// <summary>0 = fila nueva aún no persistida.</summary>
    public long IntegracionCuentaId { get; set; }

    public string Uso { get; set; } = string.Empty;
    public long? ServicioId { get; set; }
    public int? CategoriaServicioId { get; set; }
    public bool? ConMedicion { get; set; }
    public long? AccountId { get; set; }
    public bool IsActive { get; set; } = true;

    // Solo lectura, para mostrar en el grid sin lookups adicionales.
    public string? AccountCode { get; set; }
    public string? AccountName { get; set; }
    public string? ServicioNombre { get; set; }
    public string? CategoriaDescripcion { get; set; }

    /// <summary>Clave de fila para la UI (grids Blazor).</summary>
    public Guid RowId { get; set; } = Guid.NewGuid();
}

/// <summary>Diario y tipo de partida por módulo (con_integracion_asiento) +
/// flag de activación (columna activo_* de con_integracion_config).</summary>
public sealed class IntegracionAsientoDto
{
    public string Module { get; set; } = string.Empty;
    public long? JournalId { get; set; }
    public long? TypeId { get; set; }
    public bool Activo { get; set; }
}

/// <summary>Configuración completa de integración contable de una empresa.</summary>
public sealed class IntegracionContableDto
{
    /// <summary>false = la empresa aún no tiene cabecera (config por defecto).</summary>
    public bool Existe { get; set; }

    public IntegracionConfigDto Config { get; set; } = new();
    public List<IntegracionCuentaDto> Cuentas { get; set; } = new();
    public List<IntegracionAsientoDto> Asientos { get; set; } = new();
}

/// <summary>Resultado de sp_con_aplicar_perfil_integracion.</summary>
public sealed class IntegracionPerfilResultDto
{
    public int Insertadas { get; set; }
    public int Existentes { get; set; }
    public int SinCuenta { get; set; }
}

/// <summary>Resultado de validación de la configuración (huecos, posteo).</summary>
public sealed class IntegracionValidacionDto
{
    public List<string> Errores { get; set; } = new();
    public List<string> Advertencias { get; set; } = new();
    public bool EsValida => Errores.Count == 0;
}

/// <summary>Respuesta de guardado: configuración persistida + validación.</summary>
public sealed class IntegracionGuardarResultDto
{
    public IntegracionContableDto Configuracion { get; set; } = new();
    public IntegracionValidacionDto Validacion { get; set; } = new();
}

/// <summary>Servicio comercial activo (adm_servicio) para la matriz.</summary>
public sealed class ServicioIntegracionLookupDto
{
    public long ServicioId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string Display => string.IsNullOrWhiteSpace(Nombre) ? Codigo : $"{Codigo} - {Nombre}";
}

/// <summary>Categoría de servicio activa para la matriz.</summary>
public sealed class CategoriaServicioLookupDto
{
    public int CategoriaServicioId { get; set; }
    public string Descripcion { get; set; } = string.Empty;
}
