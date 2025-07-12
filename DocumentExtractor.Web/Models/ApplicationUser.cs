using Microsoft.AspNetCore.Identity;

namespace DocumentExtractor.Web.Models;

/// <summary>
/// Application user entity for ASP.NET Core Identity.
/// Extend this class with subscription / profile fields later.
/// </summary>
public class ApplicationUser : IdentityUser
{
    // Additional custom properties can be added here (e.g., SubscriptionStatus)
    public SubscriptionStatus SubscriptionStatus { get; set; } = SubscriptionStatus.Trial;
}