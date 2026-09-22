namespace Portal.AI.Gateway.Application.DTOs;

public class AskSqlRequest
{
    public string Question { get; set; } = default!;
    public string? SessionId { get; set; }
    public string? UserId { get; set; }
    public string? DocumentContext { get; set; } // texto/analise de PDF atual
    public string? Schema { get; set; }

    public int Limit{ get; set; }

}