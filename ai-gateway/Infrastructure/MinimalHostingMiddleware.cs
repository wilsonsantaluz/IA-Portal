namespace AiGateway.Infrastructure;

// Opt-in deployment profile. Authentication/authorization runs before these gates.
public sealed class MinimalHostingMiddleware(RequestDelegate next,IConfiguration config) {
    private static readonly SemaphoreSlim ChatSlots=new(2,2);
    public async Task InvokeAsync(HttpContext context) {
        if(config.GetValue<bool>("Hosting:Minimal")) {
            var path=context.Request.Path;
            if(path.Equals(new PathString("/api/data/sources")) && HttpMethods.IsGet(context.Request.Method)) {
                await context.Response.WriteAsJsonAsync(Array.Empty<object>());return;
            }
            if(path.StartsWithSegments("/api/ocr") || path.StartsWithSegments("/api/sql")
                || path.StartsWithSegments("/api/data") || path.StartsWithSegments("/api/data-connectors")
                || path.StartsWithSegments("/api/kb/reindex-all")) {
                context.Response.StatusCode=503;
                await context.Response.WriteAsJsonAsync(new{error="Recurso indisponível nesta instalação mínima. Use o chat documental e a base de conhecimento textual."});return;
            }
            if(path.Equals(new PathString("/api/ia/ask/stream")) || path.Equals(new PathString("/api/chatBot/ask"))) {
                if(!await ChatSlots.WaitAsync(0,context.RequestAborted)) {
                    context.Response.StatusCode=429;
                    await context.Response.WriteAsJsonAsync(new{error="Duas respostas já estão em andamento. Aguarde e tente novamente."});return;
                }
                try{await next(context);}finally{ChatSlots.Release();}
                return;
            }
        }
        await next(context);
    }
}
