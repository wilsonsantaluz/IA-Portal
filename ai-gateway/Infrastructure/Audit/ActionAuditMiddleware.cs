using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;
using AiGateway.Security;

namespace AiGateway.Infrastructure.Audit;

public static class ActionAuditContext
{
    private static readonly object IdentityKey=new(),FailureKey=new(),TargetKey=new();
    public sealed record Identity(int UserId,int OrganizationId,string Name);
    public static void Login(HttpContext context,int user,int organization,string name)=>
        context.Items[IdentityKey]=new Identity(user,organization,name);
    public static Identity? LoginIdentity(HttpContext context)=>context.Items[IdentityKey] as Identity;
    public static void Failure(HttpContext context)=>context.Items[FailureKey]=true;
    public static bool Failed(HttpContext context)=>context.Items.ContainsKey(FailureKey);
    public static void Resource(HttpContext context,string kind,int id)=>SetResource(context,kind,id.ToString(CultureInfo.InvariantCulture));
    public static void Resource(HttpContext context,string kind,Guid id)=>SetResource(context,kind,id.ToString());
    private static void SetResource(HttpContext context,string kind,string id) {
        if(kind.Length is >0 and <=32 && kind.All(char.IsAsciiLetter))context.Items[TargetKey]=kind+":"+id;
    }
    public static string? Resource(HttpContext context)=>context.Items[TargetKey] as string;
}

public sealed class ActionAuditWriter(IServiceScopeFactory scopes,ILogger<ActionAuditWriter> logger)
{
    public async Task Append(ActionAuditEvent entry)
    {
        try {
            // Independent transaction/context: log denials, rollbacks and disconnected clients too.
            // This narrowly bounded raw INSERT is the only cross-tenant write in the audit path.
            await using var scope=scopes.CreateAsyncScope();
            var db=scope.ServiceProvider.GetRequiredService<PortalDbContext>();
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO ActionAuditEvents (CreatedAt,OrganizationId,ActorUserId,ActorName,Action,Operation,Area,Method,Route,TargetId,Outcome,StatusCode,DurationMs,RequestId,RemoteAddress) VALUES ({entry.CreatedAt},{entry.OrganizationId},{entry.ActorUserId},{entry.ActorName},{entry.Action},{entry.Operation},{entry.Area},{entry.Method},{entry.Route},{entry.TargetId},{entry.Outcome},{entry.StatusCode},{entry.DurationMs},{entry.RequestId},{entry.RemoteAddress})",timeout.Token);
        } catch(Exception ex) {
            // Never report entry data, SQL, exception messages or credentials on failure.
            logger.LogError("ActionAuditAppendFailed ({ErrorType}); request={RequestId}",ex.GetType().Name,entry.RequestId);
        }
    }
}

