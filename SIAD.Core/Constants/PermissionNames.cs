using System.Collections.Generic;
using System.Linq;

namespace SIAD.Core.Constants;

public static class PermissionModules
{
    public const string Ventas = "ventas";
    public const string Bancos = "bancos";
    public const string Compras = "compras";
    public const string Proveedores = "proveedores";
    public const string Inventario = "inventario";
    public const string Contabilidad = "contabilidad";
    public const string Reporteria = "reporteria";
    public const string Configuracion = "configuracion";

    public static readonly string[] All =
    [
        Ventas,
        Bancos,
        Compras,
        Proveedores,
        Inventario,
        Contabilidad,
        Reporteria,
        Configuracion
    ];
}

public static class PermissionResources
{
    public static class Ventas
    {
        public const string Clientes = "clientes";
        public const string CaptacionPagos = "captacion_pagos";
        public const string Cobranza = "cobranza";
        public const string FacturacionMiscelaneos = "facturacion_miscelaneos";
        public const string NotasCreditoDebito = "notas_credito_debito";
        public const string Caja = "caja";

        // Períodos comerciales F7: abrir mes / cerrar ciclo / cerrar mes son
        // operaciones sensibles del calendario de facturación — recurso propio.
        public const string PeriodosComerciales = "periodos_comerciales";
    }

    public static class Contabilidad
    {
        public const string Integracion = "integracion";

        // Recurso propio del posteo por lote (D10: operación sensible —
        // configurar la integración NO debe implicar poder postear lotes).
        public const string LoteFacturacion = "lotefacturacion";

        // Reconciliación del caché oficial de saldos (F6): solo lectura,
        // pero con recurso propio para poder acotarla desde administración.
        public const string Saldos = "saldos";
    }
}

public static class PermissionNames
{
    public static class Ventas
    {
        public const string View = "module.ventas.view";
        public const string Create = "module.ventas.create";
        public const string Edit = "module.ventas.edit";
        public const string Delete = "module.ventas.delete";

        public static class Clientes
        {
            public const string View = "module.ventas.clientes.view";
            public const string Create = "module.ventas.clientes.create";
            public const string Edit = "module.ventas.clientes.edit";
            public const string Delete = "module.ventas.clientes.delete";
            public const string EditarNoCortable = "module.ventas.clientes.no_cortable.edit";
        }

        public static class CaptacionPagos
        {
            public const string View = "module.ventas.captacion_pagos.view";
            public const string Create = "module.ventas.captacion_pagos.create";
            public const string Edit = "module.ventas.captacion_pagos.edit";
            public const string Delete = "module.ventas.captacion_pagos.delete";
        }

        public static class Cobranza
        {
            public const string View = "module.ventas.cobranza.view";
            public const string Create = "module.ventas.cobranza.create";
            public const string Edit = "module.ventas.cobranza.edit";
            public const string Delete = "module.ventas.cobranza.delete";
        }

        public static class FacturacionMiscelaneos
        {
            public const string View = "module.ventas.facturacion_miscelaneos.view";
            public const string Create = "module.ventas.facturacion_miscelaneos.create";
            public const string Edit = "module.ventas.facturacion_miscelaneos.edit";
            public const string Delete = "module.ventas.facturacion_miscelaneos.delete";
        }

        public static class NotasCreditoDebito
        {
            public const string View = "module.ventas.notas_credito_debito.view";
            public const string Create = "module.ventas.notas_credito_debito.create";
            public const string Edit = "module.ventas.notas_credito_debito.edit";
            public const string Delete = "module.ventas.notas_credito_debito.delete";
        }

        public static class Caja
        {
            public const string View = "module.ventas.caja.view";
            public const string Create = "module.ventas.caja.create";
            public const string Edit = "module.ventas.caja.edit";
            public const string Delete = "module.ventas.caja.delete";
            public const string AbonoBanco = "module.ventas.caja.abono.banco";
        }
    }

    public static class Bancos
    {
        public const string View = "module.bancos.view";
        public const string Create = "module.bancos.create";
        public const string Edit = "module.bancos.edit";
        public const string Delete = "module.bancos.delete";
    }

    public static class Compras
    {
        public const string View = "module.compras.view";
        public const string Create = "module.compras.create";
        public const string Edit = "module.compras.edit";
        public const string Delete = "module.compras.delete";
    }

    public static class Proveedores
    {
        public const string View = "module.proveedores.view";
        public const string Create = "module.proveedores.create";
        public const string Edit = "module.proveedores.edit";
        public const string Delete = "module.proveedores.delete";
    }

