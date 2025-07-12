namespace DocumentExtractor.Web.Models;

/// <summary>
/// Tracks a customer's subscription state.
/// In production this will be kept in sync with your billing provider via web-hooks.
/// </summary>
public enum SubscriptionStatus
{
    Trial = 0,
    Active = 1,
    PastDue = 2,
    Canceled = 3,
    Suspended = 4
}