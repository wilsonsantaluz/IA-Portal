namespace AiGateway.Dtos
{
    public class AskSmartRequest
    {
        public string Pergunta { get; set; } = default!;

        public string? SessionId { get; set; }

        public string? Model { get; set; }

        public string? UserId { get; set; }


        public string? DocumentContext { get; set; }

        




    }

}
