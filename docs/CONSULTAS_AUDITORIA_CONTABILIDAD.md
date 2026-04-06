# 🔍 CONSULTAS SQL - AUDITORÍA DE TRANSACCIONES CONTABLES

**Referencia**: Para revisar quién hizo qué en el módulo de contabilidad

---

## 🔎 CONSULTAS ÚTILES

### 1. **Todas las pólizas creadas por un usuario**

```sql
SELECT 
    poliza_id,
    poliza_number,
    poliza_date,
    status,
    description,
    created_by,
    created_at,
    updated_by,
    updated_at
FROM public.con_partida_hdr
WHERE created_by = 'JUAN_PEREZ'
ORDER BY created_at DESC;
```

---

### 2. **Pólizas registradas hoy con quién las registró**

```sql
SELECT 
    poliza_id,
    poliza_number,
    poliza_date,
    created_by AS "Creada por",
    created_at AS "Fecha creación",
    posted_by AS "Registrada por",
    posted_at AS "Fecha registro",
    status
FROM public.con_partida_hdr
WHERE DATE(posted_at AT TIME ZONE 'UTC') = CURRENT_DATE
AND status = 1;
```

---

### 3. **Historial completo de cambios de una póliza**

```sql
SELECT 
    poliza_id,
    'CREADA' AS Acción,
    created_by AS "Usuario",
    created_at AS "Fecha/Hora"
FROM public.con_partida_hdr
WHERE poliza_id = 123

UNION ALL

SELECT 
    poliza_id,
    'ACTUALIZADA' AS Acción,
    updated_by AS "Usuario",
    updated_at AS "Fecha/Hora"
FROM public.con_partida_hdr
WHERE poliza_id = 123 AND updated_by IS NOT NULL

UNION ALL

SELECT 
    poliza_id,
    'REGISTRADA' AS Acción,
    CAST(posted_by AS VARCHAR) AS "Usuario",
    posted_at AS "Fecha/Hora"
FROM public.con_partida_hdr
WHERE poliza_id = 123 AND posted_by IS NOT NULL

ORDER BY "Fecha/Hora" DESC;
```

---

### 4. **Todas las operaciones de un usuario en una empresa**

```sql
SELECT 
    poliza_id,
    poliza_number,
    poliza_date,
    'Creada' AS Acción,
    created_at,
    created_by
FROM public.con_partida_hdr
WHERE company_id = 5 AND created_by = 'LAURA_GOMEZ'

UNION ALL

SELECT 
    poliza_id,
    poliza_number,
    poliza_date,
    'Actualizada' AS Acción,
    updated_at,
    updated_by
FROM public.con_partida_hdr
WHERE company_id = 5 AND updated_by = 'LAURA_GOMEZ'

UNION ALL

SELECT 
    poliza_id,
    poliza_number,
    poliza_date,
    'Registrada' AS Acción,
    posted_at,
    CAST(posted_by AS VARCHAR)
FROM public.con_partida_hdr
WHERE company_id = 5 AND posted_by IS NOT NULL

ORDER BY Acción DESC;
```

---

### 5. **Cambios de Plan de Cuentas (últimas 100)**

```sql
SELECT 
    account_id,
    code,
    name,
    created_by,
    created_at,
    updated_by,
    updated_at,
    status
FROM public.con_plan_cuentas
ORDER BY 
    CASE 
        WHEN updated_at IS NOT NULL THEN updated_at
        ELSE created_at
    END DESC
LIMIT 100;
```

---

### 6. **Auditoría de Tipos de Transacción**

```sql
SELECT 
    type_id,
    code,
    name,
    created_by,
    created_at,
    updated_by,
    updated_at,
    status
FROM public.con_tipo_transaccion
WHERE company_id = 5
ORDER BY updated_at DESC NULLS LAST;
```

---

### 7. **Resumen de actividad por usuario (últimos 7 días)**

```sql
SELECT 
    created_by AS "Usuario",
    COUNT(*) AS "Pólizas creadas",
    MAX(created_at) AS "Última creación"
FROM public.con_partida_hdr
WHERE company_id = 5 
  AND created_at >= NOW() - INTERVAL '7 days'
GROUP BY created_by
ORDER BY COUNT(*) DESC;
```

---

### 8. **Pólizas en estado DRAFT sin actualizaciones recientes**

