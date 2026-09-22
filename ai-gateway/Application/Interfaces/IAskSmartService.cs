using AiGateway.Dtos;
using AiGateway.Infrastructure.Sse;
using System.Runtime.Intrinsics.X86;

namespace AiGateway.Application.Interfaces
{
    public interface IAskSmartService
    {
        public Task<string> AskAsync(AskSmartRequest req, HttpSseEventEmitter sse);
        
    }
}
