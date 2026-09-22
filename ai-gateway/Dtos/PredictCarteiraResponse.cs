namespace Portal.AI.Gateway.Application.DTOs;

public class PredictCarteiraResponse
{
    public string CarteiraId { get; set; } = "";
    public List<Dictionary<string, object?>> Previsoes { get; set; } = new();
    public string Explicacao { get; set; } = "";
}
