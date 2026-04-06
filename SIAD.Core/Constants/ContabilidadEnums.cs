using System;

namespace SIAD.Core.Constants;

/// <summary>
/// Frecuencia de depreciación de activos.
/// </summary>
public enum FrequenciaDepreciacionType
{
    /// <summary>Diariamente</summary>
    Diario = 0,
    
    /// <summary>Mensualmente</summary>
    Mensual = 1,
    
    /// <summary>Anualmente</summary>
    Anual = 2
}

/// <summary>
/// Clase contable para líneas de balance.
/// </summary>
public enum ClaseBalanceType
{
    /// <summary>Activos circulantes</summary>
    ActivoCirculante = 0,
    
    /// <summary>Activos no circulantes</summary>
    ActivoNoCirculante = 1,
    
    /// <summary>Pasivos circulantes</summary>
    PasivoCirculante = 2,
    
    /// <summary>Pasivos no circulantes</summary>
    PasivoNoCirculante = 3,
    
    /// <summary>Capital o patrimonio</summary>
    Capital = 4
}

/// <summary>
/// Tipo de línea en el estado de resultados.
/// </summary>
public enum TipoLineaResultadoType
{
    /// <summary>Ingresos</summary>
    Ingreso = 0,
    
    /// <summary>Costos</summary>
    Costo = 1,
    
    /// <summary>Gastos</summary>
    Gasto = 2
}

/// <summary>
/// Estado del período contable.
/// </summary>
public enum EstadoPeriodoType
{
    /// <summary>Abierto para movimientos</summary>
    Abierto = 0,

    /// <summary>En precierre para validaciones y preparacion del siguiente periodo</summary>
    Precierre = 1,

    /// <summary>Cerrado fiscalmente</summary>
    Cerrado = 2
}

/// <summary>
/// Utilidades canonicas para estados de periodos contables.
/// </summary>
public static class EstadoPeriodoHelper
{
    public const short AbiertoId = (short)EstadoPeriodoType.Abierto;
    public const short PrecierreId = (short)EstadoPeriodoType.Precierre;
    public const short CerradoId = (short)EstadoPeriodoType.Cerrado;

    public static bool IsValid(short? statusId)
    {
        return statusId is AbiertoId or PrecierreId or CerradoId;
    }

    public static short Require(short? statusId, string context)
    {
        if (statusId is AbiertoId or PrecierreId or CerradoId)
        {
            return statusId.Value;
        }

        throw new InvalidOperationException(
            $"Estado de periodo invalido en {context}. Valores permitidos: 0=Abierto, 1=Precierre, 2=Cerrado.");
    }

    public static bool IsOpen(short? statusId)
    {
        return statusId == AbiertoId;
    }

    public static string ToText(short? statusId)
    {
        return statusId switch
        {
            AbiertoId => "ABIERTO",
            PrecierreId => "PRECIERRE",
            CerradoId => "CERRADO",
            _ => "SIN ESTADO"
        };
    }
}

/// <summary>
/// Tipo de correlativo para documentos contables.
/// </summary>
public enum TipoCorrelativoType
{
    /// <summary>Facturas de venta</summary>
    FacturaVenta = 0,
    
    /// <summary>Notas de crédito</summary>
    NotaCredito = 1,
    
    /// <summary>Notas de débito</summary>
    NotaDebito = 2,
    
    /// <summary>Recibos</summary>
    Recibo = 3,
    
    /// <summary>Pólizas diarias</summary>
    PolizaDiaria = 4,
    
    /// <summary>Pólizas de ingresos</summary>
    PolizaIngreso = 5,
    
    /// <summary>Pólizas de egresos</summary>
    PolizaEgreso = 6,
    
    /// <summary>Pólizas de bancos</summary>
    PolizaBanco = 7
}
