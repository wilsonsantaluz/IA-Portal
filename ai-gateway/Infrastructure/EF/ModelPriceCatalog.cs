using System.Text.Json;
using System.Text.Json.Serialization;
namespace Portal.AI.Gateway.Infrastructure;

public sealed record ModelPrice([property: JsonRequired] string Version, [property: JsonRequired] string Provider,
    [property: JsonRequired] string Model, [property: JsonRequired] DateTimeOffset EffectiveFrom,
    [property: JsonRequired] decimal InputUsdPerMillion, [property: JsonRequired] decimal OutputUsdPerMillion, decimal? CachedInputUsdPerMillion);

public sealed class ModelPriceCatalog
{
    private readonly ModelPrice[] entries;
    public ModelPriceCatalog(IEnumerable<ModelPrice> prices)
    {
        entries = prices.ToArray();
        if (entries.Any(p => p is null || p.EffectiveFrom == default || string.IsNullOrWhiteSpace(p.Version) || p.Version.Length > 120 || string.IsNullOrWhiteSpace(p.Provider) || string.IsNullOrWhiteSpace(p.Model) || p.InputUsdPerMillion < 0 || p.OutputUsdPerMillion < 0 || p.CachedInputUsdPerMillion < 0)
            || entries.GroupBy(p => (p.Provider, p.Model, p.EffectiveFrom)).Any(g => g.Count() > 1)
            || entries.GroupBy(p => p.Version).Any(g => g.Count() > 1))
            throw new InvalidOperationException("Configuração de preços inválida ou duplicada.");
    }
    public static ModelPriceCatalog Load(string path) => new(JsonSerializer.Deserialize<ModelPrice[]>(File.ReadAllText(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidOperationException("Lista de preços inválida."));
    public void Apply(LlmUsage usage)
    {
        if (usage.ApiBilling == "local") { usage.EstimatedCostNanoUsd = 0; usage.PriceVersion = "local-api-no-external-billing"; return; }
        var price = entries.Where(p => p.Provider == usage.Provider && p.Model == usage.Model && p.EffectiveFrom <= new DateTimeOffset(DateTime.SpecifyKind(usage.CreatedAt, DateTimeKind.Utc))).OrderByDescending(p => p.EffectiveFrom).FirstOrDefault();
        if (price is null || usage.Source != "reported" || usage.InputTokens is not long input || usage.OutputTokens is not long output || input < 0 || output < 0) return;
        var cachedRate = price.CachedInputUsdPerMillion ?? price.InputUsdPerMillion;
        if (cachedRate != price.InputUsdPerMillion && usage.CacheTokens is null) return;
        var cache = usage.CacheTokens ?? 0;
        if (cache < 0 || cache > input) return;
        try {
            var nanos = decimal.Round(((input - cache) * price.InputUsdPerMillion + cache * cachedRate + output * price.OutputUsdPerMillion) * 1000m, 0, MidpointRounding.AwayFromZero);
            usage.EstimatedCostNanoUsd = checked((long)nanos);
            usage.PriceVersion = price.Version;
        } catch (OverflowException) { /* Unknown cost is preferable to a fabricated/clipped value. */ }
    }
}