```sql
SELECT 
    poliza_id,
    poliza_number,
    poliza_date,
    created_by,
    created_at,
    updated_at,
    EXTRACT(DAY FROM NOW() - COALESCE(updated_at, created_at)) AS "Días sin cambios"
FROM public.con_partida_hdr
WHERE company_id = 5 
  AND status = 0  -- DRAFT
  AND COALESCE(updated_at, created_at) < NOW() - INTERVAL '3 days'
ORDER BY created_at;
```

---

### 9. **Centros de Costo - Auditoría completa**

```sql
SELECT 
    cost_center_id,
    code,
    name,
    created_by,
    created_at,
    updated_by,
    updated_at,
    status
FROM public.con_centro_costo
WHERE company_id = 5
ORDER BY 
    CASE 
        WHEN updated_at IS NOT NULL THEN updated_at
        ELSE created_at
    END DESC;
```

---

### 10. **Configuración del sistema - Quién la modificó**

```sql
SELECT 
    configuracion_id,
    company_id,
    created_by,
    created_at,
    updated_by,
    updated_at,
    CASE 
        WHEN updated_at IS NOT NULL THEN updated_by
        ELSE created_by
    END AS "Última modificación por"
FROM public.con_configuracion_sistema
ORDER BY COALESCE(updated_at, created_at) DESC;
```

---

## 📊 REPORTES FRECUENTES

### **Reporte: Actividad de Auditoría - Últimos 30 días**

```sql
WITH polizas_changes AS (
    SELECT 
        'con_partida_hdr' AS tabla,
        poliza_id::TEXT AS id_registro,
        created_by AS usuario,
        created_at AS fecha,
        'CREATE' AS operacion,
        company_id
    FROM public.con_partida_hdr
    WHERE created_at >= NOW() - INTERVAL '30 days'
    
    UNION ALL
    
    SELECT 
        'con_partida_hdr' AS tabla,
        poliza_id::TEXT AS id_registro,
        updated_by AS usuario,
        updated_at AS fecha,
        'UPDATE' AS operacion,
        company_id
    FROM public.con_partida_hdr
    WHERE updated_at IS NOT NULL 
      AND updated_at >= NOW() - INTERVAL '30 days'
)
SELECT 
    tabla,
    id_registro,
    usuario,
    fecha,
    operacion,
    EXTRACT(DAY FROM NOW() - fecha)::INT AS "Hace X días",
    company_id
FROM polizas_changes
ORDER BY fecha DESC;
```

---

## 🚨 ALERTAS Y VALIDACIONES

### **Detectar cambios sospechosos (múltiples cambios en corto tiempo)**

```sql
SELECT 
    poliza_id,
    poliza_number,
    created_by,
    updated_by,
    created_at,
    updated_at,
    EXTRACT(MINUTE FROM (updated_at - created_at)) AS "Minutos entre creación y última actualización",
    status
FROM public.con_partida_hdr
WHERE company_id = 5
  AND updated_at IS NOT NULL
  AND (updated_at - created_at) < INTERVAL '1 minute'
ORDER BY created_at DESC;
```

**Nota:** Esto podría indicar correcciones rápidas o posibles inconsistencias.

---

### **Verificar integridad: Pólizas registradas sin usuario de posting**

```sql
SELECT 
    poliza_id,
    poliza_number,
    status,
    posted_at,
    posted_by,
    created_by,
    created_at
FROM public.con_partida_hdr
WHERE company_id = 5
  AND status = 1  -- POSTED
  AND posted_by IS NULL
ORDER BY posted_at DESC;
```

**Acción:** Si hay registros aquí, hay inconsistencia en los datos.

---

## 📌 NOTAS IMPORTANTES

1. **Timestamps en UTC**: Todos los `_at` campos están en `TIMESTAMP WITH TIME ZONE` (UTC)
2. **Usuarios**: Almacenados como `VARCHAR` (email, identificador único)
3. **posted_by**: Es `BIGINT` que referencia el `user_id` real (no es VARCHAR como los otros `_by`)
4. **Multiempresa**: Siempre filtrar por `company_id` para aislar datos por empresa

---

## 🔐 MEJORES PRÁCTICAS

✅ **DO:**
- Filtrar siempre por `company_id`
- Usar `TIMESTAMP WITH TIME ZONE` para comparaciones
- Mantener logs de quién accede a estos datos

❌ **DON'T:**
- Exponer `posted_by` user IDs directamente en UI (usar nombre de usuario)
- Asumir que `updated_at` es NULL significa que nunca se actualizó (puede ser NULL si nunca fue editado después de crear)
- Modificar directamente estos campos sin pasar por la aplicación


