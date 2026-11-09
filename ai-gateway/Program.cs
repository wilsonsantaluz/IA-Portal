using AiGateway.Channels;
using AiGateway.Application.Interfaces;
using AiGateway.Infrastructure.Qdrant;
using AiGateway.Infrastructure.Qdrant.Services;
using AiGateway.Infrastructure.Qdrant.Services.Chats;
using AiGateway.Infrastructure.Qdrant.Services.LLMs;
using AiGateway.Infrastructure.Qdrant.Services.NomicEmbedding;
using AiGateway.Infrastructure.Qdrant.Services.Reranking;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using Portal.AI.Gateway.Infrastructure.Trino;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Data.Sqlite;
using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;


var builder = WebApplication.CreateBuilder(args);
var minimalHosting=builder.Configuration.GetValue<bool>("Hosting:Minimal");
if(minimalHosting && builder.Configuration.GetValue<bool>("Chat:UseAdvancedRag"))
    throw new InvalidOperationException("Hosting:Minimal requer Chat:UseAdvancedRag=false.");
if(minimalHosting) builder.Services.AddRateLimiter(options=>{
    options.RejectionStatusCode=429;
    options.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>{
        var login=context.Request.Path.Equals(new PathString("/api/auth/login"));
        var key=(context.Connection.RemoteIpAddress?.ToString() ?? "unknown")+(login?":login":":api");
        return RateLimitPartition.GetFixedWindowLimiter(key,_=>new FixedWindowRateLimiterOptions{
            PermitLimit=login?10:128,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true});
    });
});
var connectorFile = builder.Configuration["TRINO_CONNECTORS_FILE"];
if (!string.IsNullOrWhiteSpace(connectorFile)) builder.Configuration.AddJsonFile(Path.GetFullPath(connectorFile,builder.Environment.ContentRootPath),optional:false,reloadOnChange:false);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<OrganizationScope>();
builder.Services.AddScoped<LocalKnowledgeRetriever>();
builder.Services.AddScoped<IChannelAgent,LocalExtractiveChannelAgent>();
builder.Services.AddScoped<ChannelService>();
builder.Services.AddSingleton<IOutboundMessageSender,SimulatorSender>();
builder.Services.AddHostedService<ChannelWorker>();
builder.Services.AddScoped<BrowserBridge>();
builder.Services.AddScoped<BrowserChannelPump>();
builder.Services.AddHttpClient("browser-bridge",c=>c.Timeout=TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false,UseProxy=false});
builder.Services.AddScoped<OrganizationWorkspaceService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<OrganizationInvitationService>();
builder.Services.AddSingleton<ConnectorProfiles>();
builder.Services.AddScoped<DataConnectorService>();
builder.Services.AddHttpClient("trino-secure", c => c.Timeout = TimeSpan.FromSeconds(20)).ConfigurePrimaryHttpMessageHandler(ConnectorProfiles.Handler);
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
builder.Services.AddScoped<TrinoRestClient>();

builder.Services.AddHttpClient("ai-provider", client => client.Timeout = TimeSpan.FromMinutes(3)).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<ITrinoQueryService, TrinoQueryService>();
builder.Services.AddScoped<ITrinoRestClient, TrinoRestClient>();
builder.Services.AddScoped<ITrinoMetadataService, TrinoMetadataService>();
builder.Services.AddScoped<AiProviderCatalog>();
builder.Services.AddScoped<DeepSeekService>();
builder.Services.AddScoped<OpenAiService>();
builder.Services.AddScoped<OllamaService>();
builder.Services.AddScoped<IAiProviderAdapter>(s => s.GetRequiredService<DeepSeekService>());
builder.Services.AddScoped<IAiProviderAdapter>(s => s.GetRequiredService<OpenAiService>());
builder.Services.AddScoped<IAiProviderAdapter>(s => s.GetRequiredService<OllamaService>());
builder.Services.AddScoped<AiProviderRouter>();
builder.Services.AddScoped<IOllamaService>(s => s.GetRequiredService<AiProviderRouter>());
builder.Services.AddScoped<IChatTitleGenerator>(s => s.GetRequiredService<AiProviderRouter>());
builder.Services.AddScoped<ILlmMetrics>(s => s.GetRequiredService<AiProviderRouter>());
builder.Services.AddScoped<ChatTitleService>();
builder.Services.AddScoped<ChatUsageService>();
builder.Services.AddScoped<UsageQueryService>();
builder.Services.AddSingleton(ModelPriceCatalog.Load(Path.GetFullPath(builder.Configuration["LLM_PRICES_FILE"] ?? "config/model-prices.json", builder.Environment.ContentRootPath)));
builder.Services.AddScoped<IMlService, MlService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<IDataRagService, DataRagService>();

