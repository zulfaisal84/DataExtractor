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

    // Stripe identifiers to link billing information
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }

    // Refresh token storage for JWT renewal
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
}