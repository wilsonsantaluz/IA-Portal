using AiGateway.Dtos;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
namespace Portal.AI.Gateway.Infrastructure;

public sealed class AiProviderCatalog(PortalDbContext db, IConfiguration cfg)
{
    public static readonly string[] Ids = ["deepseek", "openai", "ollama", "litellm"];
    private static string Prefix(string id) => id.ToUpperInvariant();
    private string Option(string key, string fallback) => string.IsNullOrWhiteSpace(cfg[key]) ? fallback : cfg[key]!;
    private static void Known(string id) { if (!Ids.Contains(id)) throw new ChatException(400, "Provedor inválido."); }
    public string Key(string id) { Known(id); return cfg[$"{Prefix(id)}_API_KEY"] ?? ""; }
    private string Endpoint(string id) => cfg[$"{Prefix(id)}_BASE_URL"] ?? (id == "litellm" ? "http://litellm:4000/v1/" : id == "deepseek" ? "https://api.deepseek.com/" : id == "openai" ? "https://api.openai.com/v1/" : "http://host.docker.internal:11434/");
    private static Uri Url(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http") || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ChatException(400, "Endpoint inválido: use HTTP(S) sem credenciais, query ou fragmento.");
        return uri;
    }
    private static string Origin(string endpoint) => Url(endpoint).GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    public string ValidateEndpoint(string id, string endpoint)
    {
        Known(id); var uri = Url(endpoint);
        var allowed = Option($"{Prefix(id)}_ALLOWED_ORIGINS", id == "ollama" ? "http://localhost:11434,http://host.docker.internal:11434,http://ollama:11434" : Origin(Endpoint(id))).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Origin);
        if (!allowed.Contains(Origin(endpoint))) throw new ChatException(400, "Destino não permitido pelo servidor para este provedor.");
        // Ollama uses root-relative API routes; remote adapters may use a configured base path (e.g. /v1/).
        if (id == "ollama" && uri.AbsolutePath != "/") throw new ChatException(400, "O endpoint Ollama deve apontar para a raiz do serviço.");
        return endpoint.TrimEnd('/') + "/";
    }
    public string Billing(string id, string endpoint)
    {
        if (id == "litellm") return "unknown";
        if (id != "ollama") return "external";
        var local = Option("OLLAMA_LOCAL_ORIGINS", "http://localhost:11434,http://host.docker.internal:11434,http://ollama:11434").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(Origin);
        return local.Contains(Origin(endpoint)) ? "local" : "unknown";
    }
    private static string[] Models(string[] values)
    {
        if (values is null || values.Length > 50 || values.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 150 || s.Any(char.IsControl))) throw new ChatException(400, "Informe até 50 identificadores de modelo válidos.");
        return values.Select(s => s.Trim()).Distinct().ToArray();
    }
    public async Task<ProviderInfo> Get(string id, CancellationToken ct)
    {
        Known(id); var stored = await db.AiProviderSettings.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, ct);
        var defaultModel = cfg[$"{Prefix(id)}_DEFAULT_MODEL"] ?? (id == "litellm" ? "fast" : id == "deepseek" ? "deepseek-chat" : id == "openai" ? "gpt-4.1" : "");
        var models = stored is not null ? JsonSerializer.Deserialize<string[]>(stored.ModelsJson)! : Models(Option($"{Prefix(id)}_MODELS", defaultModel).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        var endpoint = stored?.Endpoint ?? Endpoint(id); string? error = null;
        try { endpoint = ValidateEndpoint(id, endpoint); } catch (ChatException ex) { error = ex.Message; try { Url(endpoint); } catch { endpoint = ""; } }
        return new(id, stored?.Enabled ?? (bool.TryParse(cfg[$"{Prefix(id)}_ENABLED"], out var enabled) ? enabled : id == "deepseek"), id == "ollama" || !string.IsNullOrWhiteSpace(Key(id)), endpoint, models,
            stored?.DefaultModel ?? (models.Contains(defaultModel) ? defaultModel : models.FirstOrDefault() ?? ""), stored?.Version ?? 0, true, error is null ? Billing(id, endpoint) : "unknown", $"{Prefix(id)}_API_KEY", error);
    }
    public async Task<List<ProviderInfo>> List(bool admin, CancellationToken ct)
    {
        var result = new List<ProviderInfo>(); foreach (var id in Ids) { var info = await Get(id, ct); if (admin || info.Enabled && info.Configured && info.Models.Length > 0 && info.ConfigurationError is null) result.Add(info); } return result;
    }
    public async Task<ProviderInfo> Resolve(string provider, string model, CancellationToken ct)
    {
        var info = await Get(provider, ct);
        if (!info.Enabled || !info.Configured || info.ConfigurationError is not null || !info.Models.Contains(model)) throw new ChatException(409, "Provedor/modelo indisponível. Escolha explicitamente uma opção habilitada.");
        return info;
    }
    public async Task<ModelSelection> Preference(int user, CancellationToken ct)
    {
        var preference = await db.OrganizationAiPreferences.AsNoTracking().SingleOrDefaultAsync(p => p.OrganizationId == db.OrganizationId && p.UserId == user, ct);
        var organization = await db.OrganizationAiDefaults.AsNoTracking().SingleOrDefaultAsync(p => p.OrganizationId == db.OrganizationId, ct);
        var global = await db.AiSystemDefaults.AsNoTracking().SingleOrDefaultAsync(p => p.Id == 1, ct);
        var provider = preference?.Provider ?? organization?.Provider ?? global?.Provider ?? Option("AI_DEFAULT_PROVIDER", "deepseek");
        var model = preference?.Model ?? organization?.Model ?? global?.Model ?? (await Get(provider, ct)).DefaultModel;
        return new(provider, model);
    }
    public async Task<ModelSelection> Default(int user, CancellationToken ct) { var selected = await Preference(user, ct); await Resolve(selected.Provider, selected.Model, ct); return selected; }
    public async Task SetPreference(int user, ModelSelection selection, CancellationToken ct)
    {
        await Resolve(selection.Provider, selection.Model, ct);
        var preference = await db.OrganizationAiPreferences.SingleOrDefaultAsync(p => p.OrganizationId == db.OrganizationId && p.UserId == user, ct);
        if (preference is null) { preference = new() { UserId = user, OrganizationId = db.OrganizationId }; db.OrganizationAiPreferences.Add(preference); }
        preference.Provider = selection.Provider; preference.Model = selection.Model;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw new ChatException(409, "Preferência alterada; atualize a página."); }
    }
    public async Task<ProviderInfo> Save(string id, ProviderUpdate request, CancellationToken ct)
    {
        Known(id); var endpoint = ValidateEndpoint(id, request.Endpoint); var models = Models(request.Models);
        if (request.DefaultModel.Length > 150) throw new ChatException(400, "Modelo padrão inválido.");
        if (request.Enabled && (!models.Contains(request.DefaultModel) || (id != "ollama" && string.IsNullOrWhiteSpace(Key(id))))) throw new ChatException(400, "Habilitar requer credencial no ambiente e um modelo padrão na lista.");
        var item = await db.AiProviderSettings.SingleOrDefaultAsync(p => p.Id == id, ct);
        if (item is null) {
            if (request.Version != 0) throw new ChatException(409, "Configuração alterada; atualize a página.");
            item = new() { Id = id }; db.AiProviderSettings.Add(item);
        } else if (item.Version != request.Version) throw new ChatException(409, "Configuração alterada; atualize a página.");
        item.Enabled = request.Enabled; item.Endpoint = endpoint; item.ModelsJson = JsonSerializer.Serialize(models); item.DefaultModel = request.DefaultModel; item.Version++;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw new ChatException(409, "Configuração alterada; atualize a página."); }
        db.ChangeTracker.Clear(); return await Get(id, ct);
    }
    public async Task<object> Global(CancellationToken ct)
    {
        var global = await db.AiSystemDefaults.AsNoTracking().SingleOrDefaultAsync(p => p.Id == 1, ct);
        var provider = global?.Provider ?? Option("AI_DEFAULT_PROVIDER", "deepseek");
        return new { provider, model = global?.Model ?? (await Get(provider, ct)).DefaultModel, version = global?.Version ?? 0 };
    }
    public async Task SetGlobal(DefaultUpdate request, CancellationToken ct)
    {
        await Resolve(request.Provider, request.Model, ct);
        var item = await db.AiSystemDefaults.SingleOrDefaultAsync(p => p.Id == 1, ct);
        if (item is null) { if (request.Version != 0) throw new ChatException(409, "Padrão alterado; atualize a página."); item = new() { Id = 1 }; db.AiSystemDefaults.Add(item); }
        else if (item.Version != request.Version) throw new ChatException(409, "Padrão alterado; atualize a página.");
        item.Provider = request.Provider; item.Model = request.Model; item.Version++;
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { throw new ChatException(409, "Padrão alterado; atualize a página."); }
    }
}
