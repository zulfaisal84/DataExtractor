using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using DocumentExtractor.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace DocumentExtractor.Web.Middleware;

public class SubscriptionEnforcementMiddleware
{
    private readonly RequestDelegate _next;

    public SubscriptionEnforcementMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                         context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            if (!string.IsNullOrEmpty(userId))
            {
                var user = await userManager.FindByIdAsync(userId);
                if (user != null && user.SubscriptionStatus != SubscriptionStatus.Active && user.SubscriptionStatus != SubscriptionStatus.Trial)
                {
                    context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                    await context.Response.WriteAsync("Subscription inactive.");
                    return;
                }
            }
        }

        await _next(context);
    }
}