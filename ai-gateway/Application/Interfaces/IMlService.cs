namespace Portal.AI.Gateway.Application.Interfaces;
public interface IMlService{
 Task<List<Dictionary<string,object?>>> PredictCarteiraAsync(string carteira,int meses,CancellationToken ct=default);
}
