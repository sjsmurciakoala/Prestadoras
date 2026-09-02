# Runbook de despliegue a SRV — unificación de autorización (sep 2026)

**Base destino:** `siad_v4` @ `172.16.0.9`
**Preparado:** 2026-09-01
**Scripts:** 2 · **Requiere desplegar el binario del portal en la misma ventana**

---

## 1. Qué cubre

La autorización del portal tenía **dos caminos en paralelo**: policies por rol
(`CanContabilidad`, `CanBancos`, …) y permisos por claim (`module.*`). Se retiró el primero:
ahora toda decisión se toma por permiso, y el único rol con significado en el código es
**Super Administrador**, que actúa como bypass global.

Como consecuencia, los roles pasaron a ser **contenedores de permisos** y hay que poblarlos:
siete de ellos estaban vacíos y sus usuarios se quedarían sin acceso.

| # | Script | Qué hace |
|---|---|---|
| 1 | `2026-09-01_permisos_por_rol.sql` | Asigna permisos a Admin, Super Administrador, Contabilidad, Bancos, Compras, Configuracion, Presupuesto y Compromisos |
| 2 | `2026-09-01_usuario_facturacion_rol_ventas.sql` | Mueve `aguasdepuertocortesfacturacion@gmail.com` del rol `User` (vacío) al rol `Ventas` |

Ambos son **idempotentes** y solo agregan; no quitan permisos existentes.

---

## 2. ⚠️ El orden importa: binario primero, SQL después

**No apliques el SQL con el binario viejo.** El binario anterior mete todos los permisos de
todos los roles dentro de la cookie de sesión. Al darle 141 permisos al rol Admin, un usuario
con varios roles llega a una cookie de **33 KB** y el servidor responde **HTTP 431
(Request Header Fields Too Large)**: no puede iniciar sesión. Se comprobó en local.

El binario nuevo saca los permisos de la cookie (los resuelve por petición desde los roles,
con caché), y la cookie baja a **~1,1 KB**.

```
1. Backup del SRV                    (Database/backup_bd_simple.ps1)
2. Publicar el portal                (./publish-onprem.ps1 -Solo portal)
3. Aplicar los 2 scripts SQL
4. Verificar (sección 4)
```

Entre los pasos 2 y 3 hay una ventana breve en la que los usuarios que **no** son Super
Administrador pierden acceso: el binario nuevo ya exige permisos y los roles aún no los tienen.
Hazlo seguido y en horario de bajo uso.

---

## 3. Aplicar

### Estado: parcialmente aplicado el 2026-09-02

Se adelantó lo que **no** arriesga nada con el binario viejo todavía desplegado, para poder
probar el filtrado por rol antes del despliegue:

| Rol | Estado |
|---|---|
| Contabilidad (8), Bancos (6), Compras (15), Configuracion (5), Presupuesto (4), Compromisos (5) | ✅ aplicado 2026-09-02 |
| `aguasdepuertocortesfacturacion@gmail.com`: `User` → `Ventas` | ✅ aplicado 2026-09-02 |
| **Admin (234)** y **Super Administrador (234)** | ⏳ **pendientes hasta desplegar el binario** |

Los dos pendientes son los únicos con el catálogo completo. Con el binario viejo, sus permisos
viajan dentro de la cookie y la llevan por encima del límite de 32 KB de Kestrel → **HTTP 431**.
No cuesta nada esperar: los tres usuarios con esos roles tienen además Super Administrador, que
salta toda comprobación de permiso.

Respaldo previo de las 194 filas: `scratchpad/respaldo_roles_siad_v4.txt`.

### Al desplegar, completar

```bash
export SRV="postgresql://USUARIO:CLAVE@172.16.0.9:5432/siad_v4"

# Idempotente: vuelve a pasar por los seis roles ya aplicados sin duplicar nada,
# y agrega Admin y Super Administrador, que es lo que falta.
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-01_permisos_por_rol.sql
psql "$SRV" -v ON_ERROR_STOP=1 -f Database/2026-09-01_usuario_facturacion_rol_ventas.sql
```

> En PowerShell, `"$SRV"` es `"$env:SRV"`. Cada script trae su propio `BEGIN … COMMIT`.

**¿Ya aplicado?**

```sql
SELECT r."Name", count(c."Id") AS permisos
FROM identity."AspNetRoles" r
LEFT JOIN identity."AspNetRoleClaims" c
       ON c."RoleId" = r."Id" AND c."ClaimType" = 'permission'
GROUP BY 1 ORDER BY 2 DESC;
```

Tras lo aplicado el 2026-09-02, `Contabilidad`, `Bancos`, `Compras`, `Configuracion`,
`Presupuesto` y `Compromisos` ya NO están en 0. Lo que falta se reconoce porque **`Admin`
sigue en 0** y `Super Administrador` no llega al total del catálogo.

---

## 4. Verificar después de aplicar

- [ ] **Entrar como Super Administrador** (`srivera@koalaoutsourcing.com`). Debe ver el menú completo.
- [ ] **Entrar con un usuario de rol acotado** (p. ej. `contabilidad@aguasdepuestocortes.com`):
      el menú debe mostrar Contabilidad, Bancos e Informes, y **no** Ventas ni Inventario.
- [ ] Entrar a una ruta sin permiso escribiendo la URL a mano: debe salir **«No tienes acceso a
      esta opción»**, no la pantalla de login.
- [ ] Confirmar que nadie queda con el menú vacío:

```sql
SELECT u."Email", count(rc."Id") AS permisos
FROM identity."AspNetUsers" u
LEFT JOIN identity."AspNetUserRoles" ur ON ur."UserId" = u."Id"
LEFT JOIN identity."AspNetRoleClaims" rc
       ON rc."RoleId" = ur."RoleId" AND rc."ClaimType" = 'permission'
GROUP BY 1 ORDER BY 2;
```

---

## 5. Rollback

Los scripts solo insertan, así que revertir es borrar lo insertado:

```sql
-- deshace el paso 1 (deja los roles como estaban: vacíos)
DELETE FROM identity."AspNetRoleClaims" c
USING identity."AspNetRoles" r
WHERE c."RoleId" = r."Id"
  AND c."ClaimType" = 'permission'
  AND r."Name" IN ('Admin','Contabilidad','Bancos','Compras',
                   'Configuracion','Presupuesto','Compromisos');
```

⚠️ Revertir el SQL **sin** revertir también el binario deja sin acceso a todos los usuarios que
no sean Super Administrador. Si hay que volver atrás, vuelve atrás con las dos cosas.
