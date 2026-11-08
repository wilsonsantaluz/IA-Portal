using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Organizations;

[ApiController, Authorize, Route("api/organizations")]
public sealed class OrganizationsController(PortalDbContext db, OrganizationWorkspaceService workspace) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = PortalAccess.UserId(User);
        return Ok(await db.Organizations.Where(o => o.IsActive && db.OrganizationMemberships.Any(m =>
            m.OrganizationId == o.Id && m.UserId == userId && m.IsActive))
            .OrderBy(o => o.Id).Select(o => new { o.Id, o.Name, o.Slug,
                policyRole=db.OrganizationMemberships.Where(m=>m.OrganizationId==o.Id && m.UserId==userId).Select(m=>m.PolicyRole).FirstOrDefault() }).ToListAsync(ct));
    }

    [HttpPost, Authorize(Policy=PortalAccess.CreateOrganization)]
    public async Task<IActionResult> Create(CreateOrganizationRequest request,CancellationToken ct)
        => Created("/api/organizations",await workspace.Create(PortalAccess.UserId(User),request,ct));

    [HttpGet("current/members"), Authorize(Policy=PortalAccess.Administration)]
    public async Task<IActionResult> Members(CancellationToken ct) => Ok(await db.OrganizationMemberships
        .Where(m=>m.OrganizationId==db.OrganizationId).Join(db.Users,m=>m.UserId,u=>u.UserId,(m,u)=>new {
            u.UserId,u.UserName,m.PolicyRole,m.IsActive,m.Version,accountActive=u.IsActive
        }).OrderBy(x=>x.UserName).ToListAsync(ct));

    [HttpPut("current/members/{userId:int}"), Authorize(Policy=PortalAccess.Administration)]
    public async Task<IActionResult> Member(int userId,MembershipPolicyRequest request,CancellationToken ct) {
        await workspace.ChangeMember(PortalAccess.UserId(User),userId,request,ct);return NoContent();
    }

    [HttpGet("current/ai-default")]
    public async Task<IActionResult> Default(CancellationToken ct) => Ok(await workspace.Default(ct));

    [HttpPost("current/transfer-ownership"),Authorize(Policy=PortalAccess.Administration)]
    public async Task<IActionResult> Transfer(OwnershipTransferRequest request,CancellationToken ct){
        await workspace.TransferOwnership(PortalAccess.UserId(User),request,ct);return NoContent();
    }

    [HttpPut("current/ai-default"), Authorize(Policy=PortalAccess.Organizations)]
    public async Task<IActionResult> Default(AiGateway.Dtos.DefaultUpdate request,CancellationToken ct) {
        await workspace.SetDefault(PortalAccess.UserId(User),request,ct);return Ok(await workspace.Default(ct));
    }
}
