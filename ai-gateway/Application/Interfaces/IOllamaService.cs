using System.Runtime.CompilerServices;

namespace Portal.AI.Gateway.Application.Interfaces;
public interface IOllamaService{
 Task<string> GenerateAsync(string model,string prompt,CancellationToken ct=default);
    public IAsyncEnumerable<string> GenerateStreamAsync(
          string model,
          string prompt,
          [EnumeratorCancellation] CancellationToken ct = default);

}
