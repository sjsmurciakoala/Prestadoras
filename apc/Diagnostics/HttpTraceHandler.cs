using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace apc.Diagnostics
{
    public class HttpTraceHandler : DelegatingHandler
    {
        private static readonly object FileLock = new();
        private static readonly string LogDir =
            Environment.GetEnvironmentVariable("SIAD_LOG_DIR")
            ?? Path.Combine("C:\\SIADLogs");
        private static readonly string LogFile = Path.Combine(LogDir, "http-trace.log");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            TryEnsureDir();
            var start = DateTimeOffset.Now;
            Log($"--> {request.Method} {request.RequestUri}");
            if (request.Headers.TryGetValues("Cookie", out var cookies))
            {
                var cookieStr = string.Join("; ", cookies);
                if (cookieStr.Length > 200) cookieStr = cookieStr.Substring(0, 200) + "...";
                Log($"    Cookie: {cookieStr}");
            }

            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                var elapsed = DateTimeOffset.Now - start;
                var media = response.Content?.Headers?.ContentType?.MediaType ?? "<none>";
                Log($"<-- {(int)response.StatusCode} {response.ReasonPhrase} ({elapsed.TotalMilliseconds:F0} ms) Content-Type={media}");
                return response;
            }
            catch (Exception ex)
            {
                var elapsed = DateTimeOffset.Now - start;
                Log($"<!! ERROR after {elapsed.TotalMilliseconds:F0} ms: {ex.Message}");
                throw;
            }
        }

        private static void TryEnsureDir()
        {
            try
            {
                if (!Directory.Exists(LogDir))
                {
                    Directory.CreateDirectory(LogDir);
                }
            }
            catch
            {
                // No bloquear la app por problemas de logging
            }
        }

        private static void Log(string line)
        {
            try
            {
                lock (FileLock)
                {
                    File.AppendAllText(LogFile, $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff}] {line}{Environment.NewLine}");
                }
            }
            catch
            {
                // Ignorar errores de escritura de log
            }
        }
    }
}
