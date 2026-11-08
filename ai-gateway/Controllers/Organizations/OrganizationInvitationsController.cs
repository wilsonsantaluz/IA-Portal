using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Organizations;

[ApiController,Authorize,RequestSizeLimit(4096),Route("api/organization-invitations")]
public sealed class OrganizationInvitationsController(OrganizationInvitationService service):ControllerBase
{
    // Codes belong in POST bodies, not request paths/query strings or access logs.
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(InvitationTokenRequest request,CancellationToken ct)
        =>Ok(await service.Preview(PortalAccess.UserId(User),request.Token,ct));
    [HttpPost("accept")]
    public async Task<IActionResult> Accept(InvitationTokenRequest request,CancellationToken ct)
        =>Ok(await service.Accept(PortalAccess.UserId(User),request.Token,ct));

    [HttpGet("/api/organizations/current/invitations"),Authorize(Policy=PortalAccess.Administration)]
    public async Task<IActionResult> List(CancellationToken ct)=>Ok(await service.List(PortalAccess.UserId(User),ct));
    [HttpPost("/api/organizations/current/invitations"),Authorize(Policy=PortalAccess.Administration)]
    public async Task<IActionResult> Create(InvitationCreateRequest request,CancellationToken ct)
        =>Created("/api/organizations/current/invitations",await service.Create(PortalAccess.UserId(User),request.Role,ct));
    [HttpPost("/api/organizations/current/invitations/{id:guid}/revoke"),Authorize(Policy=PortalAccess.Administration)]
    public async Task<IActionResult> Revoke(Guid id,InvitationRevokeRequest request,CancellationToken ct){
        await service.Revoke(PortalAccess.UserId(User),id,request.Version,ct);return NoContent();
    }
}
