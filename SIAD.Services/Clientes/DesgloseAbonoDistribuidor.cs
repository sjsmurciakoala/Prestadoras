using SIAD.Core.DTOs.Clientes;

namespace SIAD.Services.Clientes;

/// <summary>
/// Reparte el neto de pagos/abonos del cliente entre los ítems del desglose por
/// servicio según los porcentajes configurados en adm_desglose_abono_porcentaje.
/// Lógica pura (sin BD) para poder probarla en unitario. La suma de las filas
/// devueltas siempre es igual a items + pagos + ajustes, de modo que el TOTAL del
/// desglose cuadra con el saldo actual del cliente.
/// </summary>
public static class DesgloseAbonoDistribuidor
{
    /// <summary>Código del ítem especial del desglose para el saldo migrado.</summary>
    public const string CodigoSaldoAnterior = "SALDO_ANTERIOR";

    public sealed record ItemDesglose(string Codigo, string Nombre, decimal Saldo, int Orden);

    /// <param name="items">Ítems del desglose (servicios del catálogo y, si existe, el saldo anterior), ya ordenados.</param>
    /// <param name="pagos">Neto de pagos/abonos vigentes (negativo cuando hay abonos).</param>
    /// <param name="ajustes">Neto de NC/ND y otros movimientos no distribuibles.</param>
    /// <param name="porcentajes">Configuración por código de ítem; distribuye solo si suma exactamente 100.</param>
    public static IReadOnlyList<SaldoServicioDto> Distribuir(
        IReadOnlyList<ItemDesglose> items,
        decimal pagos,
        decimal ajustes,
        IReadOnlyDictionary<string, decimal> porcentajes)
    {
        var saldos = items.ToDictionary(i => i.Codigo, i => i.Saldo);
        var pctAplicado = new Dictionary<string, decimal>();
        var sinDistribuir = pagos;

        if (pagos != 0m && porcentajes.Count > 0 && porcentajes.Values.Sum() == 100m)
        {
            // Solo reparte entre ítems configurados que este cliente tiene en su
            // desglose, renormalizando los pesos: si un ítem configurado no aparece
            // (p. ej. no tiene saldo anterior), su cuota se reparte proporcionalmente
            // entre el resto. El residuo de redondeo cae en el ítem de mayor peso.
            var presentes = items
                .Where(i => porcentajes.TryGetValue(i.Codigo, out var pct) && pct > 0m)
                .OrderByDescending(i => porcentajes[i.Codigo])
                .ThenBy(i => i.Orden)
                .ToList();

            var pesoTotal = presentes.Sum(i => porcentajes[i.Codigo]);
            if (pesoTotal > 0m)
            {
                var repartido = 0m;
                foreach (var item in presentes.Skip(1))
                {
                    var cuota = decimal.Round(
                        pagos * porcentajes[item.Codigo] / pesoTotal, 2, MidpointRounding.AwayFromZero);
                    saldos[item.Codigo] += cuota;
                    repartido += cuota;
                }

                saldos[presentes[0].Codigo] += pagos - repartido;
                sinDistribuir = 0m;

                foreach (var item in presentes)
                {
                    pctAplicado[item.Codigo] = porcentajes[item.Codigo];
                }
            }
        }

        var resultado = items
            .Select(i => new SaldoServicioDto(
                i.Nombre,
                saldos[i.Codigo],
                0,
                Deuda: i.Saldo,
                Porcentaje: pctAplicado.TryGetValue(i.Codigo, out var pct) ? pct : null))
            .ToList();

        var resto = sinDistribuir + ajustes;
        if (resto != 0m)
        {
            resultado.Add(new SaldoServicioDto("Pagos y ajustes", resto, 0, Deuda: resto));
        }

        return resultado;
    }
}
