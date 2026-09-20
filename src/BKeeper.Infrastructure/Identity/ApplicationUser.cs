using BKeeper.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace BKeeper.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public Guid BoxId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}
