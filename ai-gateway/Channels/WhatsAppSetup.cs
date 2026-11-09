using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AiGateway.Security;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Channels;

public sealed record WhatsAppAsset(string PhoneNumberId,string AccountId);
public sealed record WhatsAppSetupStatus(string? AppId,string? WebhookUrl,string WebhookPath,
    bool AppSecretConfigured,bool VerifyTokenConfigured,bool AccessTokenConfigured,
    bool PublicCallbackConfigured,bool ReceiveConfigurationReady,bool LiveSendingAvailable,
    IReadOnlyList<WhatsAppAsset> AuthorizedNumbers);

// Explicit allowlist DTO: never serialize a configuration section or a credential.
public static class WhatsAppSetup {
    public const string CallbackPath="/api/webhooks/whatsapp";
    static bool Id(string? value)=>value is {Length:>0 and <=40}&&value.All(char.IsAsciiDigit);
    public static WhatsAppSetupStatus Read(IConfiguration config,int organizationId) {
        var appId=config["WhatsApp:AppId"];
        if(!Id(appId))appId=null;
        var numbers=config.GetSection("WhatsApp:Routes").GetChildren()
            .Where(section=>Id(section.Key)&&Id(section["AccountId"])&&section["OrganizationId"]==organizationId.ToString())
            .Select(section=>new WhatsAppAsset(section.Key,section["AccountId"]!)).ToArray();
        string? webhookUrl=null;
        if(Uri.TryCreate(config["WhatsApp:PublicBaseUrl"],UriKind.Absolute,out var origin)
            && origin.Scheme=="https"&&string.IsNullOrEmpty(origin.UserInfo+origin.Query+origin.Fragment)
            && origin.AbsolutePath=="/"&&!origin.IsLoopback)
            webhookUrl=new Uri(origin,CallbackPath).AbsoluteUri;
        var secret=!string.IsNullOrWhiteSpace(config["WhatsApp:AppSecret"]);
        var verify=!string.IsNullOrWhiteSpace(config["WhatsApp:VerifyToken"]);
        var token=!string.IsNullOrWhiteSpace(config["WhatsApp:AccessToken"]);
        return new(appId,webhookUrl,CallbackPath,secret,verify,token,webhookUrl is not null,
            secret&&verify&&webhookUrl is not null&&numbers.Length>0,false,numbers);
    }
}

[ApiController,Route("api/channels/whatsapp/setup"),Authorize(Policy=PortalAccess.Channels)]
public sealed class WhatsAppSetupController(PortalDbContext db,IConfiguration config):ControllerBase {
    [HttpGet]public ActionResult<WhatsAppSetupStatus> Get()=>WhatsAppSetup.Read(config,db.OrganizationId);
}
