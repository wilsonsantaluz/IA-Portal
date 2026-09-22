namespace Portal.AI.Gateway.Application.DTOs;

public class KnowledgeBaseItemDto
{
    public int? Id { get; set; }
    public string KeyName { get; set; } = default!;
    public string Section { get; set; } = string.Empty;
    public string? Subsection { get; set; }
    public string? Title { get; set; }
    public string Content { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}
