using AiGateway.Security;
using Microsoft.EntityFrameworkCore;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Channels;

public sealed class ChannelWorker(IServiceScopeFactory scopes,ILogger<ChannelWorker> logger) : BackgroundService {
    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        while(!stoppingToken.IsCancellationRequested){
            try{
                await using var registry=scopes.CreateAsyncScope();var db=registry.ServiceProvider.GetRequiredService<PortalDbContext>();
                var bridge=registry.ServiceProvider.GetRequiredService<BrowserBridge>();
                if(bridge.Enabled){
                    var inactive=await db.ChannelConnections.IgnoreQueryFilters().AsNoTracking().Where(c=>c.Provider=="whatsapp-browser"
                        &&db.Organizations.Any(o=>o.Id==c.OrganizationId&&!o.IsActive)).Select(c=>new{c.OrganizationId,c.Id}).ToArrayAsync(stoppingToken);
                    foreach(var c in inactive)try{await bridge.Call(c.OrganizationId,c.Id,"stop",new{},stoppingToken);}catch(Exception)when(!stoppingToken.IsCancellationRequested){logger.LogWarning("Inactive organization browser could not be paused.");}
                }
                // Registry-only scan. Re-enter a fresh validated tenant scope before any content access.
                var organizations=await db.ChannelConnections.IgnoreQueryFilters().AsNoTracking()
                    .Where(c=>db.Organizations.Any(o=>o.Id==c.OrganizationId&&o.IsActive)).Select(c=>c.OrganizationId).Distinct().ToListAsync(stoppingToken);
                foreach(var organization in organizations){
                    await using var scope=scopes.CreateAsyncScope();scope.ServiceProvider.GetRequiredService<OrganizationScope>().Id=organization;
                    await scope.ServiceProvider.GetRequiredService<ChannelService>().Process(stoppingToken);
                    try { await scope.ServiceProvider.GetRequiredService<BrowserChannelPump>().Tick(stoppingToken); }
                    catch(Exception)when(!stoppingToken.IsCancellationRequested){logger.LogWarning("Browser channel sync unavailable; tenant content omitted.");}
                }
            }catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception){logger.LogWarning("ChannelWorker iteration failed; content and credentials omitted.");}
            await Task.Delay(TimeSpan.FromSeconds(2),stoppingToken);
        }
    }
}
