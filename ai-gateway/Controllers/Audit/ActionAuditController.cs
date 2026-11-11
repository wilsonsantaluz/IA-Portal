using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Audit;

public sealed record AuditSearch(DateTimeOffset? From=null,DateTimeOffset? To=null,string? Q=null,
    int? UserId=null,string? Outcome=null,long? BeforeId=null,int Limit=50);

[ApiController,Route("api/audit/actions"),Authorize(Policy=PortalAccess.AuditRead)]
public sealed class ActionAuditController(PortalDbContext db,TimeProvider clock) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] AuditSearch search,CancellationToken ct)
    {
        var to=(search.To ?? clock.GetUtcNow()).UtcDateTime;
        var from=(search.From ?? new DateTimeOffset(to,TimeSpan.Zero).AddDays(-7)).UtcDateTime;
        if(from>to || to-from>TimeSpan.FromDays(366) || search.Limit is <1 or >100
            || search.BeforeId is <=0 || search.UserId is <=0 || search.Q?.Length>160
            || search.Outcome is not (null or "" or "success" or "failure" or "denied" or "cancelled"))
            return BadRequest(new{error="Informe período de até 366 dias, filtros válidos e limite de 1 a 100."});
        // Organization 1 owns installation administration. Other admins remain tenant-bound.
        var installation=db.OrganizationId==1;
        var query=installation ? db.ActionAuditEvents.IgnoreQueryFilters().AsNoTracking() : db.ActionAuditEvents.AsNoTracking();
        query=query.Where(x=>x.CreatedAt>=from && x.CreatedAt<=to);
        if(search.BeforeId.HasValue)query=query.Where(x=>x.Id<search.BeforeId.Value);
        if(search.UserId.HasValue)query=query.Where(x=>x.ActorUserId==search.UserId);
        if(!string.IsNullOrEmpty(search.Outcome))query=query.Where(x=>x.Outcome==search.Outcome);
        if(!string.IsNullOrWhiteSpace(search.Q)) {
            var q=search.Q.Trim().ToLowerInvariant();query=query.Where(x=>x.Action.ToLower().Contains(q) || x.Operation.ToLower().Contains(q) || x.Area.ToLower().Contains(q)
                || x.Route.ToLower().Contains(q) || x.RequestId.ToLower().Contains(q) || x.Method.ToLower().Contains(q)
                || (x.RemoteAddress!=null && x.RemoteAddress.ToLower().Contains(q))
                || (x.ActorName!=null && x.ActorName.ToLower().Contains(q)) || (x.TargetId!=null && x.TargetId.ToLower().Contains(q)));
        }
        var rows=await query.OrderByDescending(x=>x.Id).Take(search.Limit+1).ToListAsync(ct);
        var more=rows.Count>search.Limit;var items=rows.Take(search.Limit).ToArray();
        return Ok(new{items,nextBeforeId=more ? items[^1].Id : (long?)null,scope=installation ? "installation" : "organization"});
    }
}
