using System;
using System.Globalization;
using System.Linq;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Utilities;
using apc.Client.Services.Bancos;
using apc.Client.Services.Tenant;

namespace apc.Client.Pages.Contabilidad;

public partial class TransaccionBancariaModal : IDisposable
{
    [Inject]
    private BanTransaccionesClient TransaccionesClient { get; set; } = null!;

    [Inject]
    private BanTiposTransaccionesClient TiposTransaccionesClient { get; set; } = null!;

    [Inject]
    private TenantState TenantState { get; set; } = null!;

    [Inject]
    private NavigationManager NavigationManager { get; set; } = null!;

    [Inject]
    private HttpClient Http { get; set; } = null!;

    [Parameter]
    public List<BancoCuentaListDto>? CuentasBancarias { get; set; }

    [Parameter]
    public long? BancoId { get; set; }

    [Parameter]
    public long? BancoCuentaId { get; set; }

    [Parameter]
    public DateOnly? DefaultFechaMovimiento { get; set; }

    [Parameter]
    public string? DefaultReferencia { get; set; }

    [Parameter]
    public decimal? DefaultMonto { get; set; }

    [Parameter]
    public string? DefaultDescripcion { get; set; }

    [Parameter]
    public bool ReadOnly { get; set; }

    [Parameter]
    public bool DisableInternalScroll { get; set; }

    [Parameter]
    public BanTransaccionDetalleDto? TransaccionDetalle { get; set; }

    [Parameter]
    public EventCallback<bool> OnClose { get; set; }

    [Parameter]
    public EventCallback OnTransaccionGuardada { get; set; }

    protected BanTransaccionCreateDto NuevaTransaccion { get; set; } = new();
    protected DateTime fecha = DateTime.Now;
    protected bool Guardando { get; set; }
    protected string? MensajeError { get; set; }
    protected bool MensajeEsAdvertencia { get; set; }
    private bool isDisposed;
    private bool detalleInicializado;
    private long? detalleKardexId;
    private string? defaultsSignatureApplied;
    private CancellationTokenSource? loadCts;

    private List<BancoCuentaListDto> CuentasFiltradas { get; set; } = new();
    private List<BanTipoTransaccionListDto> TiposTransaccion { get; set; } = new();
    private List<PlanCuentaDto> PlanCuentas { get; set; } = new();
    private List<PlanCuentaDto> CuentasContra { get; set; } = new();
    private List<CuentaContableLookupDto> CuentasContraLookup { get; set; } = new();
    private Dictionary<long, PlanCuentaDto> PlanCuentasLookup { get; set; } = new();
    private string? MensajeTiposError { get; set; }
    private string? MensajeCuentasError { get; set; }

    private static readonly string[] BancoCajaKeywords = new[]
    {
        "BANCO",
        "BANCOS",
        "CAJA",
        "EFECTIVO",
        "CHEQUE",
        "CHEQUES"
    };

    private IReadOnlyList<BanTransaccionContraLineaDto> ContraLineasValidas
        => NuevaTransaccion.ContraCuentas?
            .Where(l => l.CuentaId > 0 && l.Monto > 0)
            .ToList()
           ?? new List<BanTransaccionContraLineaDto>();

    private decimal TotalContra => ContraLineasValidas.Sum(l => l.Monto);
    private decimal DiferenciaMonto => NuevaTransaccion.Monto - TotalContra;
    private string DiferenciaAlertClass => DiferenciaMonto == 0m ? "alert-success" : "alert-warning";
    private string MensajeAlertClass => MensajeEsAdvertencia ? "alert alert-warning mt-3" : "alert alert-danger mt-3";
    private static readonly CultureInfo MontoCulture = new("en-US");

    private bool EsEntrada
    {
        get
        {
            var tipo = TiposTransaccion.FirstOrDefault(t =>
                string.Equals(t.TipoTransaccion, NuevaTransaccion.IdTipoTransaccion, StringComparison.OrdinalIgnoreCase));
            return tipo is null || string.Equals(tipo.EntraSale, "E", StringComparison.OrdinalIgnoreCase);
        }
    }

