namespace AiGateway.Dtos;

public sealed record ModelSelection(string Provider, string Model);
public sealed record ChangeModelRequest(string Provider, string Model, int Version);
public sealed record ProviderUpdate(bool Enabled, string Endpoint, string[] Models, string DefaultModel, int Version);
public sealed record DefaultUpdate(string Provider, string Model, int Version);
public sealed record ProviderInfo(string Id, bool Enabled, bool Configured, string Endpoint, string[] Models,
    string DefaultModel, int Version, bool Streaming, string ApiBilling, string KeyEnvironmentVariable, string? ConfigurationError = null);
