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

    /// <summary>The father's name (ata adı); empty when the owner did not give it.</summary>
    public string? FatherName { get; set; }

    /// <summary>The passport number as the owner keeps it on the profile (upper case, no spaces or dashes); empty when not given.</summary>
    public string? PassportNumber { get; set; }

    /// <summary>The identity card number (şəxsiyyət vəsiqəsi) in the same form; empty when not given.</summary>
    public string? NationalIdNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;

    /// <summary>When the owner opened the link sent to this address; null until then. Never set without that link (or the trusted admin seeder).</summary>
    public DateTime? EmailVerifiedAt { get; set; }

    /// <summary>Tokens issued before this moment no longer open the account ("sign out everywhere", a changed password). Null: no cut-off.</summary>
    public DateTime? SessionsValidFrom { get; set; }

    /// <summary>When the password was last changed by its owner (settings or reset); null when it never was.</summary>
    public DateTime? PasswordChangedAt { get; set; }

    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public ICollection<SavedPassenger> SavedPassengers { get; set; } = new List<SavedPassenger>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
