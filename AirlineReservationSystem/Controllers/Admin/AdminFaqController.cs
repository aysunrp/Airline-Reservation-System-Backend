using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers;
using Service.Helpers.DTOs.Cms;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>The questions and answers of the support page, managed in the admin panel (list, detail, create, edit, delete).</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Route("api/admin/faq")]
[Produces("application/json")]
public class AdminFaqController : ControllerBase
{
    private readonly IAdminFaqService _faq;
    private readonly IAuditLogService _audit;

    public AdminFaqController(IAdminFaqService faq, IAuditLogService audit)
    {
        _faq = faq;
        _audit = audit;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FaqItemDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FaqItemDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _faq.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    [ProducesResponseType<FaqItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqItemDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _faq.GetAsync(id, cancellationToken));

    [HttpPost]
    [ProducesResponseType<FaqItemDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FaqItemDto>> Create([FromBody] FaqItemRequestDto request, CancellationToken cancellationToken)
    {
        var created = await _faq.CreateAsync(request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FaqItemCreated, "FaqItem", created.Id.ToString(), created.Question, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<FaqItemDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqItemDto>> Update(int id, [FromBody] FaqItemRequestDto request, CancellationToken cancellationToken)
    {
        var updated = await _faq.UpdateAsync(id, request, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FaqItemUpdated, "FaqItem", id.ToString(), updated.Question, cancellationToken);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var existing = await _faq.GetAsync(id, cancellationToken);
        await _faq.DeleteAsync(id, cancellationToken);
        await _audit.RecordAsync(User.ToAuditActor(), AuditActions.FaqItemDeleted, "FaqItem", id.ToString(), existing.Question, cancellationToken);
        return NoContent();
    }
}
