using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Portal.AI.Gateway.Infrastructure;
using System.Net;
using System.Text;
using System.Text.Json;

static class VectorIsolationChecks
{
    public static async Task Run(PortalDbContext db)
    {
        var active=new KnowledgeBaseItem{KeyName="vector-active",Content="AUTHORIZED_VECTOR_DOCUMENT",Section="Support",Title="Authorized",IsActive=true};
        var inactive=new KnowledgeBaseItem{KeyName="vector-inactive",Content="INACTIVE_VECTOR_DOCUMENT",Section="Support",IsActive=false};
        db.KnowledgeBaseItems.AddRange(active,inactive);await db.SaveChangesAsync();
        using var handler=new VectorHandler(active.Id,inactive.Id);
        using var http=new HttpClient(handler);
        var cfg=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Qdrant:Endpoint","http://vector.test/"}}).Build();
        var reranker=new RecordingReranker();
        var service=new KnowledgeBaseRagService(db,new TestEmbedding(),new IQdrantClient(http,cfg),cfg,null!,reranker);
        var answer=await service.AskAsync(new AskKbRagRequest{Prompt="Synthetic tenant lookup"},false);
        if(!handler.Scoped || reranker.Seen.Count!=1 || reranker.Seen.Any(x=>x.Contains("FORBIDDEN")) ||
           !answer.texto.Contains("AUTHORIZED_VECTOR_DOCUMENT") || answer.texto.Contains("FORBIDDEN") || answer.texto.Contains("Human content preserved"))
            throw new Exception("Vector tenant filtering/revalidation failed");
        Console.WriteLine("PASS: vector search sends tenant filter; foreign, missing-scope, mismatched item and inactive payloads excluded before reranking; context comes from authorized SQL rows.");
    }
    sealed class TestEmbedding:IOnnxEmbeddingService {public float[] Generate(string text)=>[1,0,0];}
    sealed class RecordingReranker:IRerankerService {
        public List<string> Seen {get;}=[];
        public Task<float> ComputeScoreAsync(string query,string passage){Seen.Add(passage);return Task.FromResult(1f);}
    }
    sealed class VectorHandler(int active,int inactive):HttpMessageHandler {
        public bool Scoped {get;private set;}
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            if(request.Method==HttpMethod.Get)return new(HttpStatusCode.OK){Content=new StringContent("{}")};
            using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var filter=body.RootElement.GetProperty("filter").GetProperty("must")[0];
            Scoped=filter.GetProperty("key").GetString()=="organizationId" && filter.GetProperty("match").GetProperty("value").GetInt32()==2;
            object Point(int item,int? org,string title){var payload=new Dictionary<string,object>{{"itemId",item},{"title",title}};if(org.HasValue)payload["organizationId"]=org.Value;return new{score=1,payload};}
            var json=JsonSerializer.Serialize(new{result=new[]{Point(active,1,"FORBIDDEN foreign"),Point(1,2,"FORBIDDEN relational mismatch"),Point(active,null,"FORBIDDEN missing scope"),Point(inactive,2,"FORBIDDEN inactive"),Point(active,2,"AUTHORIZED_VECTOR_HEADER")}});
            return new(HttpStatusCode.OK){Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
}
