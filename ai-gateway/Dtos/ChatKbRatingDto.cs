public class ChatKbRatingDto
{
    public string UserId { get; set; } = "";
    public string Question { get; set; } = "";
    public string Sources { get; set; } = "";
    public int Rating { get; set; }

    public string? Section { get; set; }
    public string? Subsection { get; set; }
    public int? OperationId { get; set; }
}
