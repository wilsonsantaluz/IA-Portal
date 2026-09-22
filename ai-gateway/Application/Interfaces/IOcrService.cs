namespace Portal.AI.Gateway.Application.Interfaces;
public interface IOcrService{
 Task<string> ExtractTextAsync(Stream file,string name,string contentType,CancellationToken ct=default);
}
