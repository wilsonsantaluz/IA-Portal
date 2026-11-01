using System.ComponentModel.DataAnnotations;
using Portal.AI.Gateway.Infrastructure;
namespace AiGateway.Dtos;
public record SessionDto(Guid Id, string Title, string TitleSource, DateTime CreatedAt, DateTime UpdatedAt, string? Provider, string? Model, int Version);
public record MessageDto(long Id, Guid SessionId, Guid? RequestId, string Role, string Content, DateTime CreatedAt, string Status, string? Provider, string? Model);
public record SessionPage(IReadOnlyList<SessionDto> Items, string? NextCursor);
public record MessagePage(IReadOnlyList<MessageDto> Items, string? NextCursor);
public record RenameSessionRequest([Required, StringLength(120, MinimumLength=1)] string Title, int Version);
public record TurnReservation(ChatMessage UserMessage, ChatMessage? Answer, bool Replayed);
