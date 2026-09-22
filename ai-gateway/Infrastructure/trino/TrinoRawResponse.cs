using System.Text.Json.Serialization;

namespace Portal.AI.Gateway.Infrastructure.Trino;

public class TrinoRawResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("infoUri")]
    public string? InfoUri { get; set; }

    [JsonPropertyName("nextUri")]
    public string? NextUri { get; set; }

    [JsonPropertyName("columns")]
    public List<TrinoRawColumn>? Columns { get; set; }

    [JsonPropertyName("data")]
    public List<List<object>>? Data { get; set; }

    [JsonPropertyName("stats")]
    public TrinoRawStats? Stats { get; set; }

    [JsonPropertyName("warnings")]
    public List<object>? Warnings { get; set; }
}

public class TrinoRawColumn
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

public class TrinoRawStats
{
    [JsonPropertyName("state")]
    public string State { get; set; } = "";

    [JsonPropertyName("queued")]
    public bool Queued { get; set; }

    [JsonPropertyName("scheduled")]
    public bool Scheduled { get; set; }

    [JsonPropertyName("progressPercentage")]
    public float? ProgressPercentage { get; set; }

    [JsonPropertyName("runningPercentage")]
    public float? RunningPercentage { get; set; }

    [JsonPropertyName("queuedTimeMillis")]
    public long? QueuedTimeMillis { get; set; }

    [JsonPropertyName("elapsedTimeMillis")]
    public long? ElapsedTimeMillis { get; set; }

    [JsonPropertyName("completedSplits")]
    public int? CompletedSplits { get; set; }
}
