using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Patrón transaccional compartido del módulo de almacén: abre transacción propia SOLO si
/// no hay una ambiente.
/// <para>
/// No es un detalle de estilo. Los tests de integración envuelven cada prueba en
/// <c>BEGIN … ROLLBACK</c> (ver <c>IntegrationTestBase</c>): un servicio que abriera
/// transacción incondicionalmente quedaría fuera de cobertura, porque anidar no está
/// soportado. Reusando la ambiente, el mismo código es testeable y en producción sigue
/// siendo atómico.
/// </para>
/// <para>
/// Vivía duplicado como métodos privados de <c>ArticuloUbicacionService</c>; se extrajo
/// aquí para que el motor de posteo y los servicios de carga inicial usen exactamente el
/// mismo comportamiento en vez de copiarlo.
/// </para>
/// </summary>
public static class TransaccionAmbiente
{
    /// <summary>
    /// Devuelve una transacción nueva, o <c>null</c> si ya hay una ambiente (en cuyo caso la
    /// atomicidad la garantiza quien la abrió). El <c>await using</c> del llamador hace
    /// rollback + dispose si algo lanza.
    /// </summary>
    public static async Task<IDbContextTransaction?> IniciarAsync(SiadDbContext context, CancellationToken ct)
        => context.Database.CurrentTransaction is null
            ? await context.Database.BeginTransactionAsync(ct)
            : null;

    /// <summary>Commit de la transacción propia; no-op si estamos dentro de una ambiente.</summary>
    public static Task ConfirmarAsync(IDbContextTransaction? tx, CancellationToken ct)
        => tx is null ? Task.CompletedTask : tx.CommitAsync(ct);
}
