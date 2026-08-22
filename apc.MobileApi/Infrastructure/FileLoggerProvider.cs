using System.Collections.Concurrent;
using System.Text;

namespace apc.MobileApi.Infrastructure;

/// <summary>
/// Opciones del log a archivo. Se leen de la sección <c>MobileApi:Log</c>.
/// </summary>
public sealed class FileLoggerOptions
{
    /// <summary>Carpeta destino, relativa al content root si no es absoluta.</summary>
    public string Directorio { get; set; } = "logs";

    /// <summary>Nivel mínimo que se persiste.</summary>
    public LogLevel NivelMinimo { get; set; } = LogLevel.Warning;

    /// <summary>Días de retención; los archivos más viejos se borran al arrancar.</summary>
    public int RetencionDias { get; set; } = 30;
}

/// <summary>
/// Logger a archivo con rotación diaria, sin dependencias de terceros.
/// </summary>
/// <remarks>
/// La API corría con el logger por defecto (consola) y hospedada en IIS con
/// <c>stdoutLogEnabled="false"</c>: todo <c>LogError</c> se descartaba. Un error de
/// campo —por ejemplo el CORRELATIVO_DUPLICADO que devolvía 500 al subir una lectura—
/// no dejaba rastro en ninguna parte, y diagnosticarlo exigía prender el log de IIS,
/// reciclar el sitio y reproducir el problema. Con esto, el rastro queda siempre.
///
/// La solución no usa Serilog ni NLog; se implementa a mano para no sumarle una
/// dependencia nueva a un host de producción por una necesidad tan acotada.
/// </remarks>
public sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLoggerOptions _options;
    private readonly string _directorio;
    private readonly ConcurrentDictionary<string, FileLogger> _loggers = new();
    private readonly object _candado = new();

    public FileLoggerProvider(FileLoggerOptions options, string contentRoot)
    {
        _options = options;
        _directorio = Path.IsPathRooted(options.Directorio)
            ? options.Directorio
            : Path.Combine(contentRoot, options.Directorio);

        Directory.CreateDirectory(_directorio);
        PurgarAntiguos();
    }

    public ILogger CreateLogger(string categoryName) =>
        _loggers.GetOrAdd(categoryName, nombre => new FileLogger(this, nombre));

    public void Dispose() => _loggers.Clear();

    private bool Habilitado(LogLevel level) => level >= _options.NivelMinimo && level != LogLevel.None;

    /// <summary>
    /// Escribe una línea. Nunca propaga: un fallo de disco no puede tumbar un request.
    /// </summary>
    private void Escribir(string texto)
    {
        try
        {
            var archivo = Path.Combine(_directorio, $"mobileapi-{DateTime.Now:yyyyMMdd}.log");
            lock (_candado)
            {
                File.AppendAllText(archivo, texto, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void PurgarAntiguos()
    {
        if (_options.RetencionDias <= 0)
        {
            return;
        }

        try
        {
            var corte = DateTime.Now.AddDays(-_options.RetencionDias);
            foreach (var archivo in Directory.EnumerateFiles(_directorio, "mobileapi-*.log"))
            {
                if (File.GetLastWriteTime(archivo) < corte)
                {
                    File.Delete(archivo);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class FileLogger(FileLoggerProvider proveedor, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => proveedor.Habilitado(logLevel);

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var sb = new StringBuilder()
                .Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [").Append(Nivel(logLevel)).Append("] ")
                .Append(categoria)
                .Append(": ")
                .AppendLine(formatter(state, exception));

            if (exception is not null)
            {
                sb.AppendLine(exception.ToString());
            }

            proveedor.Escribir(sb.ToString());
        }

        private static string Nivel(LogLevel level) => level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "___",
        };
    }
}
