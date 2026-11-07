namespace AiGateway.Application.Interfaces;

// Per-request output for the existing SSE controller, never a source of authorization.
public sealed class ChatGrounding
{
    public string? DirectAnswer { get; set; }
    public string Appendix { get; set; } = "";
    public List<string> DocumentSources { get; } = [];
}
