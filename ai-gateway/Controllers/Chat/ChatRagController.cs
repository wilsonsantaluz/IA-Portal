using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;

using Microsoft.AspNetCore.Mvc;

namespace AiGateway.Controllers.Chat
{
    /// <summary>
    /// Endpoint de chat baseado em RAG sobre a Knowledge Base.
    /// Não interfere no ChatBotController atual.
    /// </summary>
    [ApiController]
    [Route("api/chatkb")]
    public class ChatRagController : ControllerBase
    {
        private readonly IAskSmartService _ragService;
        private readonly PortalDbContext _db;


        private readonly IChatRepository _chatRepo;

        public ChatRagController(IAskSmartService ragService, PortalDbContext db, IChatRepository chatRepo)
        {
            _db = db;
            _chatRepo = chatRepo;
            _ragService = ragService;
        }

        [HttpPost("rate")]
        public async Task<IActionResult> Rate([FromBody] ChatKbRatingDto dto)
        {
            var entity = new ChatKbRating
            {
                UserId = dto.UserId,
                Question = dto.Question,
                Sources = dto.Sources,
                Rating = dto.Rating,
                CreatedAt = DateTime.UtcNow
            };

            _db.ChatKbRatings.Add(entity);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Rating saved" });
        }

   
    }
}
