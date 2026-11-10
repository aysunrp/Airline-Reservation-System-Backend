using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.Cms;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// The cards of the home page (the "why" features and the "premium" cabin classes) managed in the admin panel: list, detail, create, edit,
/// delete, and the card photograph uploaded from the computer (replacing or removing it).
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/home-cards")]
[Produces("application/json")]
public class AdminHomeCardsController : ControllerBase
{
    private readonly IAdminHomeCardService _cards;
    private readonly IAuditLogService _audit;

    public AdminHomeCardsController(IAdminHomeCardService cards, IAuditLogService audit)
    {
        _cards = cards;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<HomeCardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<HomeCardDto>>> GetAll([FromQuery] string? section, CancellationToken cancellationToken)
        => Ok(await _cards.GetAllAsync(section, cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<HomeCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HomeCardDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _cards.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<HomeCardDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<HomeCardDto>> Create([FromBody] HomeCardRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _cards.CreateAsync(request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HomeCardCreated, "HomeCard", created.Id.ToString(), $"{created.Section}/{created.Title}", cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<HomeCardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<HomeCardDto>> Update(int id, [FromBody] HomeCardRequestDto request, CancellationToken cancellationToken)
    {
        var updated = await _cards.UpdateAsync(id, request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HomeCardUpdated, "HomeCard", id.ToString(), $"{updated.Section}/{updated.Title}", cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _cards.GetAsync(id, cancellationToken);
        await _cards.DeleteAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HomeCardDeleted, "HomeCard", id.ToString(), $"{existing.Section}/{existing.Title}", cancellationToken);
        return NoContent();
    }

    /// <summary>The photograph, sent as a file (jpg, jpeg, png or webp). The previous uploaded file is removed when nothing else uses it.</summary>
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(12_000_000)]
    [ProducesResponseType<CardImageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CardImageDto>> UploadImage(int id, IFormFile file, CancellationToken cancellationToken)
    {
        var result = await _cards.UploadImageAsync(id, file, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HomeCardImageUpdated, "HomeCard", id.ToString(), result.ImagePath, cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:int}/image")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveImage(int id, CancellationToken cancellationToken)
    {
        await _cards.RemoveImageAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.HomeCardImageRemoved, "HomeCard", id.ToString(), null, cancellationToken);
        return NoContent();
    }
}
