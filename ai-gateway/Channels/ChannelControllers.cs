using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AiGateway.Channels;

[ApiController,Route("api/channels"),Authorize(Policy=PortalAccess.Channels),RequestSizeLimit(32768)]
public sealed class ChannelsController(ChannelService service,IConfiguration config,PortalDbContext db):ControllerBase {
    [HttpGet]public async Task<IActionResult> List(CancellationToken ct)=>Ok(await service.List(ct));
    [HttpPost,Authorize(Policy=PortalAccess.Knowledge)]public async Task<IActionResult> Create(ChannelCreate input,CancellationToken ct){
        // Installation operator must bind verified provider assets before tenant registration.
        if(input.Provider=="whatsapp-cloud" && (config[$"WhatsApp:Routes:{input.ExternalPhoneNumberId}:OrganizationId"]!=db.OrganizationId.ToString()
            ||config[$"WhatsApp:Routes:{input.ExternalPhoneNumberId}:AccountId"]!=input.ExternalAccountId))
            throw new ChatException(409,"Número não autorizado pelo operador. Onboarding oficial pendente.");
        return Ok(await service.Create(input,ct));
    }
    [HttpPut("{id:guid}")]public async Task<IActionResult> Update(Guid id,ChannelPolicy input,CancellationToken ct){await service.Update(id,input,ct);return NoContent();}
    [HttpPost("{id:guid}/simulate")]public async Task<IActionResult> Simulate(Guid id,InboundChannelMessage input,CancellationToken ct){
        if((await service.Connection(id,ct)).Provider!="simulator")throw new ChatException(409,"Use um canal de simulação.");
        return Ok(new{accepted=await service.Receive(id,input,ct)});
    }
    [HttpGet("{id:guid}/conversations")]public async Task<IActionResult> Conversations(Guid id,int offset,CancellationToken ct)=>Ok(await service.Conversations(id,offset,ct));
    [HttpGet("conversations/{id:guid}/messages")]public async Task<IActionResult> Messages(Guid id,int offset,CancellationToken ct)=>Ok(await service.Messages(id,offset,ct));
    [HttpPost("conversations/{id:guid}/handoff")]public async Task<IActionResult> Handoff(Guid id,HandoffRequest input,CancellationToken ct){await service.Handoff(id,input,ct);return NoContent();}
    [HttpGet("{id:guid}/usage")]public async Task<IActionResult> Usage(Guid id,CancellationToken ct)=>Ok(await service.Usage(id,ct));
}

