using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.TalentoHumano;
using apc.Security;

namespace apc.Controllers.TalentoHumano;

[ApiController]
[Route("api/talentohumano/departamentos")]
[ModuleAuthorize(PermissionModules.TalentoHumano)]
public sealed class DepartamentosController : CatalogoThControllerBase
{
    public DepartamentosController(ICatalogoThService service) : base(service) { }
    protected override CatalogoTh Tipo => CatalogoTh.Departamento;
}