    public static class Inventario
    {
        public const string View = "module.inventario.view";
        public const string Create = "module.inventario.create";
        public const string Edit = "module.inventario.edit";
        public const string Delete = "module.inventario.delete";
    }

    public static class Contabilidad
    {
        public const string View = "module.contabilidad.view";
        public const string Create = "module.contabilidad.create";
        public const string Edit = "module.contabilidad.edit";
        public const string Delete = "module.contabilidad.delete";
    }

    public static class Reporteria
    {
        public const string View = "module.reporteria.view";
        public const string Create = "module.reporteria.create";
        public const string Edit = "module.reporteria.edit";
        public const string Delete = "module.reporteria.delete";
    }

    public static class Configuracion
    {
        public const string View = "module.configuracion.view";
        public const string Create = "module.configuracion.create";
        public const string Edit = "module.configuracion.edit";
        public const string Delete = "module.configuracion.delete";
    }

    public static class Legacy
    {
        public const string Ventas = "module.ventas";
        public const string Bancos = "module.bancos";
        public const string Compras = "module.compras";
        public const string Proveedores = "module.proveedores";
        public const string Inventario = "module.inventario";
        public const string Contabilidad = "module.contabilidad";
        public const string Reporteria = "module.reporteria";
        public const string Configuracion = "module.configuracion";
    }

    public static readonly string[] All = BuildAll();