public static class WhatsAppSignature {
    public static string? StringValue(this JsonElement element)=>element.ValueKind==JsonValueKind.String?element.GetString():null;
    public static bool Verify(byte[] body,string? signature,string? secret){
        if(string.IsNullOrWhiteSpace(secret)||signature is null||!signature.StartsWith("sha256=",StringComparison.Ordinal)||signature.Length!=71)return false;
        try{return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(signature[7..]),HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret),body));}catch(FormatException){return false;}
    }
    public static bool Equal(string? a,string? b)=>!string.IsNullOrEmpty(a)&&!string.IsNullOrEmpty(b)&&CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(a)),SHA256.HashData(Encoding.UTF8.GetBytes(b)));
}
[ApiController,Route("api/webhooks/whatsapp"),AllowAnonymous,RequestSizeLimit(262144)]
public sealed class WhatsAppWebhookController(IConfiguration config,PortalDbContext db,OrganizationScope scope,ChannelService service):ControllerBase {
    [HttpGet]public IActionResult Verify([FromQuery(Name="hub.mode")]string? mode,[FromQuery(Name="hub.verify_token")]string? token,[FromQuery(Name="hub.challenge")]string? challenge){
        if(mode!="subscribe"||!WhatsAppSignature.Equal(token,config["WhatsApp:VerifyToken"])||challenge is null||challenge.Length>100)return Unauthorized();
        return Content(challenge,"text/plain");
    }
    [HttpPost]public async Task<IActionResult> Receive(CancellationToken ct){
        using var stream=new MemoryStream();var buffer=new byte[8192];int read;
        while((read=await Request.Body.ReadAsync(buffer,ct))>0){
            if(stream.Length+read>262144)return StatusCode(413);
            await stream.WriteAsync(buffer.AsMemory(0,read),ct);
        }
        var body=stream.ToArray();
        if(!WhatsAppSignature.Verify(body,Request.Headers["X-Hub-Signature-256"],config["WhatsApp:AppSecret"]))return Unauthorized();
        JsonDocument document;
        try{document=JsonDocument.Parse(body,new JsonDocumentOptions{MaxDepth=24});}catch(JsonException){return BadRequest();}
        using(document){
            var root=document.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("object",out var kind)||kind.StringValue()!="whatsapp_business_account")return BadRequest();
            if(!root.TryGetProperty("entry",out var entries)||entries.ValueKind!=JsonValueKind.Array)return BadRequest();
            foreach(var entry in entries.EnumerateArray()){
                if(entry.ValueKind!=JsonValueKind.Object||!entry.TryGetProperty("id",out var account)||!entry.TryGetProperty("changes",out var changes)||changes.ValueKind!=JsonValueKind.Array)continue;
                foreach(var change in changes.EnumerateArray()){
                    // History/app echoes are intentionally not customer input and cannot trigger a bot.
                    if(change.ValueKind!=JsonValueKind.Object||!change.TryGetProperty("field",out var field)||field.StringValue()!="messages"||!change.TryGetProperty("value",out var value)
                        ||value.ValueKind!=JsonValueKind.Object||!value.TryGetProperty("metadata",out var metadata)||metadata.ValueKind!=JsonValueKind.Object||!metadata.TryGetProperty("phone_number_id",out var phone))continue;
                    var phoneId=phone.StringValue();var accountId=account.StringValue();
                    if(phoneId is null||accountId is null)continue;
                    var connection=await db.ChannelConnections.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c=>c.Provider=="whatsapp-cloud"&&c.ExternalPhoneNumberId==phoneId&&c.ExternalAccountId==accountId&&c.IsEnabled,ct);
                    if(connection is null || config[$"WhatsApp:Routes:{phoneId}:OrganizationId"]!=connection.OrganizationId.ToString()
                        || config[$"WhatsApp:Routes:{phoneId}:AccountId"]!=accountId || !await db.Organizations.AnyAsync(o=>o.Id==connection.OrganizationId&&o.IsActive,ct))continue;
                    scope.Id=connection.OrganizationId;
                    if(value.TryGetProperty("messages",out var messages)&&messages.ValueKind==JsonValueKind.Array){
                        foreach(var m in messages.EnumerateArray()){
                            if(m.ValueKind!=JsonValueKind.Object||!m.TryGetProperty("type",out var type)||type.StringValue()!="text"||!m.TryGetProperty("id",out var id)||!m.TryGetProperty("from",out var from)
                                ||!m.TryGetProperty("timestamp",out var ts)||!long.TryParse(ts.StringValue(),out var seconds)||!m.TryGetProperty("text",out var text)||text.ValueKind!=JsonValueKind.Object||!text.TryGetProperty("body",out var content))continue;
                            if(seconds<0||seconds>253402300799)continue;
                            try{await service.Receive(connection.Id,new(id.StringValue()!,from.StringValue()!,content.StringValue()!,DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime),ct);}
                            catch(ChatException ex)when(ex.Status==400){/* Unsupported/malformed input never becomes a retry loop. */}
                        }
                    }
                    // Status/pricing events will be linked only to locally known outbound messages.
                    // This increment never sends real WhatsApp messages, so no synthetic delivery is recorded.
                    db.ChangeTracker.Clear();
                }
            }
        }
        return Ok(); // inbound is durably stored before ack; agent processing happens in worker
    }
}
