using Domain.Entities;
using Domain.Enums;
using FluentValidation;
using Microsoft.Extensions.Options;
using Repository.Repositories.Interfaces;
using Service.Helpers.DTOs.Aircraft;
using Service.Helpers.Exceptions;
using Service.Helpers.Mappings;
using Service.Helpers.Settings;
using Service.Services.Interfaces;

namespace Service.Services;

public class AdminAircraftService : IAdminAircraftService
{
    // Front to back.
    private static readonly CabinClass[] CabinOrder = { CabinClass.Business, CabinClass.Comfort, CabinClass.Economy };

    private readonly IAircraftRepository _aircraftRepository;
    private readonly IValidator<AircraftRequestDto> _validator;
    private readonly SeatLayoutSettings _layout;

    public AdminAircraftService(IAircraftRepository aircraftRepository, IValidator<AircraftRequestDto> validator, IOptions<SeatLayoutSettings> layout)
    {
        _aircraftRepository = aircraftRepository;
        _validator = validator;
        _layout = layout.Value;
    }

    public async Task<IReadOnlyList<AircraftDto>> GetAllAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        var aircraft = await _aircraftRepository.GetAllWithSeatsAsync(cancellationToken);
        var term = search?.Trim();

        return aircraft
            .Where(a => string.IsNullOrEmpty(term)
                        || a.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || a.Model.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || a.RegistrationNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.ToDto())
            .ToList();
    }

    public async Task<AircraftDetailsDto> GetDetailsAsync(int id, CancellationToken cancellationToken = default)
        => (await GetAircraftAsync(id, cancellationToken)).ToDetailsDto();

    public async Task<AircraftDetailsDto> CreateAsync(AircraftRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var registration = NormalizeRegistration(request.RegistrationNumber);
        await EnsureRegistrationIsFreeAsync(registration, null, cancellationToken);

        var aircraft = new Aircraft();
        Apply(request, registration, aircraft);
        foreach (var seat in GenerateSeats(request))
        {
            aircraft.Seats.Add(seat);
        }

        await _aircraftRepository.AddAsync(aircraft, cancellationToken);
        await _aircraftRepository.SaveChangesAsync(cancellationToken);

        return aircraft.ToDetailsDto();
    }

    public async Task<AircraftDetailsDto> UpdateAsync(int id, AircraftRequestDto request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        var aircraft = await GetAircraftAsync(id, cancellationToken);
        var registration = NormalizeRegistration(request.RegistrationNumber);
        await EnsureRegistrationIsFreeAsync(registration, id, cancellationToken);

        var current = aircraft.ToDto();
        var layoutChanged = current.BusinessSeats != request.BusinessSeats
                            || current.ComfortSeats != request.ComfortSeats
                            || current.EconomySeats != request.EconomySeats;

        if (layoutChanged)
        {
            // Flight seat inventories point at these seats, so the layout is fixed once the aircraft is in use.
            if (await _aircraftRepository.IsInUseAsync(id, cancellationToken))
            {
                throw new BusinessRuleException("The seat layout cannot be changed while flights or schedules use this aircraft.");
            }

            aircraft.Seats.Clear();
            foreach (var seat in GenerateSeats(request))
            {
                aircraft.Seats.Add(seat);
            }
        }

        Apply(request, registration, aircraft);
        _aircraftRepository.Update(aircraft);
        await _aircraftRepository.SaveChangesAsync(cancellationToken);

        return aircraft.ToDetailsDto();
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var aircraft = await GetAircraftAsync(id, cancellationToken);
        if (await _aircraftRepository.IsInUseAsync(id, cancellationToken))
        {
            throw new BusinessRuleException("This aircraft is used by flights or schedules. Set it to Inactive instead of deleting it.");
        }

        _aircraftRepository.Delete(aircraft);
        await _aircraftRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Numbers the seats row by row: each cabin fills whole rows with its letters (the last row may be partly filled)
    /// and the next cabin starts after the configured gap.
    /// </summary>
    internal List<AircraftSeat> GenerateSeats(AircraftRequestDto request)
    {
        var counts = new Dictionary<CabinClass, int>
        {
            [CabinClass.Business] = request.BusinessSeats,
            [CabinClass.Comfort] = request.ComfortSeats,
            [CabinClass.Economy] = request.EconomySeats
        };

        var seats = new List<AircraftSeat>();
        var row = 1;
        foreach (var cabin in CabinOrder.Where(c => counts[c] > 0))
        {
            var letters = _layout.CabinLetters.TryGetValue(cabin, out var configured)
                ? configured.Where(char.IsLetter).Select(char.ToUpperInvariant).ToArray()
                : Array.Empty<char>();
            if (letters.Length == 0)
            {
                throw new InvalidOperationException($"No seat letters are configured for {cabin}.");
            }

            for (var placed = 0; placed < counts[cabin]; row++)
            {
                foreach (var letter in letters.Take(counts[cabin] - placed))
                {
                    seats.Add(new AircraftSeat { RowNumber = row, Letter = letter.ToString(), CabinClass = cabin });
                    placed++;
                }
            }

            row += _layout.RowsBetweenCabins;
        }

        return seats;
    }

    private static string NormalizeRegistration(string registrationNumber) => registrationNumber.Trim().ToUpperInvariant();

    private static void Apply(AircraftRequestDto request, string registration, Aircraft aircraft)
    {
        aircraft.Name = request.Name.Trim();
        aircraft.Model = request.Model.Trim();
        aircraft.RegistrationNumber = registration;
        aircraft.Status = request.Status;
    }

    private async Task EnsureRegistrationIsFreeAsync(string registration, int? excludeId, CancellationToken cancellationToken)
    {
        if (await _aircraftRepository.RegistrationExistsAsync(registration, excludeId, cancellationToken))
        {
            throw new ConflictException($"An aircraft with registration {registration} already exists.");
        }
    }

    private async Task<Aircraft> GetAircraftAsync(int id, CancellationToken cancellationToken)
        => await _aircraftRepository.GetWithSeatsAsync(id, cancellationToken)
           ?? throw new NotFoundException("Aircraft not found.");
}