    private static string[] BuildAll()
    {
        var list = new List<string>
        {
            Ventas.View,
            Ventas.Create,
            Ventas.Edit,
            Ventas.Delete,
            Bancos.View,
            Bancos.Create,
            Bancos.Edit,
            Bancos.Delete,
            Compras.View,
            Compras.Create,
            Compras.Edit,
            Compras.Delete,
            Proveedores.View,
            Proveedores.Create,
            Proveedores.Edit,
            Proveedores.Delete,
            Inventario.View,
            Inventario.Create,
            Inventario.Edit,
            Inventario.Delete,
            Contabilidad.View,
            Contabilidad.Create,
            Contabilidad.Edit,
            Contabilidad.Delete,
            Reporteria.View,
            Reporteria.Create,
            Reporteria.Edit,
            Reporteria.Delete,
            Configuracion.View,
            Configuracion.Create,
            Configuracion.Edit,
            Configuracion.Delete,

            Ventas.Clientes.View,
            Ventas.Clientes.Create,
            Ventas.Clientes.Edit,
            Ventas.Clientes.Delete,
            Ventas.Clientes.EditarNoCortable,
            Ventas.CaptacionPagos.View,
            Ventas.CaptacionPagos.Create,
            Ventas.CaptacionPagos.Edit,
            Ventas.CaptacionPagos.Delete,
            Ventas.Cobranza.View,
            Ventas.Cobranza.Create,
            Ventas.Cobranza.Edit,
            Ventas.Cobranza.Delete,
            Ventas.FacturacionMiscelaneos.View,
            Ventas.FacturacionMiscelaneos.Create,
            Ventas.FacturacionMiscelaneos.Edit,
            Ventas.FacturacionMiscelaneos.Delete,
            Ventas.NotasCreditoDebito.View,
            Ventas.NotasCreditoDebito.Create,
            Ventas.NotasCreditoDebito.Edit,
            Ventas.NotasCreditoDebito.Delete,
            Ventas.Caja.View,
            Ventas.Caja.Create,
            Ventas.Caja.Edit,
            Ventas.Caja.Delete,
            Ventas.Caja.AbonoBanco
        };

        list.AddRange(PermissionEndpointCatalog.All.Select(e => e.Permission));

        return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public sealed record PermissionPolicyDefinition(string Policy, string[] Permissions);

    public static readonly PermissionPolicyDefinition[] Policies = BuildPolicies();

    private static PermissionPolicyDefinition[] BuildPolicies()
    {
        var policies = new List<PermissionPolicyDefinition>
        {
        new PermissionPolicyDefinition(Ventas.View, [Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Create, [Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Edit, [Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Delete, [Ventas.Delete]),
        new PermissionPolicyDefinition(Bancos.View, [Bancos.View, Legacy.Bancos]),
        new PermissionPolicyDefinition(Bancos.Create, [Bancos.Create]),
        new PermissionPolicyDefinition(Bancos.Edit, [Bancos.Edit]),
        new PermissionPolicyDefinition(Bancos.Delete, [Bancos.Delete]),
        new PermissionPolicyDefinition(Compras.View, [Compras.View, Legacy.Compras]),
        new PermissionPolicyDefinition(Compras.Create, [Compras.Create]),
        new PermissionPolicyDefinition(Compras.Edit, [Compras.Edit]),
        new PermissionPolicyDefinition(Compras.Delete, [Compras.Delete]),
        new PermissionPolicyDefinition(Proveedores.View, [Proveedores.View, Legacy.Proveedores]),
        new PermissionPolicyDefinition(Proveedores.Create, [Proveedores.Create]),
        new PermissionPolicyDefinition(Proveedores.Edit, [Proveedores.Edit]),
        new PermissionPolicyDefinition(Proveedores.Delete, [Proveedores.Delete]),
        new PermissionPolicyDefinition(Inventario.View, [Inventario.View, Legacy.Inventario]),
        new PermissionPolicyDefinition(Inventario.Create, [Inventario.Create]),
        new PermissionPolicyDefinition(Inventario.Edit, [Inventario.Edit]),
        new PermissionPolicyDefinition(Inventario.Delete, [Inventario.Delete]),
        new PermissionPolicyDefinition(Contabilidad.View, [Contabilidad.View, Legacy.Contabilidad]),
        new PermissionPolicyDefinition(Contabilidad.Create, [Contabilidad.Create]),
        new PermissionPolicyDefinition(Contabilidad.Edit, [Contabilidad.Edit]),
        new PermissionPolicyDefinition(Contabilidad.Delete, [Contabilidad.Delete]),
        new PermissionPolicyDefinition(Reporteria.View, [Reporteria.View, Legacy.Reporteria]),
        new PermissionPolicyDefinition(Reporteria.Create, [Reporteria.Create]),
        new PermissionPolicyDefinition(Reporteria.Edit, [Reporteria.Edit]),
        new PermissionPolicyDefinition(Reporteria.Delete, [Reporteria.Delete]),
        new PermissionPolicyDefinition(Configuracion.View, [Configuracion.View, Legacy.Configuracion]),
        new PermissionPolicyDefinition(Configuracion.Create, [Configuracion.Create]),
        new PermissionPolicyDefinition(Configuracion.Edit, [Configuracion.Edit]),
        new PermissionPolicyDefinition(Configuracion.Delete, [Configuracion.Delete]),

        new PermissionPolicyDefinition(Ventas.Clientes.View, [Ventas.Clientes.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Clientes.Create, [Ventas.Clientes.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Clientes.Edit, [Ventas.Clientes.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Clientes.Delete, [Ventas.Clientes.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Clientes.EditarNoCortable, [Ventas.Clientes.EditarNoCortable]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.View, [Ventas.CaptacionPagos.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.Create, [Ventas.CaptacionPagos.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.Edit, [Ventas.CaptacionPagos.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.CaptacionPagos.Delete, [Ventas.CaptacionPagos.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Cobranza.View, [Ventas.Cobranza.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Cobranza.Create, [Ventas.Cobranza.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Cobranza.Edit, [Ventas.Cobranza.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Cobranza.Delete, [Ventas.Cobranza.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.View, [Ventas.FacturacionMiscelaneos.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.Create, [Ventas.FacturacionMiscelaneos.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.Edit, [Ventas.FacturacionMiscelaneos.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.FacturacionMiscelaneos.Delete, [Ventas.FacturacionMiscelaneos.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.View, [Ventas.NotasCreditoDebito.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.Create, [Ventas.NotasCreditoDebito.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.Edit, [Ventas.NotasCreditoDebito.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.NotasCreditoDebito.Delete, [Ventas.NotasCreditoDebito.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Caja.View, [Ventas.Caja.View, Ventas.View, Legacy.Ventas]),
        new PermissionPolicyDefinition(Ventas.Caja.Create, [Ventas.Caja.Create, Ventas.Create]),
        new PermissionPolicyDefinition(Ventas.Caja.Edit, [Ventas.Caja.Edit, Ventas.Edit]),
        new PermissionPolicyDefinition(Ventas.Caja.Delete, [Ventas.Caja.Delete, Ventas.Delete]),
        new PermissionPolicyDefinition(Ventas.Caja.AbonoBanco, [Ventas.Caja.AbonoBanco, Ventas.Caja.Create])
        };

        foreach (var endpoint in PermissionEndpointCatalog.All)
        {
            var permissions = new List<string>
            {
                endpoint.Permission,
                PermissionKeyBuilder.BuildPermission(endpoint.Module, endpoint.Option, endpoint.Action),
                PermissionKeyBuilder.BuildModulePermission(endpoint.Module, endpoint.Action)
            };

            if (endpoint.Action == PermissionAction.View)
            {
                permissions.Add($"module.{endpoint.Module}");
            }

            var distinct = permissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            policies.Add(new PermissionPolicyDefinition(endpoint.Permission, distinct));
        }

        return policies.ToArray();
    }
}
