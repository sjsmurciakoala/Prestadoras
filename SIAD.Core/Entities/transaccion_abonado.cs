using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class transaccion_abonado
{
    public int ide { get; set; }

    public long company_id { get; set; }

    public string? cliente_clave { get; set; }

    public decimal? recibo { get; set; }

    public string? tipotransaccion { get; set; }

    public decimal? docufuente { get; set; }

    public string? docufuente2 { get; set; }

    public DateOnly? fecha_docu { get; set; }

    public string? tipo_partida { get; set; }

    public string? banco { get; set; }

    public string? descripcion { get; set; }

    public decimal? plazo { get; set; }

    public decimal? docuaplicar { get; set; }

    public string? trans_aplicar { get; set; }

    public decimal? debitos { get; set; }

    public decimal? creditos { get; set; }

    public decimal? saldo { get; set; }

    public string? tipo_servicio { get; set; }

    public string? aplicar_alca { get; set; }

    public string? periodo { get; set; }

    public string? tasa { get; set; }

    public string? estado { get; set; }

    public DateOnly? fecha_registro { get; set; }

    public string? ciclo { get; set; }

    public string? ruta { get; set; }

    public string? secuencia { get; set; }

    public string? tiene_med { get; set; }

    public string? codigoplan { get; set; }

    public string? motivo { get; set; }

    public string? usuario { get; set; }

    public decimal? saldo_detalle { get; set; }
}
