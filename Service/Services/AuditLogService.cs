using Domain.Entities;
using Microsoft.Extensions.Logging;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Account;
using Service.Services.Interfaces;

namespace Service.Services;

public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _logs;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository logs, ILogger<AuditLogService> logger)
    {
        _logs = logs;
        _logger = logger;
    }

    public Task StageAsync(AuditActor actor, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default)
        => _logs.AddAsync(Create(actor, action, targetType, targetId, details), cancellationToken);

    public async Task RecordAsync(AuditActor actor, string action, string targetType, string? targetId = null, string? details = null, CancellationToken cancellationToken = default)
    {
        try
        {
            await _logs.AddAsync(Create(actor, action, targetType, targetId, details), cancellationToken);
            await _logs.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Only the action's name goes to the log: no details, no addresses.
            _logger.LogError(exception, "The audit entry {Action} could not be saved.", action);
        }
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAsync(string? action, int? actorUserId, string? targetType, int take, CancellationToken cancellationToken = default)
        => (await _logs.SearchAsync(action, actorUserId, targetType, Math.Clamp(take, 1, 500), cancellationToken))
            .Select(a => new AuditLogDto(a.Id, a.CreatedAt, a.ActorUserId, a.ActorRole, a.Action, a.TargetType, a.TargetId, a.Details))
            .ToList();

    private static AuditLog Create(AuditActor actor, string action, string targetType, string? targetId, string? details)
        => new()
        {
            ActorUserId = actor.UserId,
            ActorRole = Clip(actor.Role, 20),
            Action = Clip(action, 60)!,
            TargetType = Clip(targetType, 40)!,
            TargetId = Clip(targetId, 64),
            Details = Clip(details, 500)
        };

    // Text from a request must not break the table or the log lines read later: one line, no control characters, within the column.
    private static string? Clip(string? value, int max)
    {
        if (value is null)
        {
            return null;
        }

        var clean = new string(value.Select(c => char.IsControl(c) ? ' ' : c).ToArray()).Trim();
        return clean.Length <= max ? clean : clean[..max];
    }
}
