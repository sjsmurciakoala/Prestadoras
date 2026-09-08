using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.ActivosFijos;

/// <summary>
/// Historial de asignación del activo (<c>af_activo_asignacion</c>), la mejora del módulo
/// frente a SIMAFI y al desarrollo de Merendon, que solo guardan el responsable ACTUAL.
/// <para>
/// La invariante dura: <b>una sola asignación vigente por activo</b> (la de
/// <c>fecha_hasta IS NULL</c>), y esa fila tiene que ser espejo de lo que muestra el maestro.
/// Se abre por dos caminos —<c>sp_af_activo_asignar</c> y la propia ficha
/// (<c>sp_af_activo_guardar</c>)— y los dos se prueban aquí.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class ActivoFijoAsignacionTests : ActivosFijosTestBase
{
    public ActivoFijoAsignacionTests(PostgresFixture fixture) : base(fixture)
    {
    }

    // ═══════════════════════════════════════════════ Apertura desde la ficha

    [SkippableFact]
    public async Task Guardar_con_responsable_abre_la_asignacion_inicial()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var ubicacionId = await CrearUbicacionAsync(nombre: "BODEGA CENTRAL");
        var empleadoId = await CrearEmpleadoAsync("ANA LOPEZ");
        var compra = Hoy(-30);

        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: empleadoId, ubicacionId: ubicacionId,
            cargoResponsable: "JEFE DE BODEGA", fechaCompra: compra);

        var unica = Assert.Single(await LeerAsignacionesAsync(activoId));

        Assert.True(unica.vigente);
        Assert.Null(unica.fecha_hasta);
        Assert.Equal(Texto(compra), unica.fecha_desde);   // arranca el día de la compra
        Assert.Equal(empleadoId, unica.empleado_id);
        Assert.Equal("ANA LOPEZ", unica.responsable);
        Assert.Equal("JEFE DE BODEGA", unica.cargo_responsable);
        Assert.Equal(ubicacionId, unica.ubicacion_id);
        Assert.Equal("BODEGA CENTRAL", unica.ubicacion);
        Assert.Equal("Asignación inicial del registro", unica.motivo);

        // El maestro copia el nombre del catálogo, no el texto libre.
        var activo = await LeerActivoAsync(activoId);
        Assert.Equal(empleadoId, activo.empleado_id);
        Assert.Equal("ANA LOPEZ", activo.responsable);
    }

    /// <summary>Sin responsable ni ubicación no hay nada que historiar: no se abre asignación.</summary>
    [SkippableFact]
    public async Task Guardar_sin_responsable_ni_ubicacion_no_abre_asignacion()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var (activoId, _) = await GuardarActivoAsync(tipoActivoId: tipoId);

        Assert.Empty(await LeerAsignacionesAsync(activoId));
        Assert.Equal(0, await ContarAsignacionesVigentesAsync(activoId));
    }

    /// <summary>Editar la ficha cambiando de responsable cierra la vigente y abre otra.</summary>
    [SkippableFact]
    public async Task Editar_la_ficha_cambiando_de_responsable_cierra_la_vigente_y_abre_otra()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var primero = await CrearEmpleadoAsync("ANA LOPEZ");
        var segundo = await CrearEmpleadoAsync("CARLOS DIAZ");
        var compra = Hoy(-30);

        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: primero, fechaCompra: compra);

        await GuardarActivoAsync(
            id: activoId, tipoActivoId: tipoId, empleadoId: segundo, fechaCompra: compra);

        var asignaciones = await LeerAsignacionesAsync(activoId);
        Assert.Equal(2, asignaciones.Count);

        var vigente = asignaciones[0];   // el listado ordena por fecha_desde y por id, descendente
        var cerrada = asignaciones[1];

        Assert.True(vigente.vigente);
        Assert.Equal(segundo, vigente.empleado_id);
        Assert.Equal("CARLOS DIAZ", vigente.responsable);
        Assert.Equal("Cambio registrado desde la ficha del activo", vigente.motivo);

        Assert.False(cerrada.vigente);
        Assert.Equal(Texto(Hoy()), cerrada.fecha_hasta);
        Assert.Equal(primero, cerrada.empleado_id);

        Assert.Equal(1, await ContarAsignacionesVigentesAsync(activoId));
    }

    /// <summary>Editar sin tocar responsable, ubicación ni centro de costo NO abre otra fila.</summary>
    [SkippableFact]
    public async Task Editar_la_ficha_sin_cambiar_la_asignacion_no_abre_otra()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var empleadoId = await CrearEmpleadoAsync("ANA LOPEZ");
        var ubicacionId = await CrearUbicacionAsync();
        var compra = Hoy(-30);

        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: empleadoId, ubicacionId: ubicacionId, fechaCompra: compra);

        await GuardarActivoAsync(
            id: activoId, descripcion: "MISMO ACTIVO, OTRA DESCRIPCION", tipoActivoId: tipoId,
            empleadoId: empleadoId, ubicacionId: ubicacionId, fechaCompra: compra);

        var unica = Assert.Single(await LeerAsignacionesAsync(activoId));
        Assert.True(unica.vigente);
        Assert.Equal(Texto(compra), unica.fecha_desde);
    }

    // ═══════════════════════════════════════════════ sp_af_activo_asignar

    [SkippableFact]
    public async Task Asignar_cierra_la_vigente_y_abre_la_nueva()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var bodega = await CrearUbicacionAsync(nombre: "BODEGA CENTRAL");
        var sucursal = await CrearUbicacionAsync(nombre: "SUCURSAL NORTE");
        var cargoId = await CrearCargoAsync("SUPERVISOR DE PRUEBA");
        var ana = await CrearEmpleadoAsync("ANA LOPEZ");
        var carlos = await CrearEmpleadoAsync("CARLOS DIAZ", cargoId);
        var compra = Hoy(-30);

        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: ana, ubicacionId: bodega, fechaCompra: compra);

        await AsignarAsync(activoId, fecha: Hoy(), empleadoId: carlos, ubicacionId: sucursal,
            motivo: "Traslado a la sucursal");

        var asignaciones = await LeerAsignacionesAsync(activoId);
        Assert.Equal(2, asignaciones.Count);

        var vigente = asignaciones[0];
        Assert.True(vigente.vigente);
        Assert.Null(vigente.fecha_hasta);
        Assert.Equal(Texto(Hoy()), vigente.fecha_desde);
        Assert.Equal(carlos, vigente.empleado_id);
        Assert.Equal("CARLOS DIAZ", vigente.responsable);
        Assert.Equal("SUPERVISOR DE PRUEBA", vigente.cargo_responsable);   // lo copia de th_cargo
        Assert.Equal(sucursal, vigente.ubicacion_id);
        Assert.Equal("SUCURSAL NORTE", vigente.ubicacion);
        Assert.Equal("Traslado a la sucursal", vigente.motivo);

        var cerrada = asignaciones[1];
        Assert.False(cerrada.vigente);
        Assert.Equal(Texto(compra), cerrada.fecha_desde);
        Assert.Equal(Texto(Hoy()), cerrada.fecha_hasta);
        Assert.Equal(ana, cerrada.empleado_id);

        Assert.Equal(1, await ContarAsignacionesVigentesAsync(activoId));
    }

    /// <summary>El maestro queda como espejo de la asignación vigente.</summary>
    [SkippableFact]
    public async Task Asignar_actualiza_el_maestro_con_responsable_cargo_y_ubicacion()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var ubicacionId = await CrearUbicacionAsync(nombre: "TALLER");
        var cargoId = await CrearCargoAsync("MECANICO DE PRUEBA");
        var empleadoId = await CrearEmpleadoAsync("LUIS MEJIA", cargoId);

        var (activoId, _) = await GuardarActivoAsync(tipoActivoId: tipoId, responsable: "RESPONSABLE VIEJO");

        await AsignarAsync(activoId, fecha: Hoy(), empleadoId: empleadoId, ubicacionId: ubicacionId);

        var activo = await LeerActivoAsync(activoId);
        Assert.Equal(empleadoId, activo.empleado_id);
        Assert.Equal("LUIS MEJIA", activo.responsable);
        Assert.Equal("MECANICO DE PRUEBA", activo.cargo_responsable);
        Assert.Equal(ubicacionId, activo.ubicacion_id);
        Assert.Equal(Texto(Hoy()), activo.fecha_asignacion);
    }

    /// <summary>
    /// La invariante del historial: por muchas reasignaciones que se hagan, nunca queda más
    /// de una fila abierta (la respalda el índice único parcial <c>uq_af_activo_asignacion_vigente</c>).
    /// </summary>
    [SkippableFact]
    public async Task Varias_reasignaciones_nunca_dejan_mas_de_una_vigente()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var ana = await CrearEmpleadoAsync("ANA LOPEZ");
        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: ana, fechaCompra: Hoy(-30));

        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var empleadoId = await CrearEmpleadoAsync("RESPONSABLE " + vuelta);
            await AsignarAsync(activoId, fecha: Hoy(), empleadoId: empleadoId, motivo: "Vuelta " + vuelta);
            Assert.Equal(1, await ContarAsignacionesVigentesAsync(activoId));
        }

        var asignaciones = await LeerAsignacionesAsync(activoId);
        Assert.Equal(4, asignaciones.Count);   // la inicial + tres reasignaciones

        var abiertas = 0;
        foreach (var asignacion in asignaciones)
        {
            if (asignacion.vigente) abiertas++;
        }

        Assert.Equal(1, abiertas);
        Assert.Equal("RESPONSABLE 2", asignaciones[0].responsable);   // la última manda
    }

    /// <summary>Asignar solo a una ubicación (sin responsable) es válido: el activo queda en bodega.</summary>
    [SkippableFact]
    public async Task Asignar_solo_a_una_ubicacion_deja_el_responsable_en_blanco()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var ubicacionId = await CrearUbicacionAsync(nombre: "BODEGA DE RESGUARDO");
        var ana = await CrearEmpleadoAsync("ANA LOPEZ");

        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: ana, fechaCompra: Hoy(-30));

        await AsignarAsync(activoId, fecha: Hoy(), empleadoId: null, ubicacionId: ubicacionId,
            motivo: "Devuelto a bodega");

        var vigente = (await LeerAsignacionesAsync(activoId))[0];
        Assert.Null(vigente.empleado_id);
        Assert.Null(vigente.responsable);
        Assert.Equal(ubicacionId, vigente.ubicacion_id);

        var activo = await LeerActivoAsync(activoId);
        Assert.Null(activo.empleado_id);
        Assert.Null(activo.responsable);
        Assert.Equal(ubicacionId, activo.ubicacion_id);
    }

    // ═══════════════════════════════════════════════ Validaciones

    [SkippableFact]
    public async Task Asignar_con_fecha_anterior_al_inicio_de_la_vigente_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var ana = await CrearEmpleadoAsync("ANA LOPEZ");
        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, empleadoId: ana, fechaCompra: Hoy(-30));

        var error = await ErrorDeAsync(() => AsignarAsync(activoId, fecha: Hoy(-40), empleadoId: ana));

        Assert.Contains("no puede ser anterior al inicio de la asignación vigente", error);
        Assert.Equal(1, await ContarAsignacionesVigentesAsync(activoId));
    }

    [SkippableFact]
    public async Task Asignar_con_fecha_futura_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, _) = await GuardarActivoAsync(tipoActivoId: tipoId);

        Assert.Contains("fecha de asignación no puede ser futura",
            await ErrorDeAsync(() => AsignarAsync(activoId, fecha: Hoy(1))));
    }

    /// <summary>Un activo dado de baja o vendido ya salió del patrimonio: no se le asigna nada.</summary>
    [SkippableFact]
    public async Task Asignar_un_activo_en_estado_final_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, estadoActivoId: EstadoDadoDeBaja);

        Assert.Contains("ya salió del patrimonio",
            await ErrorDeAsync(() => AsignarAsync(activoId, fecha: Hoy())));
    }

    [SkippableFact]
    public async Task Asignar_un_empleado_o_un_activo_inexistente_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, _) = await GuardarActivoAsync(tipoActivoId: tipoId);

        Assert.Contains("responsable seleccionado no existe",
            await ErrorDeAsync(() => AsignarAsync(activoId, fecha: Hoy(), empleadoId: -1)));

        Assert.Contains("No se encontró el activo",
            await ErrorDeAsync(() => AsignarAsync(IdInexistente, fecha: Hoy())));
    }
}