public sealed class ActionAuditMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context,ActionAuditWriter writer,TimeProvider clock,OrganizationScope organization)
    {
        if(!context.Request.Path.StartsWithSegments("/api")) { await next(context);return; }
        var started=clock.GetUtcNow().UtcDateTime;var watch=Stopwatch.StartNew();var unexpected=false;
        try { await next(context); }
        catch { unexpected=true;throw; }
        finally {
            var descriptor=context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
            // Only a server-owned route TEMPLATE is persisted. No raw URLs, query/body/header values.
            var route=(context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            route=route is null ? "/api/*" : "/"+route.TrimStart('/');
            var login=ActionAuditContext.LoginIdentity(context);
            var validated=context.User.Identity?.IsAuthenticated==true && organization.Id>0 && organization.UserId>0;
            var actor=login?.UserId ?? (validated ? organization.UserId : (int?)null);
            var tenant=login?.OrganizationId ?? (validated ? organization.Id : (int?)null);
            var status=unexpected ? 500 : context.Response.StatusCode;
            var outcome=context.RequestAborted.IsCancellationRequested ? "cancelled"
                : unexpected || ActionAuditContext.Failed(context) ? "failure"
                : status is 401 or 403 or 429 && !(descriptor?.ControllerName=="Auth" && descriptor.ActionName=="Login") ? "denied"
                : status>=400 ? "failure" : "success";
            var method=new[]{"GET","POST","PUT","PATCH","DELETE","HEAD","OPTIONS"}.Contains(context.Request.Method) ? context.Request.Method : "OTHER";
            var area=Area(route);var action=Label(descriptor?.ActionName,method,area);
            await writer.Append(new() {
                CreatedAt=started,OrganizationId=tenant,ActorUserId=actor,
                ActorName=Clean(login?.Name ?? (validated ? context.User.FindFirstValue("name") ?? context.User.FindFirstValue(ClaimTypes.Name) : null),150),
                Action=action,Operation=descriptor is null ? "HTTP.Unmatched" : descriptor.ControllerName+"."+descriptor.ActionName,
                Area=area,Method=method,Route=route,TargetId=ActionAuditContext.Resource(context) ?? Target(context),Outcome=outcome,
                StatusCode=status,DurationMs=watch.ElapsedMilliseconds,RequestId=Guid.NewGuid().ToString("N"),
                RemoteAddress=Clean(context.Connection.RemoteIpAddress?.ToString(),64)
            });
        }
    }
    private static string? Clean(string? value,int length)=>value is null ? null : new string(value.Where(c=>!char.IsControl(c)).Take(length).ToArray());
    private static string? Target(HttpContext context) {
        foreach(var key in new[]{"id","sessionId","userId"})
            if(context.Request.RouteValues.TryGetValue(key,out var raw)) {
                var value=Convert.ToString(raw,CultureInfo.InvariantCulture);
                if(Guid.TryParse(value,out var id))return key+":"+id;
                if(int.TryParse(value,out var number) && number>0)return key+":"+number;
            }
        return null;
    }
    private static string Area(string route) {
        var value=route.ToLowerInvariant();
        if(value.StartsWith("/api/auth"))return "Autenticação";
        if(value.StartsWith("/api/audit"))return "Log de ações";
        if(value.StartsWith("/api/kb") || value.StartsWith("/api/knowledge"))return "Base de conhecimento";
        if(value.StartsWith("/api/chat") || value.StartsWith("/api/ia"))return "Chat";
        if(value.StartsWith("/api/admin/ai") || value.StartsWith("/api/ai") || value.StartsWith("/api/me/ai"))return "Inteligência Artificial";
        if(value.StartsWith("/api/usage"))return "Consumo de IA";
        if(value.StartsWith("/api/users"))return "Usuários";
        if(value.StartsWith("/api/roles") || value.StartsWith("/api/permissions"))return "Grupos e permissões";
        if(value.StartsWith("/api/organization"))return "Organizações";
        if(value.StartsWith("/api/data") || value.StartsWith("/api/sql"))return "Dados e conectores";
        if(value.StartsWith("/api/channels") || value.StartsWith("/api/webhooks"))return "Canais";
        if(value.StartsWith("/api/ocr"))return "Documentos";
        return "Portal";
    }
    private static string Label(string? action,string method,string area)=>action switch {
        "Login"=>"Entrar no IA-Portal", "Logout"=>"Sair do IA-Portal", "Me"=>"Verificar sessão",
        "Register"=>"Solicitar cadastro", "Confirm"=>"Confirmar cadastro", "AskStream" or "AskSql"=>"Enviar pergunta",
        "Rename"=>"Renomear conversa", "Model"=>"Alterar modelo da conversa", "Toggle"=>"Alterar ativação de documento",
        "ReindexAll"=>"Reindexar base de conhecimento", "Upsert" or "Save" when area=="Base de conhecimento"=>"Criar ou editar documento",
        _=>method switch {"GET"=>"Consultar: "+area,"POST"=>"Executar: "+area,"PUT" or "PATCH"=>"Alterar: "+area,"DELETE"=>"Excluir: "+area,_=>"Acessar: "+area}
    };
}
