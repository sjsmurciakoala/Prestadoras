using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class historicomedicion
{
    public int ide { get; set; }

    public long company_id { get; set; }

    public decimal ano { get; set; }

    public decimal mes { get; set; }

    public string? contador { get; set; }

    public string? ciclo { get; set; }

    public string? ruta { get; set; }

    public string? secuencia { get; set; }

    public string? clave { get; set; }

    public DateOnly? fecha { get; set; }

    public string? usuario { get; set; }

    public decimal? lect_act { get; set; }

    public decimal? lect_ant { get; set; }

    public DateOnly? fecha_lect_act { get; set; }

    public DateOnly? fecha_lect_ant { get; set; }

    public TimeOnly? hora { get; set; }

    public decimal? consumo { get; set; }

    public decimal? consumoant { get; set; }

    public string? tservi1 { get; set; }

    public decimal? taservi1 { get; set; }

    public string? tservi2 { get; set; }

    public decimal? taservi2 { get; set; }

    public string? tservi3 { get; set; }

    public decimal? taservi3 { get; set; }

    public string? tservi4 { get; set; }

    public decimal? taservi4 { get; set; }

    public char? cerrado { get; set; }

    public char? ser1 { get; set; }

    public char? ser2 { get; set; }

    public char? ser3 { get; set; }

    public char? ser4 { get; set; }

    public string? comentario { get; set; }

    public string? propietario { get; set; }

    public string? ubicacion { get; set; }

    public string? otros { get; set; }

    public char? categoria { get; set; }

    public string? observacion { get; set; }

    public string? mes2 { get; set; }

    public string? condicion { get; set; }

    public decimal? lec_prom { get; set; }

    public char? tipolectura { get; set; }

    public string? codinfo { get; set; }

    public char? ser5 { get; set; }

    public char? ser6 { get; set; }

    public char? ser7 { get; set; }

    public char? ser8 { get; set; }

    public char? ser9 { get; set; }

    public char? ser10 { get; set; }

    public string? tservi5 { get; set; }

    public string? tservi6 { get; set; }

    public string? tservi7 { get; set; }

    public string? tservi8 { get; set; }

    public string? tservi9 { get; set; }

    public string? tservi10 { get; set; }

    public decimal? taservi5 { get; set; }

    public decimal? taservi6 { get; set; }

    public decimal? taservi7 { get; set; }

    public decimal? taservi8 { get; set; }

    public decimal? taservi9 { get; set; }

    public decimal? taservi10 { get; set; }

    public decimal? revision { get; set; }

    public char? orden { get; set; }

    public char? revision2 { get; set; }

    public decimal? sel { get; set; }

    public decimal? numeroord { get; set; }

    public string? ajuste { get; set; }

    public string? numerofactura { get; set; }

    public long? correlativocai { get; set; }

    public long? idcai { get; set; }

    public byte[]? imagenmedidor { get; set; }

    public decimal? descuentoapp { get; set; }

    public char? categoriacliente { get; set; }
}
