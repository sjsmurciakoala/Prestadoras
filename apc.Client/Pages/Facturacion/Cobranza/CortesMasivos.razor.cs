using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Catalogos;
using SIAD.Core.DTOs.Rutas;
using apc.Client.Services.Cobranza;
using apc.Client.Services.Catalogos;
using apc.Client.Services.Informes;

namespace apc.Client.Pages.Facturacion.Cobranza;

public class CortesMasivosBase : ComponentBase
{
    [Inject] protected CorteMasivoClient CorteMasivoClient { get; set; } = default!;
    [Inject] protected CatalogosClient CatalogosClient { get; set; } = default!;
    [Inject] protected InformesClient InformesClient { get; set; } = default!;
    [Inject] protected IJSRuntime JS { get; set; } = default!;
    [Inject] protected NavigationManager Navigation { get; set; } = default!;

    protected static readonly IReadOnlyList<MesLookup> meses =
    [
        new(1, "Enero"),    new(2, "Febrero"),   new(3, "Marzo"),
        new(4, "Abril"),    new(5, "Mayo"),       new(6, "Junio"),
        new(7, "Julio"),    new(8, "Agosto"),     new(9, "Septiembre"),
        new(10, "Octubre"), new(11, "Noviembre"), new(12, "Diciembre")
    ];

    // Catálogos
    protected List<CicloLookupDto> ciclos = [];
    protected List<BarrioLookupDto> barrios = [];

    // Estado formulario
    protected int periodoAnio = DateTime.Now.Year;
    protected int periodoMes  = DateTime.Now.Month;
    protected int? cicloSeleccionado;
    protected string? barrioSeleccionado;
    protected decimal? valorMinimo;
    protected int? diasCorte;

    // Estado generación
    protected bool isGenerando = false;
    protected string? generarError;
    protected CorteMasivoHdrDto? ultimoLoteGenerado;

    // Historial
    protected List<CorteMasivoHdrDto> lotes = [];
    protected bool isLoadingHistorial = true;

    // Detalle popup
    protected bool mostrarDetalle = false;
    protected bool isLoadingDetalle = false;
    protected CorteMasivoDetalleDto? detalleActual;
    protected string tituloPopupDetalle = "Detalle del lote";

    protected override async Task OnInitializedAsync()
    {
        await Task.WhenAll(
            CargarCatalogos(),
            CargarHistorial()
        );
    }

    private async Task CargarCatalogos()
    {
        try
        {
            var resultCiclos = await InformesClient.GetCiclosAsync();
            ciclos = (resultCiclos ?? []).ToList();
        }
        catch
        {
            // catálogos opcionales
        }

        try
        {
            barrios = await CatalogosClient.ObtenerBarriosAsync();
        }
        catch
        {
            barrios = [];
        }
    }

    protected async Task CargarHistorial()
    {
        isLoadingHistorial = true;
        try
        {
            var result = await CorteMasivoClient.ListarAsync();
            lotes = result?.ToList() ?? [];
        }
        catch { lotes = []; }
        finally { isLoadingHistorial = false; }
    }

    protected async Task GenerarLote()
    {
        if (periodoAnio < 2020 || periodoAnio > 2099)
        {
            generarError = "El año del período debe estar entre 2020 y 2099.";
            return;
        }
        if (periodoMes < 1 || periodoMes > 12)
        {
            generarError = "Seleccione un mes de período válido.";
            return;
        }
        if (diasCorte is < 0)
        {
            generarError = "Los días de corte no pueden ser negativos.";
            return;
        }

        generarError = null;
        ultimoLoteGenerado = null;
        isGenerando = true;
        try
        {
            var req = new GenerarCorteMasivoRequest(
                PeriodoAnio:  periodoAnio,
                PeriodoMes:   periodoMes,
                CicloId:      cicloSeleccionado,
                BarrioCodigo: barrioSeleccionado,
                CategoriaId:  null,
                ValorMinimo:  valorMinimo ?? 0m,
                DiasCorte:    diasCorte ?? 0);

            ultimoLoteGenerado = await CorteMasivoClient.GenerarAsync(req);
            await CargarHistorial();
        }
        catch (Exception ex)
        {
            generarError = "No se pudieron generar los cortes.";
        }
        finally
        {
            isGenerando = false;
        }
    }

    protected async Task AbrirDetalle(CorteMasivoHdrDto lote, bool soloSinPago)
    {
        tituloPopupDetalle = soloSinPago
            ? $"Lote {lote.Correlativo} — Pendientes sin pago"
            : $"Lote {lote.Correlativo} — Detalle completo";
        detalleActual = null;
        mostrarDetalle = true;
        isLoadingDetalle = true;
        try
        {
            detalleActual = soloSinPago
                ? await CorteMasivoClient.ObtenerParaReimpresionAsync(lote.Id)
                : await CorteMasivoClient.ObtenerDetalleAsync(lote.Id);
        }
        catch { detalleActual = null; }
        finally { isLoadingDetalle = false; }
    }

    protected async Task ImprimirLote(int loteId, bool soloSinPago = false)
    {
        var relativeUrl = CorteMasivoClient.GetImprimirUrl(loteId, soloSinPago);
        var absoluteUrl = Navigation.ToAbsoluteUri(relativeUrl).ToString();
        await JS.InvokeVoidAsync("open", absoluteUrl, "_blank");
    }

    protected async Task DescargarExcel(int loteId, bool comparativo)
    {
        var relativeUrl = comparativo
            ? CorteMasivoClient.GetComparativoExcelUrl(loteId)
            : CorteMasivoClient.GetExcelUrl(loteId, false);

        var absoluteUrl = Navigation.ToAbsoluteUri(relativeUrl).ToString();
        await JS.InvokeVoidAsync("open", absoluteUrl, "_blank");
    }

    protected record MesLookup(int Numero, string Nombre);
}