builder.Services.AddScoped<ChatGrounding>();
builder.Services.AddScoped<LocalChatContextService>();
builder.Services.AddScoped<VectorChatContextService>();
builder.Services.AddScoped<IAskSmartService, BusinessChatContextService>();
builder.Services.AddHttpClient<OllamaPdfService>();
builder.Services.AddSingleton<IRerankerService, OnnxRerankerService>();



var sqlite = new SqliteConnectionStringBuilder(
    builder.Configuration.GetConnectionString("PortalConnection") ?? "Data Source=data/portal.db");
sqlite.DataSource = Path.GetFullPath(sqlite.DataSource, builder.Environment.ContentRootPath);
Directory.CreateDirectory(Path.GetDirectoryName(sqlite.DataSource)!);
builder.Services.AddDbContext<PortalDbContext>(options => options.UseSqlite(sqlite.ToString()));


builder.Services.AddScoped<IChatRepository, ChatRepository>();
builder.Services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();

builder.Services.AddSingleton<IOnnxEmbeddingService, NomicEmbeddingService>();
builder.Services.AddScoped<IKnowledgeBaseRagService, KnowledgeBaseRagService>();
builder.Services.AddScoped<ISqlPlannerService, SqlPlannerService>();

builder.Services.AddHttpClient<IQdrantClient>();
/*
 builder.Services
    .AddAuthentication("JwtBearer")
    .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// ... AddControllers, DbContext, etc.

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

 */

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", opt =>
    {
        var cfg = builder.Configuration.GetSection("Jwt");

        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = cfg["Issuer"],
            ValidAudience = cfg["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(cfg["Key"]!)
            )
        };
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var db = context.HttpContext.RequestServices.GetRequiredService<PortalDbContext>();
                var principal = context.Principal!;
                if (!int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
                { context.Fail("Sessão inválida."); return; }
                var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == id && x.IsActive, context.HttpContext.RequestAborted);
                if (user is null || principal.FindFirstValue("ver") != PortalAccess.Version(user, context.HttpContext.RequestServices.GetRequiredService<IConfiguration>()))
                { context.Fail("Sessão expirada. Entre novamente."); return; }
                var selection = context.Request.Headers["X-Organization-Id"];
                int organizationId;
                if (selection.Count == 0) {
                    organizationId = await PortalAccess.DefaultOrganizationAsync(db,id,builder.Configuration,context.HttpContext.RequestAborted);
                } else if (selection.Count != 1 || !int.TryParse(selection[0], out organizationId) || organizationId <= 0) {
                    context.Fail("Organizacao invalida."); return;
                }
                if (!await db.OrganizationMemberships.AnyAsync(x => x.UserId == id && x.OrganizationId == organizationId && x.IsActive, context.HttpContext.RequestAborted)
                    || !await db.Organizations.AnyAsync(x => x.Id == organizationId && x.IsActive, context.HttpContext.RequestAborted)) {
                    context.Fail("Organizacao nao autorizada."); return;
                }
                var scope = context.HttpContext.RequestServices.GetRequiredService<OrganizationScope>();
                scope.Id = organizationId; scope.UserId = id;
                var access = await PortalAccess.ReadAsync(db, id, context.HttpContext.RequestAborted, organizationId);
                var identity = (ClaimsIdentity)principal.Identity!;
                foreach (var claim in identity.Claims.Where(x => x.Type == "perm" || x.Type == ClaimTypes.Role).ToArray()) identity.RemoveClaim(claim);
                identity.AddClaims(access.Permissions.Select(x => new Claim("perm", x)));
                identity.AddClaims(access.Roles.Select(x => new Claim(ClaimTypes.Role, x)));
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireClaim("perm", PortalAccess.Administration).Build();
    foreach (var permission in PortalAccess.Catalog)
        options.AddPolicy(permission.Key, policy => policy.RequireAuthenticatedUser().RequireClaim("perm", permission.Key));
});



var app =builder.Build();

app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ChatException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = ex.Status;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

await using (var scope = app.Services.CreateAsyncScope())
{
    await DatabaseInitializer.InitializeAsync(
        scope.ServiceProvider.GetRequiredService<PortalDbContext>(), app.Configuration);
}


if(!minimalHosting) { app.UseSwagger(); app.UseSwaggerUI(); app.UseCors("AllowAll"); }
if(minimalHosting)app.UseForwardedHeaders(new ForwardedHeadersOptions{
    ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto
}); // Default trusted proxies are loopback only; the gateway itself binds to loopback.
app.UseAuthentication();
app.UseAuthorization();
if(minimalHosting)app.UseRateLimiter();
app.UseMiddleware<AiGateway.Infrastructure.MinimalHostingMiddleware>();
app.MapGet("/health/live",()=>Results.Ok(new{status="ok"})).AllowAnonymous();
app.MapGet("/health/ready",async (PortalDbContext db,CancellationToken ct)=>
    await db.Database.CanConnectAsync(ct)?Results.Ok(new{status="ready"}):Results.StatusCode(503)).AllowAnonymous();
app.MapControllers();
app.Run();
