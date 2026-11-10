using System.Security.Claims;
using AirlineReservationSystem.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Service.Helpers.DTOs.Newsletter;
using Service.Services.Interfaces;

namespace AirlineReservationSystem.Controllers.Admin;

/// <summary>
/// Promotion e-mail campaigns. Saving a promo code never sends anything: an admin previews the e-mail, confirms it, and it is sent in the
/// background to confirmed newsletter subscribers only. A promo code has at most one campaign.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.Admin)]
[Produces("application/json")]
public class AdminCampaignsController : ControllerBase
{
    private readonly ICampaignService _campaigns;

    public AdminCampaignsController(ICampaignService campaigns)
    {
        _campaigns = campaigns;
    }

    /// <summary>The e-mail as it would be sent (language az, en or ru), built from the saved promotion, and how many subscribers would get it.</summary>
    [HttpGet("api/admin/promo-codes/{id:int}/campaign-preview")]
    [ProducesResponseType<CampaignPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignPreviewDto>> Preview(int id, [FromQuery] string? language, CancellationToken cancellationToken)
        => Ok(await _campaigns.PreviewAsync(id, language, cancellationToken));

    /// <summary>Confirms and queues the campaign (202). Needs the preview hash and confirm=true; 409 when the promotion changed or a campaign exists.</summary>
    [HttpPost("api/admin/promo-codes/{id:int}/campaign")]
    [ProducesResponseType<CampaignDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CampaignDto>> Create(int id, CreateCampaignRequestDto request, CancellationToken cancellationToken)
    {
        int? adminId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out var parsed) ? parsed : null;
        return Accepted(await _campaigns.CreateAsync(id, request, adminId, cancellationToken));
    }

    /// <summary>Recent campaigns with their delivery status: sent, failed, skipped and still waiting.</summary>
    [HttpGet("api/admin/campaigns")]
    [ProducesResponseType<IReadOnlyList<CampaignDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CampaignDto>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _campaigns.GetRecentAsync(cancellationToken));

    [HttpGet("api/admin/campaigns/{id:int}")]
    [ProducesResponseType<CampaignDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CampaignDto>> Get(int id, CancellationToken cancellationToken)
        => Ok(await _campaigns.GetAsync(id, cancellationToken));
}
