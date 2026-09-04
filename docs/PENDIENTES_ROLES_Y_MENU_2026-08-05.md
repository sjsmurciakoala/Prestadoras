# Pendientes de roles, permisos y menú (hallazgos del ensayo 2026-08-05)

Documento de revisión. **No hay cambios de código todavía** — solo el registro
de lo encontrado durante el ensayo en el servidor 0.9 (base `siad_v4`), para
decidir e implementar después.

## Contexto

Durante el ensayo se armaron dos roles por SQL directo sobre `siad_v4`
(el rol no existía en la BD aunque estaba definido en `RoleNames.cs`):

- **Cobranzas** — `Database/2026-08-05_rol_cobranzas_permisos.sql` (24 permisos).
  Usuario: `cobranzas@aguasdepuertocortes.com`.
- **Comercial** — `Database/2026-08-05_rol_comercial_permisos.sql` (20 permisos,
  set "limpio" Opción A). Usuario: `comercial@aguasdepuertocortes.com`.

Ambos scripts son idempotentes y quedaron probados contra `siad_v3_copia09`.

## Hallazgo 1 (el importante): el menú NO se filtra por permisos

**Estado actual**: `apc.Client/Layout/SidebarNavigation.razor` solo oculta:
1. Secciones con `RequiredPolicy` = Super Administrador.
2. Ítems con `RequiredCapability` (config de empresa, p.ej. cheque manual).
3. Ítems con `SoloSuperAdmin` (flag agregado en la reorg de 5 secciones).

**NO** filtra por los *permission claims* del usuario. Resultado: un usuario con
rol Comercial o Cobranzas **ve TODO el menú** (Bancos, Contabilidad, Inventario,
etc.), aunque no tenga permiso.

**No es hueco de seguridad**: el backend sí bloquea — cada endpoint tiene
`[ModuleAuthorize(...)]` y devuelve 403 al usuario sin permiso. Es un hueco de
**experiencia**: el menú no refleja el rol.

### Arreglo propuesto (a implementar y republicar)

- Agregar `RequiredPermission` (string, permiso claim requerido) a
  `SidebarNavItem` y/o `SidebarNavSection`. Null = siempre visible.
- En el filtro del sidebar: mostrar el ítem solo si
  `esSuperAdmin || user.HasClaim("permission", item.RequiredPermission)`.
  Aplicar recursivamente (un padre sin hijos visibles se oculta).
- Anotar el menú con el permiso base de cada opción de nivel superior:
  - Clientes → `module.ventas.clientes.view`
  - Caja → `module.ventas.caja.view`
  - Facturación (misc/notas) → `module.ventas.facturacion_miscelaneos.view`
  - Cobranza → `module.ventas.cobranza.view`
  - Órdenes y campo → `module.inventario.view` (ver Hallazgo 3)
  - Tarifario operativo → `module.ventas.clientes.view` (usa permiso de Clientes)
  - Informes → `module.reporteria.view`
  - Sección Bancos → `module.bancos.view`
  - Sección Contabilidad → `module.contabilidad.view`
  - Sección Inventario → `module.inventario.view`
  - Configuración: catálogos comerciales → ver Hallazgo 3; Sistema ya usa `SoloSuperAdmin`.
- Verificar con dxdocs cualquier API de DevExpress que se toque.

## Hallazgo 2: no hay "Restablecer contraseña" en el portal

La pantalla de usuarios (`UsuarioPortalForm`) solo pide contraseña al **crear**;
al **editar** cambia rol y empresa, no la clave. El
`UsuariosPortalController` (PUT) no toca `PasswordHash`.

Por eso los cambios de clave del ensayo se hicieron por SQL, generando el hash
ASP.NET Core Identity V3 a mano (PBKDF2-HMACSHA256, 100000 iter, salt 16B,
subkey 32B, marcador 0x01 → base64 empieza con `AQAAAA`). El app lo valida
porque el hash auto-describe su PRF e iteraciones.

### Arreglo propuesto

- Endpoint `POST usuarios/{id}/password` que llame a
  `UserManager.RemovePasswordAsync` + `AddPasswordAsync` (o
  `GeneratePasswordResetTokenAsync` + `ResetPasswordAsync`).
- Botón "Restablecer contraseña" en el formulario de editar usuario, restringido
  a Super Admin.

## Hallazgo 3: catálogos comerciales comparten permiso con Almacén

En los controllers, **Solicitudes, Ciclos, Libretas, Medidores** están
protegidos con `[ModuleAuthorize(PermissionModules.Inventario)]` — el MISMO
permiso que Artículos, Kardex, Requisiciones, Bodegas (Almacén). Órdenes también
usa Inventario.

Consecuencia: no se puede dar a Comercial la gestión de esos catálogos sin
abrirle también el Almacén. Por eso el rol Comercial quedó "limpio" (Opción A):
**sin** Solicitudes/Ciclos/Libretas/Medidores.

### Arreglo propuesto (Opción C)

- Reasignar esos catálogos comerciales a su propio permiso (p.ej.
  `PermissionResources.Ventas.*` nuevos, o un módulo `comercial`) para separarlos
  del Almacén. Registrar los permisos nuevos en `PermissionNames` y
  `PermissionEndpointCatalog`.
- Tras el cambio, sumar esos permisos al rol Comercial.

Nota menor: `MedidoresController` tiene un comentario `// ajusta si deseas
permitir anónimos` y `BarriosController` usa `module.configuracion` (no
inventario) — revisar consistencia de estos mapeos de paso.

## Hallazgo 4: cookie de permisos demasiado grande (HTTP 400 headers)

El Super Administrador tiene 208 permission claims; todos van en la cookie de
autenticación. En IIS eso puede pasar el límite de tamaño de headers →
"HTTP Error 400. The size of the request headers is too long."

**Mitigación aplicada en el ensayo** (server): subir límites de HTTP.sys
(`MaxRequestBytes`/`MaxFieldLength` a 65536 en el registro) + `iisreset`.

### Arreglo de fondo propuesto

- No serializar 200+ claims en la cookie. Opciones: claims compactos, o cargar
  permisos del servidor por sesión (claims transformation con caché) en vez de
  hornearlos todos en la cookie.

## Prioridad sugerida (para el próximo publish del portal)

1. **Filtrado del menú por permisos** (Hallazgo 1) — lo que hace que los roles se
   sientan reales.
2. **Restablecer contraseña** en el portal (Hallazgo 2) — deja de depender de SQL.
3. **Separar catálogos comerciales del Almacén** (Hallazgo 3) — completa el rol
   Comercial.
4. **Aliviar la cookie de permisos** (Hallazgo 4) — refactor, menos urgente
   (ya mitigado con el ajuste de IIS).

Todos van en un mismo publish del portal cuando se decida.
