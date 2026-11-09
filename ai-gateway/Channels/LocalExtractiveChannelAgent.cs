using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiGateway.Infrastructure.Qdrant.Services.Chats;

namespace AiGateway.Channels;

public sealed record ChannelAnswer(string Text,string SourcesJson);
public interface IChannelAgent {
    Task<ChannelAnswer> Answer(string question,int[] authorizedDocuments,CancellationToken ct);
}
public sealed class LocalExtractiveChannelAgent(LocalKnowledgeRetriever retrieval):IChannelAgent {
    public async Task<ChannelAnswer> Answer(string question,int[] authorizedDocuments,CancellationToken ct) {
        var documents=await retrieval.Retrieve(question,authorizedDocuments,ct);
        var sources=documents.Take(3).Select(d=>new{d.Id,d.Title,d.KeyName,d.CreatedAt,
            contentHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(d.Content)))}).ToArray();
        // Documents are quoted evidence, never executable instructions or tool authorizations.
        var text=documents.Count==0?"Não encontrei evidências nos documentos autorizados. Solicite atendimento humano.":
            "[Resposta simulada — agente extrativo, sem LLM]\n"+string.Join("\n",documents.Take(3).Select(d=>$"[{d.Id}] {d.Title}: {d.Content[..Math.Min(d.Content.Length,700)]}"));
        return new(text,JsonSerializer.Serialize(sources));
    }
}
