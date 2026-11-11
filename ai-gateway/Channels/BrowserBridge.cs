using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.Json;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Channels;

// Fixed operator endpoint; tenant input never selects a host, port or credential.
public sealed class BrowserBridge(IHttpClientFactory clients,IConfiguration config) {
    public bool Enabled=>config.GetValue<bool>("BrowserBridge:Enabled");
    public async Task<JsonElement> Call(int organization,Guid id,string action,object? body,CancellationToken ct) {
        var secret=config["BrowserBridge:Secret"];
        if(!Enabled||string.IsNullOrWhiteSpace(secret)||secret.Length<32)throw new ChatException(503,"Conector de navegador não configurado.");
        var endpoint=config["BrowserBridge:Url"];
        if(!Uri.TryCreate(endpoint,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https")||!string.IsNullOrEmpty(uri.UserInfo+uri.Query+uri.Fragment))
            throw new ChatException(503,"Endereço interno do conector inválido.");
        using var request=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,new Uri(uri,$"sessions/{organization}/{id:D}/{action}"));
        request.Headers.Authorization=new("Bearer",secret);
        if(body is not null)request.Content=JsonContent.Create(body);
        try {
            using var response=await clients.CreateClient("browser-bridge").SendAsync(request,ct);
            if(!response.IsSuccessStatusCode)throw new ChatException(503,"Conector indisponível; tente atualizar o status.");
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
        }catch(HttpRequestException){throw new ChatException(503,"Conector indisponível.");}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new ChatException(503,"Conector não respondeu; confira o status antes de tentar novamente.");}
    }
}
public sealed record ManualChannelReply([Required,StringLength(4000,MinimumLength=1)]string Text,Guid RequestId);

[ApiController,Route("api/channels"),Authorize(Policy=PortalAccess.Channels),RequestSizeLimit(32768)]
public sealed class BrowserChannelsController(ChannelService service,BrowserBridge bridge,PortalDbContext db):ControllerBase {
    async Task<ChannelConnection> Browser(Guid id,CancellationToken ct) {
        var c=await service.Connection(id,ct);
        if(c.Provider!="whatsapp-browser")throw new ChatException(409,"Este canal não usa navegador.");return c;
    }
    [HttpGet("browser/capabilities")]public object Capabilities()=>new{enabled=bridge.Enabled};
    [HttpGet("{id:guid}/browser")]public async Task<JsonElement> Status(Guid id,CancellationToken ct) {
        await Browser(id,ct);Response.Headers.CacheControl="no-store";
        return await bridge.Call(db.OrganizationId,id,"status",null,ct);
    }
    [HttpPost("{id:guid}/browser/{operation}")]public async Task<JsonElement> Operate(Guid id,string operation,CancellationToken ct) {
        var c=await Browser(id,ct);
        if(operation is not ("start" or "stop" or "logout"))throw new ChatException(400,"Operação inválida.");
        if(operation=="start"&&!c.IsEnabled)throw new ChatException(409,"Habilite o canal antes de conectar.");
        Response.Headers.CacheControl="no-store";return await bridge.Call(db.OrganizationId,id,operation,new{},ct);
    }
    [HttpPost("conversations/{id:guid}/reply")]public async Task<object> Reply(Guid id,ManualChannelReply input,CancellationToken ct) {
        if(!bridge.Enabled)throw new ChatException(503,"Conector desabilitado.");
        return await service.ManualReply(id,input,ct);
    }
}

// Single-node worker: durable portal outbox -> idempotent bridge journal -> browser.
// No external side effects inside the portal SQLite transaction.
public sealed class BrowserChannelPump(PortalDbContext db,ChannelService service,BrowserBridge bridge,TimeProvider clock,IConfiguration config) {
    public async Task Tick(CancellationToken ct) {
        if(!bridge.Enabled)return;
        var connections=await db.ChannelConnections.Where(x=>x.Provider=="whatsapp-browser").ToArrayAsync(ct);
        foreach(var c in connections) {
            if(!c.IsEnabled){await bridge.Call(db.OrganizationId,c.Id,"stop",new{},ct);continue;}
            var incoming=await bridge.Call(db.OrganizationId,c.Id,"inbox",null,ct);
            foreach(var row in incoming.EnumerateArray()) {
                var message=row.Deserialize<InboundChannelMessage>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if(message is null)continue;
                try{await service.Receive(c.Id,message,ct);}
                catch(ChatException e)when(e.Status==400){/* Poison/expired item: acknowledge without storing its body. */}
                await bridge.Call(db.OrganizationId,c.Id,"ack",new{ids=new[]{message.ExternalId}},ct);
            }
            var now=clock.GetUtcNow().UtcDateTime;
            var outputs=await db.ChannelMessages.Where(x=>x.ConnectionId==c.Id&&x.Direction=="outbound"&&x.NextAttemptAt<=now
                &&(x.State=="queued"||x.State=="dispatching"||x.State=="sent"||x.State=="delivered"))
                .OrderBy(x=>x.NextAttemptAt).ThenBy(x=>x.CreatedAt).Take(50).ToArrayAsync(ct);
            foreach(var m in outputs) {
                JsonElement result;
                if(m.State is "sent" or "delivered")result=await bridge.Call(db.OrganizationId,c.Id,"delivery",new{id=m.Id},ct);
                else {
                    var conv=await db.ChannelConversations.SingleAsync(x=>x.Id==m.ConversationId,ct);
                    var month=new DateTime(clock.GetUtcNow().Year,clock.GetUtcNow().Month,1,0,0,0,DateTimeKind.Utc);
                    var cap=Math.Clamp(config.GetValue<int?>("Channels:OrganizationLimits:"+db.OrganizationId)??1000,0,100000);
                    var allowed=c.IsEnabled&&(m.Category=="manual"?conv.HumanHandoff:c.AutoReply&&c.AgentEnabled&&!conv.HumanHandoff)
                        &&conv.LastInboundAt>clock.GetUtcNow().UtcDateTime.AddHours(-24)
                        &&await db.ChannelMessages.CountAsync(x=>x.ConnectionId==c.Id&&x.Direction=="outbound"&&x.CreatedAt>=month,ct)<=c.MonthlyLimit
                        &&await db.ChannelMessages.CountAsync(x=>x.Direction=="outbound"&&x.CreatedAt>=month,ct)<=cap;
                    // Reconcile a previous dispatch even if policy changed, but do not initiate a new send.
                    result=await bridge.Call(db.OrganizationId,c.Id,"delivery",new{id=m.Id},ct);
                    if(result.GetProperty("state").GetString()=="absent") {
                        if(!allowed){m.State="held";await db.SaveChangesAsync(ct);continue;}
                        var contact=await db.ChannelContacts.SingleAsync(x=>x.Id==conv.ContactId,ct);
                        m.State="dispatching";await db.SaveChangesAsync(ct);
                        result=await bridge.Call(db.OrganizationId,c.Id,"send",new{id=m.Id,contact=contact.ExternalId,text=m.Text},ct);
                    }
                }
                var state=result.GetProperty("state").GetString();
                m.State=state switch {"sent"=>"sent","delivered"=>"delivered","read"=>"read","failed"=>"failed","unknown"=>"unknown",_=>m.State};
                m.NextAttemptAt=now.AddSeconds(m.State is "sent" or "delivered"?30:5);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
