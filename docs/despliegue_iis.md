# Guía de Despliegue en IIS - SIAD Blazor

## Problema Resuelto
- **Error**: `UriFormatException` en producción IIS
- **Síntoma**: Navegación no funciona (URL cambia pero vista no actualiza)
- **Causa**: WebSockets no configurado + URI base incorrecta

## Cambios Aplicados

### 1. Program.cs - Corrección del URI Base
Línea 61 corregida para manejar fallback cuando `HttpContext` es null:
```csharp
// Antes (causaba el error):
client.BaseAddress = new Uri("/");

// Después (correcto):
var baseUrl = builder.Configuration["BaseUrl"] ?? "http://localhost:5000/";
client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
```

### 2. appsettings.json - BaseUrl para producción
Agregado parámetro `BaseUrl`:
```json
{
  "BaseUrl": "http://172.16.0.9/",
  ...
}
```

### 3. Program.cs - Middleware WebSockets
Agregado `app.UseWebSockets()` antes de `UseHttpsRedirection()`.

### 4. web.config - Configuración IIS
Creado archivo con:
- Habilitar WebSockets: `<webSocket enabled="true" />`
- MIME types para archivos WASM/Blazor
- Compresión deshabilitada para `.wasm`
- Límites de request aumentados

## Pasos para Publicar en IIS

### 1. Compilar en Modo Release
```powershell
cd D:\jesse\Documents\proyectos\HODSOFT_DEVEXPRESS\Prestadoras
dotnet publish apc/apc.csproj -c Release -o ./publish
```

### 2. Copiar archivos al servidor
Copiar toda la carpeta `./publish` al servidor IIS (por ejemplo `C:\inetpub\wwwroot\siad`)

### 3. Configurar IIS

#### a. Habilitar WebSockets en Windows Server
```powershell
# Ejecutar como Administrador en el servidor
Install-WindowsFeature -name Web-WebSockets
```

O manualmente:
1. Server Manager → Add Roles and Features
2. Server Roles → Web Server (IIS) → Web Server → Application Development
3. ✅ **WebSocket Protocol**

#### b. Configurar Application Pool
1. Abrir IIS Manager
2. Application Pools → Crear/Seleccionar pool
3. Configuración:
   - **.NET CLR Version**: No Managed Code
   - **Managed Pipeline Mode**: Integrated
   - **Identity**: ApplicationPoolIdentity (o cuenta con permisos DB)

#### c. Configurar Sitio/Aplicación
1. Sites → Default Web Site → Add Application (o crear nuevo site)
2. Alias: `siad` (o raíz del sitio)
3. Application Pool: el creado arriba
4. Physical Path: `C:\inetpub\wwwroot\siad`

#### d. Verificar permisos
El Application Pool Identity debe tener:
- Lectura en carpeta física
- Conexión a base de datos PostgreSQL (verificar connection string en `appsettings.json`)

### 4. Actualizar appsettings.json en producción
En el servidor, editar `appsettings.json`:
```json
{
  "BaseUrl": "http://172.16.0.9/",  // O https://dominio.com/
  "ConnectionStrings": {
    "DefaultConnection": "Host=172.16.0.9;Port=5432;Database=siad_v2;Username=postgres;Password=***;Timeout=10;SslMode=Prefer"
  }
}
```

**IMPORTANTE**: Cambiar password de producción.

### 5. Verificar web.config
Asegurar que existe `web.config` en la raíz publicada con:
```xml
<webSocket enabled="true" />
```

### 6. Reiniciar Application Pool
```powershell
# En el servidor
Restart-WebAppPool -Name "NombreDelPool"
```

O en IIS Manager: Application Pools → Recycle

### 7. Probar

1. Navegar a `http://172.16.0.9/` (o la URL configurada)
2. Verificar en consola del navegador (F12):
   - ✅ No debe aparecer `UriFormatException`
   - ✅ WebSockets conectado (o Long Polling sin errores 405)
   - ✅ Navegación funciona sin necesidad de recargar

## Solución de Problemas

### Error 405 en /_blazor
**Causa**: WebSockets no habilitado en IIS
**Solución**: Ejecutar `Install-WindowsFeature -name Web-WebSockets` y reiniciar

### Navegación no actualiza vista
**Causa**: Circuit de Blazor Server desconectado
**Solución**: Verificar que `app.UseWebSockets()` esté en `Program.cs` antes de `UseHttpsRedirection()`

### Error al conectar a base de datos
**Causa**: Application Pool Identity sin permisos
**Solución**: 
1. Verificar connection string
2. Asegurar que el servidor tiene acceso a PostgreSQL (firewall, pg_hba.conf)
3. Considerar usar cuenta de servicio específica en Application Pool

### Archivos .wasm no se descargan
**Causa**: MIME types no configurados
**Solución**: El `web.config` ya incluye los MIME types. Verificar que se copió correctamente.

## Checklist de Despliegue

- [ ] Ejecutar `dotnet publish -c Release`
- [ ] Copiar archivos a servidor
- [ ] Verificar `web.config` presente
- [ ] WebSockets instalado en IIS
- [ ] Application Pool configurado (No Managed Code)
- [ ] `appsettings.json` actualizado con `BaseUrl` correcta
- [ ] Connection string de producción configurada
- [ ] Permisos de carpeta correctos
- [ ] Recycle Application Pool
- [ ] Probar navegación sin recargar

## Contacto
Para problemas adicionales, revisar logs en:
- IIS Logs: `C:\inetpub\logs\LogFiles\W3SVC1\`
- Stdout logs (configurados en web.config): `.\logs\stdout*.log`

---
**Última actualización**: 18 de diciembre de 2025
