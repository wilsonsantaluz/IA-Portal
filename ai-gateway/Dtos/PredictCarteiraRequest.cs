namespace Portal.AI.Gateway.Application.DTOs;

public class PredictCarteiraRequest
{
    public string CarteiraId { get; set; } = "";
    public int MesesProjecao { get; set; } = 6;
}
