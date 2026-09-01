using System;
using System.Threading;

namespace SIAD.Core.Tenancy;

/// <summary>
/// Fija la empresa (tenant) para el flujo async actual, para procesos <b>sin sesión HTTP</b> — p. ej.
/// el barrido diario de alertas de stock, que recorre empresa por empresa. Es un
/// <see cref="AsyncLocal{T}"/>: no se filtra entre flujos concurrentes ni contamina un request.
/// <para>
/// <see cref="SIAD.Core.Tenancy.ICurrentCompanyService"/> (la implementación del portal) lo respeta con
/// <b>prioridad sobre el claim</b>. Uso exclusivo de batch/background; en un request normal nadie lo fija,
/// así que no tiene efecto.
/// </para>
/// </summary>
public static class TenantOverride
{
    private static readonly AsyncLocal<long?> _company = new();

    /// <summary>Empresa fijada para el flujo actual, o null si no hay override.</summary>
    public static long? CompanyId => _company.Value;

    /// <summary>Fija la empresa hasta que se libere el <see cref="IDisposable"/> devuelto (anidable).</summary>
    public static IDisposable Begin(long companyId)
    {
        var previo = _company.Value;
        _company.Value = companyId;
        return new Ambito(previo);
    }

    private sealed class Ambito : IDisposable
    {
        private readonly long? _previo;
        private bool _liberado;

        public Ambito(long? previo) => _previo = previo;

        public void Dispose()
        {
            if (_liberado) return;
            _liberado = true;
            _company.Value = _previo;
        }
    }
}
