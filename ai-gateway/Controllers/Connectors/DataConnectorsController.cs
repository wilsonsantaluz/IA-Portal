using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Infrastructure.Trino;

namespace AiGateway.Controllers.Connectors;

[ApiController, Authorize(Policy = PortalAccess.Connectors), Route("api/data-connectors")]
public sealed class DataConnectorsController(DataConnectorService service) : ControllerBase
{
    [HttpGet("groups")] public async Task<IActionResult> Groups(CancellationToken ct) => Ok(await service.Groups(ct));
    [HttpGet] public async Task<IActionResult> List(CancellationToken ct) => Ok(await service.List(true,ct));
    [HttpGet("profiles")] public async Task<IActionResult> Profiles(CancellationToken ct) => Ok(await service.Profiles(ct));
    [HttpPost] public async Task<IActionResult> Create(ConnectorUpdate request, CancellationToken ct) => Ok(new { id = await service.Save(null,request,ct) });
    [HttpPut("{id:int}")] public async Task<IActionResult> Save(int id, ConnectorUpdate request, CancellationToken ct) => Ok(new { id = await service.Save(id,request,ct) });
    [HttpPost("{id:int}/test")] public async Task<IActionResult> Test(int id, CancellationToken ct) => Ok(await service.Test(id,ct));
    [HttpGet("{id:int}/metadata")] public async Task<IActionResult> Metadata(int id, CancellationToken ct) => Ok(await service.Discover(id,ct));
}

[ApiController, Authorize(Policy = PortalAccess.DataQuery), Route("api/data")]
public sealed class DataQueriesController(DataConnectorService service) : ControllerBase
{
    [HttpGet("sources")] public async Task<IActionResult> Sources(CancellationToken ct) => Ok(await service.List(false,ct));
    [HttpGet("{id:int}/analytics")] public async Task<IActionResult> Analytics(int id, [FromQuery] string kind, [FromQuery] DateOnly reference, CancellationToken ct)
        => Ok(await service.Analytics(id,kind,reference,ct));
}
