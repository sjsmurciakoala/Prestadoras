using Microsoft.EntityFrameworkCore;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Tests;

/// <summary>
/// La consulta del mapa de cuadrillas usa GroupBy + Max, que EF Core solo puede
/// traducir a SQL en tiempo de ejecución: si no la traduce, no falla al compilar,
/// falla cuando el usuario abre /mapa.
///
/// Estas pruebas generan el SQL con ToQueryString() sin abrir ninguna conexión,
/// así que no necesitan una base de datos disponible.
/// </summary>
public class CoordenadasMapaSqlTests
{
    private const string FakeConnection =
        "Host=localhost;Port=5432;Database=no_se_conecta;Username=x;Password=y";

    private static SiadDbContext CrearContexto()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(FakeConnection)
            .Options;

        return new SiadDbContext(options, new TestCurrentCompanyService(1));
    }

    [Fact]
    public void UltimaCoordenadaPorEmpleado_SeTraduceASql()
    {
        using var context = CrearContexto();

        var ultimosIds = context.coordenadas_empleados
            .AsNoTracking()
            .GroupBy(c => c.nombre)
            .Select(g => g.Max(c => c.id));

        var consulta = context.coordenadas_empleados
            .AsNoTracking()
            .Where(coord => ultimosIds.Contains(coord.id));

        // Si EF no pudiera traducirlo, ToQueryString lanzaría InvalidOperationException.
        var sql = consulta.ToQueryString();

        Assert.Contains("coordenadas_empleado", sql);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MAX(", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConsultaCompletaDelMapa_ConJoinAUsuarios_SeTraduceASql()
    {
        using var context = CrearContexto();

        var ultimosIds = context.coordenadas_empleados
            .AsNoTracking()
            .GroupBy(c => c.nombre)
            .Select(g => g.Max(c => c.id));

        var consulta =
            from coord in context.coordenadas_empleados.AsNoTracking()
            where ultimosIds.Contains(coord.id)
            join usuario in context.usuarios_miordens.AsNoTracking()
                on coord.nombre equals usuario.usuario into usuarios
            from usuario in usuarios.DefaultIfEmpty()
            select new
            {
                Nombre = usuario != null ? (usuario.nombre ?? string.Empty) : (coord.nombre ?? string.Empty),
                Usuario = usuario != null ? (usuario.usuario ?? (coord.nombre ?? string.Empty)) : (coord.nombre ?? string.Empty),
                Tipo = usuario != null ? usuario.tipo : 0,
                coord.fecha,
                coord.latitud,
                coord.longitud
            };

        var sql = consulta.ToQueryString();

        Assert.Contains("coordenadas_empleado", sql);
        Assert.Contains("usuarios_miorden", sql);
        Assert.Contains("LEFT JOIN", sql, StringComparison.OrdinalIgnoreCase);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
