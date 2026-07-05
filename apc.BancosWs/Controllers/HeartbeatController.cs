using apc.BancosWs.Contrato;
using Microsoft.AspNetCore.Mvc;

namespace apc.BancosWs.Controllers;

/// <summary>GET /simafi/api/heartbeat → 200 "ok ok" (el banco lo monitorea).</summary>
[ApiController]
public sealed class HeartbeatController : ControllerBase
{
    [HttpGet("simafi/api/heartbeat")]
    public IActionResult Get() => Content(ContractXml.HeartbeatBody, "text/plain");
}
