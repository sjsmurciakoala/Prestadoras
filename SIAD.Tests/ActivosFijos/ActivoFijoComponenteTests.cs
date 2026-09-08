using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.ActivosFijos;

/// <summary>
/// Componentes y accesorios del activo (<c>af_activo_componente</c>): el disco de una
/// computadora, la cama de un camión. Sustituyen los dos campos de texto libre que el
/// desarrollo de Merendon tenía en la ficha.
/// <para>
/// En la Fase 1 su valor es INFORMATIVO: no altera el valor depreciable del activo, y esta
/// clase lo deja escrito en una prueba para que no se cambie por accidente.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class ActivoFijoComponenteTests : ActivosFijosTestBase
{
    public ActivoFijoComponenteTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private async Task<int> CrearActivoAsync()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, _) = await GuardarActivoAsync(tipoActivoId: tipoId, valorCompra: 30_000m);
        return activoId;
    }

    [SkippableFact]
    public async Task Componente_alta_edicion_y_baja()
    {
        var activoId = await CrearActivoAsync();

        // Alta
        var componenteId = await GuardarComponenteAsync(
            activoId, descripcion: "DISCO SOLIDO 1 TB", marca: "SEAGATE", modelo: "BARRACUDA",
            serie: "SN-0001", cantidad: 2m, valor: 1_500.50m, observacion: "Instalado de fábrica");

        Assert.True(componenteId > 0);

        var alta = Assert.Single(await LeerComponentesAsync(activoId));
        Assert.Equal(componenteId, alta.id);
        Assert.Equal("DISCO SOLIDO 1 TB", alta.descripcion);
        Assert.Equal("SEAGATE", alta.marca);
        Assert.Equal("BARRACUDA", alta.modelo);
        Assert.Equal("SN-0001", alta.serie);
        Assert.Equal(2m, alta.cantidad);
        Assert.Equal(1_500.50m, alta.valor);
        Assert.Equal("Instalado de fábrica", alta.observacion);

        // Edición: el mismo id, con otros valores.
        var mismoId = await GuardarComponenteAsync(
            activoId, id: componenteId, descripcion: "DISCO SOLIDO 2 TB",
            marca: "SEAGATE", cantidad: 1m, valor: 2_200m);

        Assert.Equal(componenteId, mismoId);

        var editado = Assert.Single(await LeerComponentesAsync(activoId));
        Assert.Equal("DISCO SOLIDO 2 TB", editado.descripcion);
        Assert.Equal(1m, editado.cantidad);
        Assert.Equal(2_200m, editado.valor);
        Assert.Null(editado.modelo);        // lo que no se manda se limpia
        Assert.Null(editado.observacion);

        // Baja
        await EliminarComponenteAsync(componenteId);
        Assert.Empty(await LeerComponentesAsync(activoId));

        // Borrar dos veces no pasa desapercibido.
        Assert.Contains("No se encontró el componente",
            await ErrorDeAsync(() => EliminarComponenteAsync(componenteId)));
    }

    [SkippableFact]
    public async Task Componente_toma_cantidad_uno_y_valor_cero_cuando_no_se_capturan()
    {
        var activoId = await CrearActivoAsync();

        await GuardarComponenteAsync(activoId, descripcion: "TECLADO", cantidad: null, valor: null);

        var componente = Assert.Single(await LeerComponentesAsync(activoId));
        Assert.Equal(1m, componente.cantidad);
        Assert.Equal(0m, componente.valor);
    }

    /// <summary>Fase 1: el valor del componente es informativo y no toca el valor depreciable.</summary>
    [SkippableFact]
    public async Task Valor_del_componente_no_altera_el_valor_depreciable_del_activo()
    {
        var activoId = await CrearActivoAsync();
        var antes = await LeerActivoAsync(activoId);

        await GuardarComponenteAsync(activoId, descripcion: "CAMA METALICA", valor: 15_000m);

        var despues = await LeerActivoAsync(activoId);
        Assert.Equal(antes.valor_compra, despues.valor_compra);
        Assert.Equal(antes.valor_a_depreciar, despues.valor_a_depreciar);
        Assert.Equal(antes.valor_libros, despues.valor_libros);
    }

    [SkippableFact]
    public async Task Componentes_de_otro_activo_no_aparecen_en_el_listado()
    {
        var primero = await CrearActivoAsync();
        var segundo = await CrearActivoAsync();

        await GuardarComponenteAsync(primero, descripcion: "COMPONENTE DEL PRIMERO");
        await GuardarComponenteAsync(segundo, descripcion: "COMPONENTE DEL SEGUNDO");

        Assert.Equal("COMPONENTE DEL PRIMERO", Assert.Single(await LeerComponentesAsync(primero)).descripcion);
        Assert.Equal("COMPONENTE DEL SEGUNDO", Assert.Single(await LeerComponentesAsync(segundo)).descripcion);
    }

    /// <summary>El id del componente no basta: la edición exige que sea de ESE activo.</summary>
    [SkippableFact]
    public async Task Editar_un_componente_desde_otro_activo_no_lo_encuentra()
    {
        var primero = await CrearActivoAsync();
        var segundo = await CrearActivoAsync();
        var componenteId = await GuardarComponenteAsync(primero, descripcion: "COMPONENTE DEL PRIMERO");

        var error = await ErrorDeAsync(() => GuardarComponenteAsync(
            segundo, id: componenteId, descripcion: "SECUESTRADO"));

        Assert.Contains("No se encontró el componente", error);
        Assert.Equal("COMPONENTE DEL PRIMERO", Assert.Single(await LeerComponentesAsync(primero)).descripcion);
    }

    [SkippableFact]
    public async Task Componente_valida_descripcion_cantidad_valor_y_activo()
    {
        var activoId = await CrearActivoAsync();

        Assert.Contains("descripción del componente es obligatoria",
            await ErrorDeAsync(() => GuardarComponenteAsync(activoId, descripcion: "   ")));

        Assert.Contains("cantidad del componente debe ser mayor que cero",
            await ErrorDeAsync(() => GuardarComponenteAsync(activoId, cantidad: 0m)));

        Assert.Contains("cantidad del componente debe ser mayor que cero",
            await ErrorDeAsync(() => GuardarComponenteAsync(activoId, cantidad: -1m)));

        Assert.Contains("valor del componente no puede ser negativo",
            await ErrorDeAsync(() => GuardarComponenteAsync(activoId, valor: -0.01m)));

        Assert.Contains("No se encontró el activo",
            await ErrorDeAsync(() => GuardarComponenteAsync(-1, descripcion: "HUERFANO")));
    }
}
