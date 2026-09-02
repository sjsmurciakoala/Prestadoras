using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SIAD.Core.Constants;
using Xunit;

namespace SIAD.Tests.Seguridad;

/// <summary>
/// Protege el catálogo de permisos y el camino único de autorización.
///
/// No tocan la base: solo leen las constantes de <c>SIAD.Core</c>, así que corren siempre,
/// incluso sin <c>SIAD_TEST_DB</c>. Existen porque la autorización se unificó el 2026-09-01
/// (un solo criterio: el claim <c>permission</c>) y es fácil deshacerlo sin darse cuenta:
/// basta con agregar una policy por rol o un permiso sin su policy.
/// </summary>
public class CatalogoPermisosTests
{
    // Nombres heredados 'module.&lt;modulo&gt;' que la cascada sigue aceptando.
    private static readonly HashSet<string> Legacy = new(StringComparer.Ordinal)
    {
        "module.ventas", "module.bancos", "module.compras", "module.proveedores",
        "module.inventario", "module.contabilidad", "module.reporteria",
        "module.configuracion", "module.talentohumano",
    };

    [Fact]
    public void El_catalogo_no_tiene_permisos_repetidos()
    {
        var repetidos = PermissionNames.All
            .GroupBy(p => p, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToArray();

        Assert.True(repetidos.Length == 0,
            $"Permisos duplicados en PermissionNames.All: {string.Join(", ", repetidos)}");
    }

    [Fact]
    public void Todo_permiso_del_catalogo_tiene_su_policy()
    {
        var conPolicy = PermissionNames.Policies.Select(p => p.Policy).ToHashSet(StringComparer.Ordinal);
        var sinPolicy = PermissionNames.All.Where(p => !conPolicy.Contains(p)).ToArray();

        Assert.True(sinPolicy.Length == 0,
            "Estos permisos existen pero ninguna policy los registra, así que "
            + $"[Authorize(Policy = ...)] fallaría al arrancar: {string.Join(", ", sinPolicy)}");
    }

    [Fact]
    public void Toda_policy_admite_permisos_que_existen()
    {
        var validos = PermissionNames.All.ToHashSet(StringComparer.Ordinal);
        var inventados = new List<string>();

        foreach (var policy in PermissionNames.Policies)
        {
            foreach (var permiso in policy.Permissions)
            {
                if (!validos.Contains(permiso) && !Legacy.Contains(permiso))
                {
                    inventados.Add($"{policy.Policy} -> {permiso}");
                }
            }
        }

        Assert.True(inventados.Count == 0,
            $"Policies que aceptan permisos inexistentes: {string.Join("; ", inventados)}");
    }

    [Fact]
    public void Cada_policy_se_satisface_con_su_propio_permiso()
    {
        var rotas = PermissionNames.Policies
            .Where(p => !p.Permissions.Contains(p.Policy, StringComparer.Ordinal))
            .Select(p => p.Policy)
            .ToArray();

        Assert.True(rotas.Length == 0,
            "Una policy debe aceptar, como mínimo, el permiso que le da nombre. "
            + $"No lo hacen: {string.Join(", ", rotas)}");
    }

    [Fact]
    public void El_permiso_de_modulo_alcanza_para_las_opciones_de_ese_modulo()
    {
        // La cascada es lo que permite dar acceso por módulo sin enumerar cada pantalla:
        // el rol Ventas tiene module.ventas.view y con eso ve Clientes, Caja y Cobranza.
        var vistaDeOpcion = PermissionNames.Policies
            .Where(p => p.Policy.StartsWith("module.ventas.", StringComparison.Ordinal)
                        && p.Policy.EndsWith(".view", StringComparison.Ordinal)
                        && p.Policy != PermissionNames.Ventas.View)
            .ToArray();

        Assert.NotEmpty(vistaDeOpcion);

        var sinCascada = vistaDeOpcion
            .Where(p => !p.Permissions.Contains(PermissionNames.Ventas.View, StringComparer.Ordinal))
            .Select(p => p.Policy)
            .ToArray();

        Assert.True(sinCascada.Length == 0,
            $"Estas opciones de Ventas no las cubre module.ventas.view: {string.Join(", ", sinCascada)}");
    }

    [Fact]
    public void Los_permisos_sensibles_no_se_heredan()
    {
        // Aprobar presupuesto compromete fondos y el SQL libre deja escribir contra la base:
        // deben exigirse en forma explícita, nunca venir de paso con el permiso de módulo.
        foreach (var permiso in new[]
                 {
                     PermissionNames.Contabilidad.Presupuesto.Aprobar,
                     PermissionNames.Reporteria.SqlPersonalizado,
                 })
        {
            var policy = PermissionNames.Policies.Single(p => p.Policy == permiso);
            Assert.True(policy.Permissions.Length == 1,
                $"{permiso} debe exigirse tal cual; hoy también lo conceden: "
                + string.Join(", ", policy.Permissions.Where(x => x != permiso)));
        }
    }

    [Fact]
    public void Los_endpoints_del_catalogo_apuntan_a_permisos_reales()
    {
        var validos = PermissionNames.All.ToHashSet(StringComparer.Ordinal);
        var huerfanos = PermissionEndpointCatalog.All
            .Where(e => !validos.Contains(e.Permission))
            .Select(e => $"{e.DisplayName} -> {e.Permission}")
            .ToArray();

        Assert.True(huerfanos.Length == 0,
            $"Endpoints que exigen un permiso inexistente: {string.Join("; ", huerfanos)}");
    }

    [Fact]
    public void Solo_queda_un_rol_con_significado_en_el_codigo()
    {
        // Si alguien vuelve a agregar constantes de rol, es señal de que está naciendo un
        // segundo camino de autorización. Los roles son contenedores de permisos: el código
        // no debe preguntar por su nombre, salvo el bypass global.
        var constantes = typeof(RoleNames)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .ToArray();

        Assert.Equal(new[] { nameof(RoleNames.SuperAdministrador) }, constantes);
    }

    [Fact]
    public void La_unica_policy_que_no_es_un_permiso_es_la_de_Super_Administrador()
    {
        var constantes = typeof(AuthorizationPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => f.Name)
            .ToArray();

        Assert.Equal(new[] { nameof(AuthorizationPolicies.SuperAdmin) }, constantes);
    }

    [Fact]
    public void Todo_permiso_respeta_la_convencion_de_nombres()
    {
        // module.<modulo>[.<recurso>].<accion>, en minúsculas: es lo que arma la cascada.
        var malos = PermissionNames.All
            .Where(p => !p.StartsWith("module.", StringComparison.Ordinal)
                        || p != p.ToLowerInvariant()
                        || p.Split('.').Length < 3)
            .ToArray();

        Assert.True(malos.Length == 0,
            $"Permisos fuera de la convención module.<modulo>.<accion>: {string.Join(", ", malos)}");
    }
}
