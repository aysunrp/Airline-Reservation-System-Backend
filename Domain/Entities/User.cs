using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? Title { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? NationalityCode { get; set; }
    public string? Phone { get; set; }
    public UserRole Role { get; set; } = UserRole.Client;
    public bool IsActive { get; set; } = true;

    /// <summary>When the owner opened the link sent to this address; null until then. Never set without that link (or the trusted admin seeder).</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<SavedPassenger> SavedPassengers { get; set; } = new List<SavedPassenger>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
