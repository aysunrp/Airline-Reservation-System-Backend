namespace Service.Helpers.DTOs.Reservation;

/// <summary>
/// The passenger rules the booking pages show and check ahead of the server: the same limits the server applies.
/// A passenger is an infant when younger than <see cref="InfantMaxAgeYears"/>, a child when younger than
/// <see cref="ChildMaxAgeYears"/>, an adult from then on; the age is counted on the day of the last flight of the trip.
/// </summary>
public record PassengerRulesDto(int InfantMaxAgeYears, int ChildMaxAgeYears, int MaxPassengers);
