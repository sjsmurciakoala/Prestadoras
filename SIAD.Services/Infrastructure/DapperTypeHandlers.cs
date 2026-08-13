using System.Data;
using Dapper;

namespace SIAD.Services.Infrastructure;

/// <summary>
/// Conversores de tipo para Dapper que el paquete no trae de fábrica.
/// <para>
/// Dapper 2.1.x no sabe pasar <see cref="DateOnly"/> como parámetro
/// (<c>NotSupportedException: The member ... of type System.DateOnly cannot be used as a
/// parameter value</c>), aunque Npgsql sí mapea <c>date</c> ↔ <see cref="DateOnly"/> al leer.
/// Registrar el handler una sola vez al arrancar deja ambos sentidos funcionando, que es lo
/// que esperan las entidades y DTOs del proyecto (usan <see cref="DateOnly"/> para columnas
/// <c>date</c>).
/// </para>
/// </summary>
public static class DapperTypeHandlers
{
    private static bool _registrados;
    private static readonly object _candado = new();

    /// <summary>
    /// Registra los handlers. Idempotente: la configuración de Dapper es estática y global,
    /// así que registrar dos veces tiraría <see cref="ArgumentException"/> por clave duplicada.
    /// </summary>
    public static void EnsureRegistered()
    {
        if (_registrados)
        {
            return;
        }

        lock (_candado)
        {
            if (_registrados)
            {
                return;
            }

            // Cubre DateOnly y DateOnly? (Dapper registra el Nullable<T> junto al T).
            SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
            _registrados = true;
        }
    }

    private sealed class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
    {
        public override void SetValue(IDbDataParameter parameter, DateOnly value)
        {
            parameter.DbType = DbType.Date;
            parameter.Value = value;
        }

        public override DateOnly Parse(object value) => value switch
        {
            DateOnly fecha => fecha,
            DateTime fechaHora => DateOnly.FromDateTime(fechaHora),
            string texto => DateOnly.Parse(texto),
            _ => DateOnly.FromDateTime(Convert.ToDateTime(value))
        };
    }
}
