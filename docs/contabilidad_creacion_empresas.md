# Plan de trabajo: alta de empresas en Contabilidad

## Objetivo
Agregar en el módulo de Contabilidad una funcionalidad para crear empresas con los campos requeridos usando Blazor WebAssembly + ASP.NET Core + DevExpress 25.1.

## Tareas concretas

1. **Modelo de datos y DTO**
   - Crear/actualizar `CompanyCreationDto` con los campos obligatorios:
     - Código, Descripción, Tipo de empresa, ID fiscal (siglas), ID fiscal (valor), Tamaño (Pequeña, Mediana, Gran contribuyente), Capital (Privado, Oficial, Mixto), Fecha constitución, Activa (bool), Dirección (tab Contacto), Teléfonos, País, Email, Pag.Web.
   - Añadir validaciones `[Required]`, `[StringLength]`, `[EmailAddress]`, `[Url]`, enumeraciones para Tamaño y Capital.

2. **Endpoint API**
   - Crear controlador `ContabilidadEmpresaController` en `apc/Controllers/Contabilidad/` con `POST api/contabilidad/empresas`.
   - Inyectar `CurrentCompanyService` para obtener el tenant y validar permisos.
   - Retornar `201 Created` con el DTO guardado y mensajes de validación en caso de error.

3. **Servicio de dominio**
   - En `SIAD.Services/Contabilidad`, implementar `ICompanyManagementService` con `Task<CompanyCreationDto> CrearAsync(long companyId, CompanyCreationDto dto)`.
   - Mapear `dto` a entidad `cfg_company`/`con_empresa_configuracion` según el modelo; aplicar limpieza de cadenas y auditoría (`created_by`, `created_at`).

4. **Persistencia y migraciones**
   - Revisar si `cfg_company` ya contiene los campos; si falta alguno (tamaño, capital, fecha constitución, activa), crear migración que agregue columnas.
   - Si se requiere tabla específica para la configuración contable, extender `con_empresa_configuracion` con los campos de contacto (dirección, teléfonos, país, email, web) y agregar migración/DDL.

5. **Cliente Blazor (DevExpress 25.1)**
   - Añadir página `Pages/Contabilidad/Empresas/NuevaEmpresa.razor` con formulario DevExpress (`DxFormLayout`, `DxTextBox`, `DxComboBox`, `DxDateEdit`, `DxCheckBox`).
   - Usar `EditForm` + `DataAnnotationsValidator` enlazando al DTO; mostrar resumen de validación y `DxButton` para Guardar/Cancelar.
   - Consumir el nuevo endpoint con un cliente `EmpresaClient` en `apc.Client/Services/Contabilidad/`.

6. **Autorización y tenant**
   - Reutilizar el claim `tenant_company` para asociar la empresa creada al tenant activo.
   - Validar que sólo roles autorizados (p.ej., `AdminContabilidad`) puedan crear empresas.

7. **UI/UX adicionales**
   - Añadir entrada de menú en `NavMenu.razor` bajo Contabilidad → “Crear empresa”.
   - Mostrar `DxMessageBox` o `DxToast` con el resultado de la creación.

8. **Pruebas**
   - Pruebas unitarias del servicio (`CompanyManagementService`) verificando validaciones y guardado de `company_id`.
   - Pruebas de integración del endpoint `POST api/contabilidad/empresas` usando `WebApplicationFactory`.
   - Prueba manual en la SPA: completar formulario y confirmar creación en la base.

## Campos solicitados (referencia rápida)
- Código* (string)
- Descripción* (string)
- Tipo de empresa* (enum/lista)
- ID fiscal (siglas)* (string)
- ID fiscal (valor)* (string)
- Tamaño* (Pequeña, Mediana, Gran contribuyente)
- Capital* (Privado, Oficial, Mixto)
- Fecha constitución* (date)
- Activa* (bool)
- Contacto/Dirección (string)
- Teléfonos (string)
- País (string)
- E-mail (string)
- Pag.Web (url)
