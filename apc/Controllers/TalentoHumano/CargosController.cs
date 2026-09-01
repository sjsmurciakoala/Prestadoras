using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Services.TalentoHumano;
using apc.Security;

namespace apc.Controllers.TalentoHumano;

[ApiController]
[Route("api/talentohumano/cargos")]
[ModuleAuthorize(PermissionModules.TalentoHumano)]
public sealed class CargosController : CatalogoThControllerBase
{
    public CargosController(ICatalogoThService service) : base(service) { }
    protected override CatalogoTh Tipo => CatalogoTh.Cargo;
}
