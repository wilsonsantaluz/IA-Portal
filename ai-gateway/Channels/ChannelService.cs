using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Channels;

public sealed record ChannelCreate([Required,StringLength(100,MinimumLength=2)] string DisplayName,
    [Required] string Provider,[Required,StringLength(80)] string ExternalAccountId,[Required,StringLength(80)] string ExternalPhoneNumberId,int[] KnowledgeIds);
public sealed record ChannelPolicy(int Version,bool IsEnabled,bool AutoReply,bool AgentEnabled,int RetentionDays,int MonthlyLimit);
public sealed record HandoffRequest(int Version,bool HumanHandoff);
public sealed class SimulatorSender : IOutboundMessageSender {
    public string Name=>"simulator";
    public Task<ChannelSendResult> Send(ChannelConnection c,ChannelMessage m,CancellationToken ct)
        =>Task.FromResult(new ChannelSendResult("simulated:"+m.Id.ToString("N")));
}
public sealed class ChannelService(PortalDbContext db,IChannelAgent agent,TimeProvider clock,
    IOutboundMessageSender sender,ILogger<ChannelService> logger,IConfiguration config) : IInboundMessageHandler,IChannelConversationRepository {
    DateTime Now=>clock.GetUtcNow().UtcDateTime;
    public async Task<ChannelConnection> Connection(Guid id,CancellationToken ct)=>
        await db.ChannelConnections.SingleOrDefaultAsync(x=>x.Id==id,ct) ?? throw new ChatException(404,"Canal não encontrado.");
    public async Task<object> Create(ChannelCreate input,CancellationToken ct) {
        if(input.Provider is not ("simulator" or "whatsapp-cloud") || input.KnowledgeIds is null || input.KnowledgeIds.Length>50
            || !ValidId(input.ExternalAccountId) || !ValidId(input.ExternalPhoneNumberId))throw new ChatException(400,"Configuração de canal inválida.");
        var ids=input.KnowledgeIds.Distinct().ToArray();
        if(await db.KnowledgeBaseItems.CountAsync(x=>ids.Contains(x.Id)&&x.IsActive,ct)!=ids.Length)throw new ChatException(400,"Documentos não autorizados.");
        var item=new ChannelConnection{Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,DisplayName=input.DisplayName.Trim(),Provider=input.Provider,
            ExternalAccountId=input.ExternalAccountId,ExternalPhoneNumberId=input.ExternalPhoneNumberId,KnowledgeIdsJson=JsonSerializer.Serialize(ids)};
        db.ChannelConnections.Add(item);
        try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){throw new ChatException(409,"Identificação de canal indisponível.");}
        logger.LogInformation("ChannelCreated Organization={Organization} Channel={Channel}",db.OrganizationId,item.Id);
        return View(item);
    }
    static bool ValidId(string? s)=>s is {Length:>0 and <=80} && s.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_' or ':');
    // Provider message IDs are opaque (wamid/base64 may contain '.', '/', '+' or '=').
    static bool ValidMessageId(string? s)=>s is {Length:>0 and <=200} && s.All(c=>c is >= '!' and <= '~');
    static object View(ChannelConnection x)=>new{x.Id,x.DisplayName,x.Provider,x.ExternalAccountId,x.ExternalPhoneNumberId,x.IsEnabled,x.AutoReply,x.AgentEnabled,x.AgentId,x.RetentionDays,x.MonthlyLimit,x.Version,
        knowledgeIds=JsonSerializer.Deserialize<int[]>(x.KnowledgeIdsJson),status=!x.IsEnabled?"disabled":x.Provider=="simulator"?"simulated":"receive-only; live onboarding not verified"};
    public async Task<object> List(CancellationToken ct)=>(await db.ChannelConnections.AsNoTracking().OrderBy(x=>x.DisplayName).ToListAsync(ct)).Select(View);
    public async Task Update(Guid id,ChannelPolicy input,CancellationToken ct) {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var c=await Connection(id,ct);
        if(c.Version!=input.Version)throw new ChatException(409,"Canal alterado; atualize a página.");
        if(input.RetentionDays is <1 or >365 || input.MonthlyLimit is <0 or >10000)throw new ChatException(400,"Limites inválidos.");
        if(input.AutoReply && c.Provider!="simulator")throw new ChatException(409,"Envio real indisponível neste incremento. Use o simulador.");
        c.IsEnabled=input.IsEnabled;c.AutoReply=input.AutoReply;c.AgentEnabled=input.AgentEnabled;c.RetentionDays=input.RetentionDays;c.MonthlyLimit=input.MonthlyLimit;c.Version++;
        await Save(ct);await tx.CommitAsync(ct);
        logger.LogInformation("ChannelPolicyChanged Organization={Organization} Channel={Channel} AutoReply={AutoReply}",db.OrganizationId,id,c.AutoReply);
    }
    async Task Save(CancellationToken ct){try{await db.SaveChangesAsync(ct);}catch(DbUpdateConcurrencyException){throw new ChatException(409,"Registro alterado; atualize a página.");}}
    public async Task<bool> Receive(Guid connectionId,InboundChannelMessage input,CancellationToken ct) {
        if(!ValidMessageId(input.ExternalId)||!ValidId(input.ContactId)||string.IsNullOrWhiteSpace(input.Text)||input.Text.Length>4000
            || input.Timestamp>Now.AddMinutes(5)||input.Timestamp<Now.AddDays(-365))throw new ChatException(400,"Mensagem inválida.");
        await using var tx=await db.Database.BeginTransactionAsync(ct);var c=await Connection(connectionId,ct);
        if(!c.IsEnabled)throw new ChatException(409,"Canal desativado.");
        if(await db.ChannelMessages.AnyAsync(x=>x.ConnectionId==c.Id&&x.ExternalId==input.ExternalId,ct))return false;
        var contact=await db.ChannelContacts.SingleOrDefaultAsync(x=>x.ConnectionId==c.Id&&x.ExternalId==input.ContactId,ct);
        if(contact is null){contact=new(){Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,ConnectionId=c.Id,ExternalId=input.ContactId};db.ChannelContacts.Add(contact);}
        var conversation=await db.ChannelConversations.SingleOrDefaultAsync(x=>x.ConnectionId==c.Id&&x.ContactId==contact.Id,ct);
        if(conversation is null){conversation=new(){Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,ConnectionId=c.Id,ContactId=contact.Id};db.ChannelConversations.Add(conversation);}
        // Provider timestamp controls the service window, never server receipt/replay time.
        if(input.Timestamp>conversation.LastInboundAt)conversation.LastInboundAt=input.Timestamp;
        conversation.UpdatedAt=Now;conversation.Version++;
        db.ChannelMessages.Add(new(){Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,ConnectionId=c.Id,ConversationId=conversation.Id,
            ExternalId=input.ExternalId,Text=input.Text,CreatedAt=input.Timestamp,NextAttemptAt=Now});
        await Save(ct);await tx.CommitAsync(ct);return true;
    }
    public async Task<object> Conversations(Guid connectionId,int offset,CancellationToken ct) {
        await Connection(connectionId,ct);
        return await db.ChannelConversations.AsNoTracking().Where(x=>x.ConnectionId==connectionId).OrderByDescending(x=>x.UpdatedAt).ThenBy(x=>x.Id).Skip(Math.Clamp(offset,0,100000)).Take(50).ToListAsync(ct);
    }
    public async Task<object> Messages(Guid conversationId,int offset,CancellationToken ct) {
        if(!await db.ChannelConversations.AnyAsync(x=>x.Id==conversationId,ct))throw new ChatException(404,"Conversa não encontrada.");
        return await db.ChannelMessages.AsNoTracking().Where(x=>x.ConversationId==conversationId).OrderBy(x=>x.CreatedAt).ThenBy(x=>x.Id).Skip(Math.Clamp(offset,0,100000)).Take(50)
            .Select(x=>new{x.Id,x.Direction,x.Text,x.State,x.CreatedAt,x.SourcesJson,x.Attempts,x.Category,tokenUsage=(int?)null,llmCost=(decimal?)null}).ToListAsync(ct);
    }
    public async Task Handoff(Guid id,HandoffRequest input,CancellationToken ct) {
        var item=await db.ChannelConversations.SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new ChatException(404,"Conversa não encontrada.");
        if(item.Version!=input.Version)throw new ChatException(409,"Conversa alterada.");
        item.HumanHandoff=input.HumanHandoff;item.Version++;await Save(ct);
        logger.LogInformation("ChannelHandoff Organization={Organization} Conversation={Conversation} Human={Human}",db.OrganizationId,id,input.HumanHandoff);
    }
    public async Task<object> Usage(Guid id,CancellationToken ct) {
        var c=await Connection(id,ct);var month=new DateTime(Now.Year,Now.Month,1,0,0,0,DateTimeKind.Utc);
        return new{inbound=await db.ChannelMessages.CountAsync(x=>x.ConnectionId==id&&x.Direction=="inbound"&&x.CreatedAt>=month,ct),
            outbound=await db.ChannelMessages.CountAsync(x=>x.ConnectionId==id&&x.Direction=="outbound"&&x.CreatedAt>=month,ct),c.MonthlyLimit,
            metaCost=(decimal?)null,llmCost=(decimal?)null,infrastructureCost=(decimal?)null,mode="extractive simulation; no LLM call"};
    }
    // One bounded transaction; the only sender enabled here is deterministic and side-effect-free.
    // Do not add remote sends inside this transaction: real sends need ambiguity reconciliation.
    public async Task Process(CancellationToken ct) {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var now=Now;
        var inputs=await db.ChannelMessages.Where(x=>x.Direction=="inbound"&&x.State=="pending").OrderBy(x=>x.CreatedAt).Take(20).ToListAsync(ct);
        foreach(var input in inputs) {
            var c=await Connection(input.ConnectionId,ct);var conversation=await db.ChannelConversations.SingleAsync(x=>x.Id==input.ConversationId,ct);
            input.State="processed";
            if(!c.IsEnabled||!c.AutoReply||!c.AgentEnabled||conversation.HumanHandoff||c.Provider!="simulator"||c.AgentId!="local-extractive-v1"||conversation.LastInboundAt<=now.AddHours(-24)){input.State="held";continue;}
            var month=new DateTime(now.Year,now.Month,1,0,0,0,DateTimeKind.Utc);
            var count=await db.ChannelMessages.CountAsync(x=>x.ConnectionId==c.Id&&x.Direction=="outbound"&&x.CreatedAt>=month,ct)
                +db.ChangeTracker.Entries<ChannelMessage>().Count(x=>x.State==EntityState.Added&&x.Entity.ConnectionId==c.Id&&x.Entity.Direction=="outbound");
            var organizationLimit=Math.Clamp(config.GetValue<int?>("Channels:OrganizationLimits:"+db.OrganizationId) ?? 1000,0,100000);
            var organizationCount=await db.ChannelMessages.CountAsync(x=>x.Direction=="outbound"&&x.CreatedAt>=month,ct)
                +db.ChangeTracker.Entries<ChannelMessage>().Count(x=>x.State==EntityState.Added&&x.Entity.Direction=="outbound");
            if(count>=c.MonthlyLimit || organizationCount>=organizationLimit){input.State="limit";continue;}
            var answer=await agent.Answer(input.Text,JsonSerializer.Deserialize<int[]>(c.KnowledgeIdsJson)??[],ct);
            db.ChannelMessages.Add(new(){Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,ConnectionId=c.Id,ConversationId=conversation.Id,
                ExternalId="reply:"+input.Id.ToString("N"),Direction="outbound",State="queued",CreatedAt=now,NextAttemptAt=now,Text=answer.Text,SourcesJson=answer.SourcesJson});
        }
        await Save(ct);
        var outputs=await db.ChannelMessages.Where(x=>x.Direction=="outbound"&&x.State=="queued"&&x.NextAttemptAt<=now).Take(20).ToListAsync(ct);
        foreach(var output in outputs){
            var c=await Connection(output.ConnectionId,ct);var conv=await db.ChannelConversations.SingleAsync(x=>x.Id==output.ConversationId,ct);
            if(!c.IsEnabled||!c.AutoReply||!c.AgentEnabled||conv.HumanHandoff||conv.LastInboundAt<=now.AddHours(-24)||c.Provider!=sender.Name){output.State="held";continue;}
            var month=new DateTime(now.Year,now.Month,1,0,0,0,DateTimeKind.Utc);
            var organizationLimit=Math.Clamp(config.GetValue<int?>("Channels:OrganizationLimits:"+db.OrganizationId) ?? 1000,0,100000);
            if(await db.ChannelMessages.CountAsync(x=>x.ConnectionId==c.Id&&x.Direction=="outbound"&&x.CreatedAt>=month,ct)>c.MonthlyLimit
                ||await db.ChannelMessages.CountAsync(x=>x.Direction=="outbound"&&x.CreatedAt>=month,ct)>organizationLimit){output.State="limit";continue;}
            output.Attempts++;
            try{var result=await sender.Send(c,output,ct);output.State="simulated";
                db.MessageDeliveryEvents.Add(new(){Id=Guid.NewGuid(),OrganizationId=db.OrganizationId,MessageId=output.Id,ExternalKey=result.ExternalId,Status="simulated",CreatedAt=now});}
            catch(ChannelTransientException){output.State=output.Attempts>=3?"failed":"queued";output.NextAttemptAt=now.AddSeconds(30*Math.Pow(2,output.Attempts-1));}
        }
        // Redact content, preserve minimal deduplication tombstones. No raw webhook persisted.
        foreach(var c in await db.ChannelConnections.ToListAsync(ct)){
            var cutoff=now.AddDays(-c.RetentionDays);
            await db.ChannelMessages.Where(x=>x.ConnectionId==c.Id&&x.CreatedAt<cutoff).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Text,"").SetProperty(x=>x.SourcesJson,"[]").SetProperty(x=>x.State,"retained-tombstone"),ct);
        }
        await Save(ct);await tx.CommitAsync(ct);
    }
}
public sealed class ChannelTransientException : Exception { }
