using AiGateway.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;

namespace AiGateway.Controllers.Chat;

// Compatibility routes retain session checks but never infer or execute free SQL.
[ApiController, Authorize(Policy = PortalAccess.Chat), Route("api/sql")]
public sealed class SqlController(IChatRepository chats) : ControllerBase
{
    [HttpPost("ask")]
    public Task<IActionResult> Ask(AskSqlRequest request, CancellationToken ct) => Disabled(request,ct);
    [HttpPost("ask-rag")]
    public Task<IActionResult> AskRag(AskSqlRequest request, CancellationToken ct) => Disabled(request,ct);
    private async Task<IActionResult> Disabled(AskSqlRequest request, CancellationToken ct)
    {
        if (request.SessionId is not null) {
            if (!Guid.TryParse(request.SessionId,out var id)) throw new ChatException(400,"Conversa invalida.");
            await chats.GetSessionAsync(id,ct);
        }
        throw new ChatException(409,"SQL livre desabilitado. Use os planos de uma fonte autorizada em /api/data.");
    }
}
