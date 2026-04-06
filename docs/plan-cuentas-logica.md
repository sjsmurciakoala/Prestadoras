# Plan de Cuentas - Vista y logica (resumen tecnico)

## Archivos clave
- apc.Client/Pages/Contabilidad/PlanCuentas.razor
- apc.Client/Pages/Contabilidad/Components/PlanCuentaForm.razor
- SIAD.Services/Contabilidad/ContabilidadCatalogosService.cs
- apc.Client/Pages/Contabilidad/Empresas/ConfiguracionSistema.razor

## Flujo general (vista)
- PlanCuentas.razor carga configuracion de empresa (formato y separador) y luego el plan de cuentas via API.
- El grid muestra Codigo formateado (mascara + separador), pero el valor guardado es el codigo normalizado.
- La edicion/creacion se hace con PlanCuentaForm.razor, que valida y enviasa a la API.

## Mascara y separador (vista)
- PlanCuentas.razor define valores por defecto:
  - formatoCuentas = "###-###-##"
  - separadorCodigo = "-"
- Si en ConfiguracionSistema hay valores, se usan esos (modelo.Principal.FormatoCuentas y SeparadorCodigo).
- nivelesFormato se calcula con RecalcularNivelesFormato():
  1) ParseFormatoNiveles(formatoCuentas)
  2) Si no hay mascara valida, se infiere por niveles existentes (longitud mas comun por nivel, luego deltas).
- Formato de visualizacion:
  - FormatAccountCode() intenta formatear por jerarquia real (cadena de padres).
  - Si no puede, usa FormatAccountCodeFromMask().

## Normalizacion de codigo
- PlanCuentas.razor y PlanCuentaForm.razor usan NormalizeCode():
  - elimina separadores y deja solo letras y digitos.
- El valor guardado en DB es el codigo normalizado (sin separador).

## Como se determina la cuenta padre (vista)
- Si la cuenta tiene ParentAccountId, se usa esa cadena para formatear y validar.
- Si no tiene ParentAccountId, FindParentByPrefix() busca el mejor padre por prefijo de codigo:
  - toma el prefijo mas largo que coincide con el codigo normalizado.
- HasChildren() marca una cuenta como padre si tiene hijos por ParentAccountId.

## Como se determina la cuenta padre (backend)
- SavePlanCuentaAsync en ContabilidadCatalogosService.cs:
  - Si ParentAccountId viene, valida que exista y que el codigo del hijo inicie con el codigo del padre.
  - Si no viene y es cuenta nueva, intenta inferir por prefijo con FindParentByPrefixAsync().
  - Si no encuentra padre, el nivel queda en 1 y ParentAccountId = null.

## Validaciones principales
### Frontend (PlanCuentaForm.razor)
- Codigo obligatorio.
- Descripcion obligatoria.
- Si selecciona padre:
  - el codigo debe iniciar con el codigo del padre
  - el codigo debe ser mas largo que el del padre
- Sugiere siguiente codigo basado en hijos del padre (no obligatorio).

### Backend (SavePlanCuentaAsync)
- Codigo obligatorio y normalizado.
- Nombre obligatorio.
- Longitud maxima del codigo: 30.
- En alta: codigo debe ser unico por empresa.
- Si trae ParentAccountId:
  - valida existencia
  - valida prefijo y longitud
  - asigna parent_account_id y level = parent.level + 1
- Si no trae parent y es alta:
  - intenta inferir por prefijo; si no, level = 1
- En actualizacion: NO permite cambiar el codigo.

## Flags y significado (grid)
- M = AllowsPosting (cuenta de detalle / movimientos)
- T = AllowsThird
- C = AllowsCostCenter
- P = Tiene hijos (HasChildren)
- B = AllowsBudget
- MC = AllowsMultiCurrency

## Donde se define la mascara
- ConfiguracionSistema.razor:
  - modelo.Principal.FormatoCuentas
  - modelo.Principal.SeparadorCodigo

## Endpoints usados
- GET /api/contabilidad/catalogos/plan-cuentas
- POST /api/contabilidad/catalogos/plan-cuentas
- GET /api/contabilidad/configuracion/{companyId}

## Puntos a revisar si la mascara no aplica
- Confirmar que formatoCuentas y separadorCodigo estan cargados desde ConfiguracionSistema.
- Verificar nivelesFormato (si queda vacio, usa codigo normalizado sin separador).
- Revisar ParentAccountId en los registros (sin padre, formatea por mascara).

