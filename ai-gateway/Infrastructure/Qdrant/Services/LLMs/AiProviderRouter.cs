using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using AiGateway.Security;
using System.Runtime.CompilerServices;
namespace AiGateway.Infrastructure.Qdrant.Services.LLMs;

public sealed class AiProviderRouter(IEnumerable<IAiProviderAdapter> adapters, AiProviderCatalog catalog, IHttpContextAccessor http) : IOllamaService, ILlmMetrics, IChatTitleGenerator
{
    private IAiProviderAdapter? current;
    public string ProviderId => current?.ProviderId ?? "";
    public string Model { get; private set; } = "";
    public string ApiBilling { get; private set; } = "external";
    public LlmCallMetrics LastCall => current?.LastCall ?? new();
    public async Task Select(string provider, string model, CancellationToken ct)
    {
        var info = await catalog.Resolve(provider, model, ct);
        current = adapters.Single(a => a.ProviderId == provider); Model = model; ApiBilling = info.ApiBilling;
        // Freeze the validated destination with its billing classification for this call.
        // Administrative changes affect the next selection, never redirect an already accepted call.
        current.ConfigureEndpoint(info.Endpoint);
    }
    private async Task Ready(CancellationToken ct)
    {
        if (current is not null) return;
        var selected = await catalog.Default(PortalAccess.UserId(http.HttpContext!.User), ct); await Select(selected.Provider, selected.Model, ct);
    }
    private static async Task<T> Normalize<T>(Func<Task<T>> call)
    {
        try { return await call(); }
        catch (HttpRequestException) { throw new AiProviderException("network"); }
        catch (IOException) { throw new AiProviderException("interrupted"); }
        catch (System.Text.Json.JsonException) { throw new AiProviderException("invalid_response"); }
    }
    public async Task<string> GenerateAsync(string model, string prompt, CancellationToken ct = default)
    { await Ready(ct); return await Normalize(() => current!.GenerateAsync(Model, prompt, ct)); }
    public async Task<TitleGeneration> GenerateTitleAsync(string model, string question, CancellationToken ct)
    { await Ready(ct); return await Normalize(() => current!.GenerateTitleAsync(Model, question, ct)); }
    public async IAsyncEnumerable<string> GenerateStreamAsync(string model, string prompt, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Ready(ct); await using var iterator = current!.GenerateStreamAsync(Model, prompt, ct).GetAsyncEnumerator(ct);
        while (await Normalize(async () => await iterator.MoveNextAsync())) yield return iterator.Current;
    }
}
