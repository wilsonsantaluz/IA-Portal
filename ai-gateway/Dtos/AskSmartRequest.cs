namespace AiGateway.Dtos
{
    public class AskSmartRequest
    {
        public string Pergunta { get; set; } = default!;

        public string? SessionId { get; set; }
        public Guid? RequestId { get; set; }
        public int? SourceId { get; set; }
        public DateOnly? ReferenceDate { get; set; }

        public string? Model { get; set; }

        public string? UserId { get; set; }


        public string? DocumentContext { get; set; }

        




    }

}
