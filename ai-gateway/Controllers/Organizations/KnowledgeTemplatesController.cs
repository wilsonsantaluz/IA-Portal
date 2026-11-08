using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;
using System.Text.Json;

namespace AiGateway.Controllers.Organizations;

public sealed record KnowledgeTemplate(int SchemaVersion,string Id,string Version,string Name,string Description,
    string Category,string[] Instructions,string[] AllowedTools,string[] ExampleQuestions,UpsertKbItemRequest[] Documents);

[ApiController, Authorize(Policy=PortalAccess.Knowledge), Route("api/knowledge-templates")]
public sealed class KnowledgeTemplatesController(PortalDbContext db,IKnowledgeBaseService knowledge,
    ILogger<KnowledgeTemplatesController> logger):ControllerBase
{
    private static async Task<KnowledgeTemplate> Template(CancellationToken ct) {
        using var stream=typeof(KnowledgeTemplatesController).Assembly.GetManifestResourceStream("AiGateway.Application.Templates.technical-support.v1.json")!;
        return (await JsonSerializer.DeserializeAsync<KnowledgeTemplate>(stream,new JsonSerializerOptions{PropertyNameCaseInsensitive=true},ct))!;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(new[]{await Template(ct)});

    [HttpPost("{id}/apply"), Authorize(Policy=PortalAccess.EditKnowledge)]
    public async Task<IActionResult> Apply(string id,CancellationToken ct) {
        var template=await Template(ct);
        if(id!=template.Id)return NotFound();
        var imported=0;var skipped=0;
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        foreach(var document in template.Documents) {
            // Never overwrite a matching user-edited/inactive document, even on reapply.
            if(await db.KnowledgeBaseItems.AnyAsync(k=>k.KeyName==document.KeyName,ct)){skipped++;continue;}
            document.Id=null;document.Subsection=$"template / {template.Id} / {template.Version}";
            await knowledge.UpsertAsync(document,ct);imported++;
        }
        await tx.CommitAsync(ct);
        logger.LogInformation("KnowledgeTemplateApplied Organization={Organization} Actor={Actor} Template={Template} Imported={Imported}",db.OrganizationId,PortalAccess.UserId(User),id,imported);
        // Instructions are reference content, never a way to grant tools or override policy.
        return Ok(new {template.Id,template.Version,imported,skipped});
    }
}
