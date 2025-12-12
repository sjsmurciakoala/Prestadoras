# 🔧 INSTRUCCIONES DE EJECUCIÓN - Migration Renombrado de Columnas

## 📋 Resumen
- **Migration:** `20251210_RenameConfigurationColumnsForConciseness`
- **Tabla afectada:** `con_configuracion_sistema`
- **Operación:** Renombrar 22 columnas (nombres más cortos)
- **Riesgo:** BAJO (no elimina datos)

---

## ✅ PASO A PASO

### 1️⃣ **HACER BACKUP (OBLIGATORIO)**

```powershell
# Navegar a carpeta del proyecto
cd D:\jesse\Documents\proyectos\HODSOFT_DEVEXPRESS\Prestadoras

# IMPORTANTE: Editar el archivo backup_bd.ps1 primero
# Cambiar la línea 15:
# $pgDatabase = "nombre_de_tu_base_de_datos"  
# Por el nombre real de tu BD

# Ejecutar backup
.\backup_bd.ps1
```

**¿Qué hace?**
- Crea archivo `.backup` en `Database\Backups\`
- Formato: `backup_antes_renombrar_columnas_YYYYMMDD_HHMMSS.backup`
- Te pedirá la contraseña de PostgreSQL

---

### 2️⃣ **EJECUTAR MIGRATION (Esperando tu confirmación)**

```powershell
# Desde la misma carpeta
.\ejecutar_migration.ps1
```

**¿Qué hace?**
- Verifica que exista backup reciente
- Te pide confirmación
- Ejecuta: `dotnet ef database update --project SIAD.Data`
- Renombra las 22 columnas en la BD

---

### 3️⃣ **VERIFICAR (Después de la migration)**

```powershell
# Iniciar la aplicación
cd apc
dotnet run
```

**Probar:**
1. Ir a `/contabilidad/empresas/configuracion`
2. Seleccionar una empresa
3. Verificar que carga correctamente
4. Hacer un cambio y guardar
5. Recargar y verificar que se guardó

---

## 🔄 REVERTIR (Si algo sale mal)

### Opción A: Rollback de migration
```powershell
cd apc
dotnet ef database update <nombre_migration_anterior> --project ..\SIAD.Data
```

### Opción B: Restaurar backup
```powershell
.\restore_bd.ps1
# O especificar archivo:
.\restore_bd.ps1 -BackupFile "ruta\al\backup.backup"
```

---

## 📝 Notas Importantes

1. **Antes de ejecutar en PRODUCCIÓN:**
   - Probar en BD de desarrollo primero
   - Verificar que todo funcione correctamente
   - Tener backup reciente

2. **El backup incluye:**
   - Estructura completa de la BD
   - Todos los datos
   - Índices y constraints

3. **La migration es reversible:**
   - Incluye método `Down()` completo
   - Puede revertirse sin pérdida de datos

---

## ⚠️ Checklist Pre-Ejecución

- [ ] Edité `backup_bd.ps1` con el nombre de mi BD
- [ ] Ejecuté `.\backup_bd.ps1` exitosamente
- [ ] Verifiqué que se creó el archivo .backup
- [ ] Cerré la aplicación (no debe estar corriendo)
- [ ] No hay otros usuarios conectados a la BD
- [ ] Tengo las credenciales de PostgreSQL

---

## 🆘 Soporte

Si algo sale mal:
1. **NO ENTRES EN PÁNICO**
2. Revisa el mensaje de error
3. Ejecuta `.\restore_bd.ps1` para volver al estado anterior
4. Documenta el error para análisis

---

**Estado Actual:** ⏸️ **ESPERANDO CONFIRMACIÓN PARA EJECUTAR**

Cuando hayas ejecutado el backup exitosamente, avísame y procederemos con la migration.
