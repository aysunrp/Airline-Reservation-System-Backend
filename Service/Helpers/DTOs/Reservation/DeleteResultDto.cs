namespace Service.Helpers.DTOs.Reservation;

/// <summary>What a delete request ended up doing: records with history are kept and switched off instead of being removed.</summary>
public enum DeleteOutcome
{
    Deleted,
    Deactivated
}

public record DeleteResultDto(string Outcome);
