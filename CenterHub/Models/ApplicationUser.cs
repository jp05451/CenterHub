using Microsoft.AspNetCore.Identity;

namespace CenterHub.Models;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
