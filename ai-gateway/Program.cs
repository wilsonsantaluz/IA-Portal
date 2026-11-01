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


var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader());
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddScoped<IKnowledgeBaseService, KnowledgeBaseService>();
builder.Services.AddHttpClient<TrinoRestClient>();

builder.Services.AddHttpClient<TrinoRestClient>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(50);
});

builder.Services.AddHttpClient<OllamaService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(50);
});


builder.Services.AddScoped<ITrinoQueryService, TrinoQueryService>();
builder.Services.AddScoped<ITrinoRestClient, TrinoRestClient>();
builder.Services.AddScoped<ITrinoMetadataService, TrinoMetadataService>();
//builder.Services.AddScoped<IOllamaService, OllamaService>();
builder.Services.AddScoped<DeepSeekService>();
builder.Services.AddScoped<IOllamaService>(services => services.GetRequiredService<DeepSeekService>());
builder.Services.AddScoped<IChatTitleGenerator>(services => services.GetRequiredService<DeepSeekService>());
builder.Services.AddScoped<ChatTitleService>();
//builder.Services.AddScoped<IOllamaService, OpenAiService>();
builder.Services.AddScoped<IMlService, MlService>();
builder.Services.AddScoped<IOcrService, OcrService>();
builder.Services.AddScoped<IDataRagService, DataRagService>();

if (builder.Configuration.GetValue<bool>("Chat:UseAdvancedRag"))
    builder.Services.AddScoped<IAskSmartService, VectorChatContextService>();
else
    builder.Services.AddScoped<IAskSmartService, LocalChatContextService>();
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
                var access = await PortalAccess.ReadAsync(db, id, context.HttpContext.RequestAborted);
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


app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