    private string DebitoHeader => "Débito";
    private string CreditoHeader => "Crédito";
    private string? ReporteUrl
    {
        get
        {
            if (!ReadOnly || TransaccionDetalle is null || TransaccionDetalle.BanKardexId <= 0)
            {
                return null;
            }

            var reporteUri = NavigationManager.ToAbsoluteUri(
                $"api/bancos/transacciones/{TransaccionDetalle.BanKardexId}/reporte");

            return reporteUri.ToString();
        }
    }

    private string ModalBodyClass
        => DisableInternalScroll
            ? "transaccion-modal-body p-4 no-internal-scroll"
            : "transaccion-modal-body p-4";

    protected long BancoCuentaSeleccionadaId
    {
        get => NuevaTransaccion.BancoCuentaId;
        set
        {
            if (NuevaTransaccion.BancoCuentaId == value)
            {
                return;
            }

            NuevaTransaccion.BancoCuentaId = value;
            AjustarTasaCambio();
        }
    }

    protected string DescripcionTransaccion
    {
        get => NuevaTransaccion.Descripcion;
        set
        {
            var descripcionAnterior = NormalizeRequiredText(NuevaTransaccion.Descripcion);
            var descripcionNueva = NormalizeRequiredText(value);

            NuevaTransaccion.Descripcion = descripcionNueva;

            if (string.Equals(descripcionAnterior, descripcionNueva, StringComparison.Ordinal))
            {
                return;
            }

            SincronizarDescripcionDetalle(descripcionAnterior, descripcionNueva);
        }
    }

    private BancoCuentaListDto? CuentaBancariaSeleccionada
        => CuentasFiltradas.FirstOrDefault(c => c.BancoCuentaId == NuevaTransaccion.BancoCuentaId);

    private bool MostrarTasaCambio
        => string.Equals(
            CuentaBancariaSeleccionada?.Moneda?.Trim(),
            "USD",
            StringComparison.OrdinalIgnoreCase);

    protected override Task OnInitializedAsync()
    {
        if (!ReadOnly && NuevaTransaccion.ContraCuentas.Count == 0)
        {
            NuevaTransaccion.ContraCuentas.Add(CrearContraLinea());
        }

        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = new CancellationTokenSource();
        _ = CargarDatosAsync(loadCts.Token);

        return Task.CompletedTask;
    }

    protected override void OnParametersSet()
    {
        // Filtrar las cuentas por banco si se proporciona BancoId
        if (BancoId.HasValue && CuentasBancarias?.Count > 0)
        {
            CuentasFiltradas = CuentasBancarias
                .Where(c => c.BancoId == BancoId.Value)
                .ToList();
        }
        else
        {
            CuentasFiltradas = CuentasBancarias ?? new List<BancoCuentaListDto>();
        }

        var seleccionActual = NuevaTransaccion.BancoCuentaId;
        var seleccionValida = seleccionActual > 0
                              && CuentasFiltradas.Any(c => c.BancoCuentaId == seleccionActual);

        if (!seleccionValida)
        {
            if (BancoCuentaId.HasValue
                && BancoCuentaId.Value > 0
                && CuentasFiltradas.Any(c => c.BancoCuentaId == BancoCuentaId.Value))
            {
                NuevaTransaccion.BancoCuentaId = BancoCuentaId.Value;
            }
            else if (CuentasFiltradas.Count == 1)
            {
                NuevaTransaccion.BancoCuentaId = CuentasFiltradas[0].BancoCuentaId;
            }
        }

        if (ReadOnly && TransaccionDetalle is not null)
        {
            if (detalleKardexId != TransaccionDetalle.BanKardexId)
            {
                detalleInicializado = false;
                detalleKardexId = TransaccionDetalle.BanKardexId;
            }

            CargarDetalleTransaccion(TransaccionDetalle);
        }
        else if (!ReadOnly)
        {
            var currentDefaultsSignature = BuildDefaultsSignature();
            if (string.Equals(defaultsSignatureApplied, currentDefaultsSignature, StringComparison.Ordinal))
            {
                AjustarTasaCambio();
                return;
            }

            if (DefaultFechaMovimiento.HasValue)
            {
                fecha = DefaultFechaMovimiento.Value.ToDateTime(TimeOnly.MinValue);
            }

            if (!string.IsNullOrWhiteSpace(DefaultReferencia))
            {
                NuevaTransaccion.Referencia = DefaultReferencia.Trim();
            }

            if (DefaultMonto.HasValue && DefaultMonto.Value > 0m)
            {
                NuevaTransaccion.Monto = DefaultMonto.Value;
            }

            if (!string.IsNullOrWhiteSpace(DefaultDescripcion))
            {
                DescripcionTransaccion = DefaultDescripcion;
            }

            defaultsSignatureApplied = currentDefaultsSignature;
        }

        AjustarTasaCambio();
    }

