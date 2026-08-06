using System;
using System.Linq;
using SIAD.Core.Constants;
using Xunit;

namespace SIAD.Tests.Almacen;

/// <summary>
/// Permisos de la carga inicial y los ajustes de inventario (Fase 3).
///
/// Además de comprobar que los permisos existen y son asignables, este archivo fija por
/// escrito el hallazgo que corrigió la revisión 3 del diseño: un sub-recurso dentro de un
/// módulo es un SUPERCONJUNTO del permiso de módulo, no una restricción. Quien lea estos
/// tests no debería volver a asumir que crear `module.inventario.carga_inicial.*` impide
/// algo a quien ya tiene `module.inventario.*`.
///
/// No requieren base de datos: son puros.
/// </summary>
public class PermisosInventarioTests
{
    [Fact]
    public void LosPermisosDeCargaInicialYAjustes_SonAsignables()
    {
        // Si no están en All, RolesPortalController los rechaza al guardarlos en un rol y
        // DatabaseInitializer no los siembra: serían inasignables.
        string[] esperados =
        [
            PermissionNames.Inventario.CargaInicial.View,
            PermissionNames.Inventario.CargaInicial.Create,
            PermissionNames.Inventario.CargaInicial.Edit,
            PermissionNames.Inventario.CargaInicial.Delete,
            PermissionNames.Inventario.Ajustes.View,
            PermissionNames.Inventario.Ajustes.Create,
            PermissionNames.Inventario.Ajustes.Edit,
            PermissionNames.Inventario.Ajustes.Delete
        ];

        foreach (var permiso in esperados)
        {
            Assert.Contains(permiso, PermissionNames.All, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void CadaPermisoNuevo_TieneSuPolicy()
    {
        string[] esperados =
        [
            PermissionNames.Inventario.CargaInicial.View,
            PermissionNames.Inventario.CargaInicial.Create,
            PermissionNames.Inventario.Ajustes.View,
            PermissionNames.Inventario.Ajustes.Create
        ];

        foreach (var permiso in esperados)
        {
            Assert.Contains(PermissionNames.Policies, p =>
                string.Equals(p.Policy, permiso, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    /// EL HALLAZGO de la rev.3: la policy del sub-recurso admite TAMBIÉN el permiso de
    /// módulo. Por eso un digitador con `module.inventario.create` pasa igual, y por eso
    /// cerrar/reabrir el corte NO se protegen con un recurso de inventario.
    /// </summary>
    [Fact]
    public void LaPolicyDelSubRecurso_AdmiteElPermisoDeModulo_NoEsUnaRestriccion()
    {
        var policy = PermissionNames.Policies.Single(p =>
            string.Equals(p.Policy, PermissionNames.Inventario.CargaInicial.Create, StringComparison.OrdinalIgnoreCase));

        Assert.Contains(PermissionNames.Inventario.Create, policy.Permissions, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void CerrarYReabrir_NoEstanEnElCatalogoDeInventario()
    {
        // Van con [ModuleAuthorize(PermissionModules.Configuracion)] SIN recurso: meterlos
        // aquí los devolvería al módulo inventario y perderían la restricción.
        Assert.DoesNotContain(PermissionEndpointCatalog.Inventario,
            e => e.Route.Contains("cerrar", StringComparison.OrdinalIgnoreCase)
              || e.Route.Contains("reabrir", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LosEndpointsDelCatalogo_EntranEnAllConSuPermisoLargo()
    {
        Assert.NotEmpty(PermissionEndpointCatalog.Inventario);

        foreach (var endpoint in PermissionEndpointCatalog.Inventario)
        {
            Assert.Equal(PermissionModules.Inventario, endpoint.Module);
            Assert.Contains(endpoint.Permission, PermissionNames.All, StringComparer.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void NoHayPermisosDuplicados()
    {
        var duplicados = PermissionNames.All
            .GroupBy(p => p, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(duplicados);
    }

    [Fact]
    public void NoHayPoliciesDuplicadas()
    {
        // Una policy repetida rompería AddAuthorization al registrarlas.
        var duplicadas = PermissionNames.Policies
            .GroupBy(p => p.Policy, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.Empty(duplicadas);
    }
}
