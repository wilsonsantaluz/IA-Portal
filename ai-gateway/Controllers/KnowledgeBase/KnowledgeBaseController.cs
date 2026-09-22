using AiGateway.Application.Interfaces;
using AiGateway.Dtos;
using AiGateway.Infrastructure.Qdrant;
using Portal.AI.Gateway.Application.DTOs;
using Portal.AI.Gateway.Application.Interfaces;
using Portal.AI.Gateway.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AiGateway.Controllers.KnowledgeBase;

[ApiController]
[Route("api/kb")]
public class KnowledgeBaseController : ControllerBase
{
    private readonly IKnowledgeBaseRepository _repo;
    private readonly IKnowledgeBaseService _knowledgeBaseService;
 
    private readonly PortalDbContext _db;               // EF
    private readonly IQdrantClient _qdrant;                // Qdrant
    private readonly IConfiguration _cfg;



    public KnowledgeBaseController(
        IKnowledgeBaseRepository repo,
        IKnowledgeBaseService KnowledgeBaseService,       
        PortalDbContext db,
        IQdrantClient qdrant,
        IConfiguration cfg

        )
    {
        _repo = repo;
        _knowledgeBaseService = KnowledgeBaseService;
      
        _db = db;
        _qdrant = qdrant;
        _cfg = cfg;
    }


    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<object>>> GetAll(CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(ct);

        var dto = items.Select(x => new
        {
            x.Id,
            x.KeyName,
            x.Section,
            x.Subsection,
            x.Title,
            x.IsActive
        });

        return Ok(dto);
    }

 
    [HttpPost("save")]
    public async Task<ActionResult> Save([FromBody] KnowledgeBaseItemDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.KeyName))
            return BadRequest("KeyName é obrigatório.");
        if (string.IsNullOrWhiteSpace(dto.Content))
            return BadRequest("Content é obrigatório.");

        KnowledgeBaseItem entity;

        if (dto.Id.HasValue && dto.Id.Value > 0)
        {
            entity = await _repo.GetByIdAsync(dto.Id.Value, ct)
                     ?? new KnowledgeBaseItem();
        }
        else
        {
            entity = new KnowledgeBaseItem();
        }

        entity.KeyName = dto.KeyName;
        entity.Section = dto.Section;
        entity.Subsection = dto.Subsection;
        entity.Title = dto.Title;
        entity.Content = dto.Content;
        if (dto.Id == null)
            entity.IsActive = true;

        var saved = await _repo.SaveAsync(entity, ct);

        return Ok(new
        {
            saved.Id,
            saved.KeyName,
            saved.Title,
            saved.IsActive
        });
    }

   
    [HttpPost("toggle/{id:int}")]
    public async Task<ActionResult> Toggle(int id, CancellationToken ct)
    {
        await _repo.ToggleActiveAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult> GetById(int id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        if (item == null) return NotFound();

        return Ok(new
        {
            item.Id,
            item.Section,
            item.Subsection,
            item.Title,
            item.KeyName,
            item.Content,
            item.IsActive
        });
    }
    [HttpPost("upsert")]
    public async Task<ActionResult> Upsert([FromBody] UpsertKbItemRequest req, CancellationToken ct)
    {
        try
        {
            var id = await _knowledgeBaseService.UpsertAsync(req, ct);
            return Ok(new { id, message = "KB salva com sucesso" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                error = "Erro ao salvar KnowledgeBaseItem",
                detail = ex.Message
            });
        }
    }
    [HttpGet("sections")]
    public async Task<ActionResult<IEnumerable<string>>> GetSections(CancellationToken ct)
    {
        var sections = await _repo.GetAllAsync(ct);
        var result = sections
            .Select(x => x.Section)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        return Ok(result);
    }
    [HttpPost("reindex-all")]
    public async Task<IActionResult> ReindexAll(CancellationToken ct)
    {
        if (!_cfg.GetValue<bool>("Chat:UseAdvancedRag"))
            return BadRequest(new { error = "Ative Chat:UseAdvancedRag e configure os modelos para reindexar." });
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var collection = _cfg["Qdrant:Collection"] ?? "kb_chunks";
            var vectorSize = int.Parse(_cfg["Embedding:Dimension"] ?? "768");          
            Console.WriteLine($"Recriando colleção Qdrant");
            await _qdrant.EnsureCollectionAsync(collection, vectorSize);         
            Console.WriteLine($"Carregando dados");
            var items = await _db.KnowledgeBaseItems
                .Where(x => x.IsActive)
                .OrderBy(x => x.Id)
                .ToListAsync(ct);

            int ok = 0;
            int fail = 0;
            foreach (var item in items)
            {
                try
                {
                    await _knowledgeBaseService.ReindexItemInQdrantAsync(item, ct);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    Console.WriteLine($"Erro ao reindexar item {item.Id}: {ex}");
                }
            }
            sw.Stop();

            return Ok(new
            {
                total = items.Count,
                sucesso = ok,
                falhas = fail,
                duracao_segundos = sw.Elapsed.TotalSeconds
            });
        }
        catch (Exception ex)
        {

            return Ok(new
            {
                falhas = ex.Message
            });           

        }
    }
}