    private string BuildDefaultsSignature()
    {
        var fechaValue = DefaultFechaMovimiento?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;
        var referenciaValue = DefaultReferencia?.Trim() ?? string.Empty;
        var montoValue = DefaultMonto?.ToString("0.####", CultureInfo.InvariantCulture) ?? string.Empty;
        var descripcionValue = DefaultDescripcion?.Trim() ?? string.Empty;
        return $"{fechaValue}|{referenciaValue}|{montoValue}|{descripcionValue}";
    }

    private async Task CargarDatosAsync(CancellationToken ct)
    {
        try
        {
            await Task.WhenAll(
                LoadTiposTransaccionAsync(ct),
                LoadPlanCuentasAsync(ct));
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadTiposTransaccionAsync(CancellationToken ct)
    {
        try
        {
            MensajeTiposError = null;
            var companyId = await TenantState.EnsureCompanyAsync(ct);
            if (ct.IsCancellationRequested || isDisposed)
            {
                return;
            }

            TiposTransaccion = (await TiposTransaccionesClient.GetAsync(companyId, ct)).ToList();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            TiposTransaccion = new List<BanTipoTransaccionListDto>();
            MensajeTiposError = $"No se pudieron cargar los tipos de transaccion: {ex.Message}";
        }

        await SafeStateHasChangedAsync(ct);
    }

    private async Task LoadPlanCuentasAsync(CancellationToken ct)
    {
        try
        {
            MensajeCuentasError = null;
            var cuentas = await Http.GetFromJsonAsync<List<PlanCuentaDto>>(
                "api/contabilidad/catalogos/plan-cuentas",
                ct);
            PlanCuentas = (cuentas ?? new List<PlanCuentaDto>())
                .Where(c => c.AllowsPosting && (string.IsNullOrWhiteSpace(c.Status)
                                                || string.Equals(c.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
                                                || string.Equals(c.Status, "ACTIVO", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            PlanCuentasLookup = PlanCuentas.ToDictionary(c => c.AccountId);
            CuentasContra = PlanCuentas
                .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
            CuentasContraLookup = CuentasContra
                .Select(c => new CuentaContableLookupDto
                {
                    AccountId = c.AccountId,
                    Code = c.Code ?? string.Empty,
                    Description = c.Name ?? string.Empty
                })
                .ToList();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            PlanCuentas = new List<PlanCuentaDto>();
            PlanCuentasLookup = new Dictionary<long, PlanCuentaDto>();
            CuentasContra = new List<PlanCuentaDto>();
            CuentasContraLookup = new List<CuentaContableLookupDto>();
            MensajeCuentasError = $"No se pudieron cargar las cuentas contables: {ex.Message}";
        }

        await SafeStateHasChangedAsync(ct);
    }

    private void AgregarContraLinea()
    {
        if (ReadOnly)
        {
            return;
        }

        NuevaTransaccion.ContraCuentas.Add(CrearContraLinea());
    }

    private void QuitarContraLinea(BanTransaccionContraLineaDto linea)
    {
        if (ReadOnly)
        {
            return;
        }

        if (linea is null)
        {
            return;
        }

        if (!NuevaTransaccion.ContraCuentas.Remove(linea))
        {
            return;
        }
        if (NuevaTransaccion.ContraCuentas.Count == 0)
        {
            NuevaTransaccion.ContraCuentas.Add(CrearContraLinea());
        }
    }


    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        loadCts?.Cancel();
        loadCts?.Dispose();
        loadCts = null;

    }

    private async Task SafeStateHasChangedAsync(CancellationToken ct)
    {
        if (isDisposed || ct.IsCancellationRequested)
        {
            return;
        }

        try
        {
            await InvokeAsync(StateHasChanged);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private bool EsCuentaBancoCaja(PlanCuentaDto cuenta)
    {
        if (CuentaContieneKeyword(cuenta))
        {
            return true;
        }

        var current = cuenta;
        var visited = new HashSet<long>();
        while (current.ParentAccountId.HasValue && visited.Add(current.ParentAccountId.Value))
        {
            if (!PlanCuentasLookup.TryGetValue(current.ParentAccountId.Value, out var parent))
            {
                break;
            }

            if (CuentaContieneKeyword(parent))
            {
                return true;
            }

            current = parent;
        }

        return false;
    }

    private static bool CuentaContieneKeyword(PlanCuentaDto cuenta)
    {
        return TextoContieneKeyword(cuenta.Name)
               || TextoContieneKeyword(cuenta.Code)
               || TextoContieneKeyword(cuenta.Category);
    }

    private static bool TextoContieneKeyword(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var keyword in BancoCajaKeywords)
        {
            if (value.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private string GetCuentaDisplay(long accountId)
    {
        if (PlanCuentasLookup.TryGetValue(accountId, out var cuenta))
        {
            var code = cuenta.Code?.Trim() ?? string.Empty;
            var name = cuenta.Name?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
            {
                return $"{AccountCodeFormatter.Format(code)} ({name})";
            }

            if (!string.IsNullOrWhiteSpace(code))
            {
                return AccountCodeFormatter.Format(code);
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return accountId > 0 ? accountId.ToString() : "Cuenta contable";
    }

    private string GetLineaDescripcion(BanTransaccionContraLineaDto linea)
    {
        if (!string.IsNullOrWhiteSpace(linea.Descripcion))
        {
            return linea.Descripcion.Trim();
        }

        if (!string.IsNullOrWhiteSpace(NuevaTransaccion.Descripcion))
        {
            return NuevaTransaccion.Descripcion.Trim();
        }

        var cuenta = GetCuentaDisplay(linea.CuentaId);
        return string.IsNullOrWhiteSpace(cuenta) ? "Contra-cuenta" : $"Contra-cuenta: {cuenta}";
    }

    private string GetBancoCuentaDisplay()
    {
        var cuenta = CuentaBancariaSeleccionada;
        if (cuenta is null)
        {
            return "Cuenta bancaria";
        }

        var code = cuenta.CuentaContableCodigo?.Trim();
        var name = cuenta.CuentaContableNombre?.Trim();
        if (!string.IsNullOrWhiteSpace(code) && !string.IsNullOrWhiteSpace(name))
        {
            return $"{AccountCodeFormatter.Format(code)} ({name})";
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            return AccountCodeFormatter.Format(code);
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        return string.IsNullOrWhiteSpace(cuenta.NumeroCuenta)
            ? "Cuenta bancaria"
            : cuenta.NumeroCuenta;
    }

    private string FormatearMonto(decimal monto)
    {
        return $"L. {monto.ToString("N2", MontoCulture)}";
    }

    protected async Task GuardarAsync()
    {
        if (ReadOnly)
        {
            return;
        }

        try
        {
            LimpiarMensaje();
            Guardando = true;

            NuevaTransaccion.FechaMovimiento = DateOnly.FromDateTime(fecha);
            var today = DateOnly.FromDateTime(DateTime.Today);
            if (NuevaTransaccion.FechaMovimiento > today)
            {
                MostrarAdvertencia("No se permiten transacciones futuras.");
                return;
            }

            AjustarTasaCambio();
            var descripcionTransaccion = NormalizeOptionalText(NuevaTransaccion.Descripcion);
            NuevaTransaccion.ContraCuentas = NuevaTransaccion.ContraCuentas
                .Where(l => l.CuentaId > 0 && l.Monto > 0)
                .Select(l => new BanTransaccionContraLineaDto
                {
                    CuentaId = l.CuentaId,
                    Monto = l.Monto,
                    Descripcion = NormalizeOptionalText(l.Descripcion) ?? descripcionTransaccion,
                    SourceDocument = string.IsNullOrWhiteSpace(l.SourceDocument) ? null : l.SourceDocument.Trim()
                })
                .ToList();

            if (NuevaTransaccion.ContraCuentas.Count == 0)
            {
                MostrarAdvertencia("Agregue al menos una contracuenta válida.");
                return;
            }

            // Llamar al servicio
            var resultado = await TransaccionesClient.RegistrarMovimientoAsync(NuevaTransaccion);

            // Notificar al componente padre
            await OnTransaccionGuardada.InvokeAsync();
            await CancelarAsync();
        }
        catch (HttpRequestException ex)
        {
            MostrarAdvertencia(ex.Message);
        }
        catch (Exception ex)
        {
            MostrarError(ex.Message);
        }
        finally
        {
            Guardando = false;
        }
    }

    private void LimpiarMensaje()
    {
        MensajeError = null;
        MensajeEsAdvertencia = false;
    }

    private void MostrarAdvertencia(string mensaje)
    {
        MensajeError = $"Advertencia: {mensaje}";
        MensajeEsAdvertencia = true;
    }

    private void MostrarError(string mensaje)
    {
        MensajeError = $"Error: {mensaje}";
        MensajeEsAdvertencia = false;
    }

    protected async Task CancelarAsync()
    {
        await OnClose.InvokeAsync(false);
    }

    private void CargarDetalleTransaccion(BanTransaccionDetalleDto detalle)
    {
        if (detalleInicializado)
        {
            return;
        }

        var contraLineas = detalle.ContraCuentas?
            .Where(l => l is not null && l.CuentaId > 0 && l.Monto > 0)
            .Select(l => new BanTransaccionContraLineaDto
            {
                CuentaId = l.CuentaId,
                Monto = l.Monto,
                Descripcion = string.IsNullOrWhiteSpace(l.Descripcion) ? null : l.Descripcion.Trim(),
                SourceDocument = string.IsNullOrWhiteSpace(l.SourceDocument) ? null : l.SourceDocument.Trim()
            })
            .ToList()
            ?? new List<BanTransaccionContraLineaDto>();

        NuevaTransaccion = new BanTransaccionCreateDto
        {
            BancoCuentaId = detalle.BancoCuentaId,
            IdTipoTransaccion = detalle.IdTipoTransaccion ?? string.Empty,
            FechaMovimiento = detalle.FechaMovimiento,
            Descripcion = detalle.Descripcion ?? string.Empty,
            Referencia = detalle.Referencia,
            Monto = Math.Abs(detalle.Monto),
            TasaCambio = detalle.TasaCambio <= 0m ? 1m : detalle.TasaCambio,
            ContraCuentas = contraLineas
        };

        fecha = detalle.FechaMovimiento.ToDateTime(TimeOnly.MinValue);

        detalleInicializado = true;
    }

    private void AjustarTasaCambio()
    {
        if (!MostrarTasaCambio)
        {
            NuevaTransaccion.TasaCambio = 1m;
            return;
        }

        if (NuevaTransaccion.TasaCambio <= 0m)
        {
            NuevaTransaccion.TasaCambio = 1m;
        }
    }

    private BanTransaccionContraLineaDto CrearContraLinea()
    {
        return new BanTransaccionContraLineaDto
        {
            Descripcion = NormalizeRequiredText(NuevaTransaccion.Descripcion)
        };
    }

    private void SincronizarDescripcionDetalle(string descripcionAnterior, string descripcionNueva)
    {
        if (NuevaTransaccion.ContraCuentas.Count == 0)
        {
            return;
        }

        var descripcionAnteriorNormalizada = NormalizeOptionalText(descripcionAnterior);

        foreach (var linea in NuevaTransaccion.ContraCuentas)
        {
            var descripcionLinea = NormalizeOptionalText(linea.Descripcion);
            if (descripcionLinea is not null
                && (descripcionAnteriorNormalizada is null
                    || !string.Equals(descripcionLinea, descripcionAnteriorNormalizada, StringComparison.Ordinal)))
            {
                continue;
            }

            linea.Descripcion = descripcionNueva;
        }
    }

    private static string NormalizeRequiredText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
