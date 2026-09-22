namespace Portal.AI.Gateway.Application.DTOs;

public class AskSqlResponse
{
    public string Sql { get; set; } = "";
    public object? Rows { get; set; }
}
