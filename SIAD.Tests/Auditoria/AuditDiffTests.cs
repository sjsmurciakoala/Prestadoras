using System.Collections.Generic;
using System.Text.Json;
using SIAD.Data.Auditoria;
using Xunit;

namespace SIAD.Tests.Auditoria;

public class AuditDiffTests
{
    [Fact]
    public void Nuevos_serializa_objeto_campo_valor()
    {
        var campos = new List<AuditDiff.Campo> { new("nombre", "viejo", "nuevo"), new("activo", true, false) };
        var json = AuditDiff.SerializeNuevos(campos);
        using var doc = JsonDocument.Parse(json!);
        Assert.Equal("nuevo", doc.RootElement.GetProperty("nombre").GetString());
        Assert.False(doc.RootElement.GetProperty("activo").GetBoolean());
    }

    [Fact]
    public void Anteriores_serializa_objeto_campo_valor()
    {
        var campos = new List<AuditDiff.Campo> { new("nombre", "viejo", "nuevo") };
        var json = AuditDiff.SerializeAnteriores(campos);
        using var doc = JsonDocument.Parse(json!);
        Assert.Equal("viejo", doc.RootElement.GetProperty("nombre").GetString());
    }

    [Fact]
    public void Lista_vacia_devuelve_null()
    {
        Assert.Null(AuditDiff.SerializeNuevos(new List<AuditDiff.Campo>()));
        Assert.Null(AuditDiff.SerializeAnteriores(new List<AuditDiff.Campo>()));
    }
}
