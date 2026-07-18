using System.Collections.Generic;
using System.Linq;

namespace SIAD.Core.Constants;

/// <summary>
/// Lista blanca de tablas maestras candidatas a auditoría (bitácora de maestros).
/// La clave es el nombre de TABLA (entry.Metadata.GetTableName()).
/// Solo se audita lo que además esté habilitado en bitacora_maestro_config.
/// Captura: la mayoría se auditan por el interceptor de SaveChanges (entidades keyed).
/// EXCEPCIÓN: prv_proveedores se persiste con SQL crudo (ExecuteSqlRaw) y entidad keyless,
/// por lo que el interceptor NO lo ve; se audita explícitamente en ProveedoresService vía
/// IBitacoraMaestrosWriter. (Las cuentas bancarias de proveedor sí van por el interceptor.)
/// </summary>
public static class AuditableMaestros
{
    public sealed record Item(string Tabla, string Nombre, string Modulo);

    public static readonly IReadOnlyList<Item> All =
    [
        new("cliente_maestro",                "Maestro de clientes",            "Clientes"),
        new("alm_articulo",                   "Artículos",                      "Almacén"),
        new("alm_grupo",                      "Grupos de artículo",             "Almacén"),
        new("alm_tipo_articulo",              "Tipos de artículo",              "Almacén"),
        new("alm_bodega",                     "Bodegas",                        "Almacén"),
        new("alm_categoria_unidad",           "Categorías de unidad",           "Almacén"),
        new("alm_unidad_medida",              "Unidades de medida",             "Almacén"),
        // Ojo: la entidad se llama prv_proveedore pero NO declara ToTable, así que EF usa
        // el nombre del DbSet (prv_proveedores) como nombre de tabla. GetTableName() =>
        // "prv_proveedores". Por eso la clave es la forma plural, no "prv_proveedor".
        new("prv_proveedores",                "Maestro de proveedores",         "Proveedores"),
        new("prv_proveedor_cuenta_bancaria",  "Cuentas bancarias de proveedor", "Proveedores"),
    ];

    private static readonly HashSet<string> _tablas =
        All.Select(x => x.Tabla).ToHashSet(System.StringComparer.OrdinalIgnoreCase);

    public static bool EsAuditable(string? tabla) => tabla is not null && _tablas.Contains(tabla);

    public static string NombreDe(string tabla) =>
        All.FirstOrDefault(x => string.Equals(x.Tabla, tabla, System.StringComparison.OrdinalIgnoreCase))?.Nombre ?? tabla;
}
