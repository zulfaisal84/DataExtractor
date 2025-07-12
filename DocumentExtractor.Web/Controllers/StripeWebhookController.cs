using DocumentExtractor.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace DocumentExtractor.Web.Controllers;

[ApiController]
[Route("api/webhooks/stripe")]
public class StripeWebhookController : ControllerBase
{
    private readonly ILogger<StripeWebhookController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _config;

    public StripeWebhookController(ILogger<StripeWebhookController> logger,
        UserManager<ApplicationUser> userManager,
        IConfiguration config)
    {
        _logger = logger;
        _userManager = userManager;
        _config = config;
    }

    [HttpPost]
    public async Task<IActionResult> Post()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var signatureHeader = Request.Headers["Stripe-Signature"];
        var webhookSecret = _config["Stripe:WebhookSecret"];

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "⚠️  Invalid Stripe webhook signature");
            return BadRequest();
        }

        _logger.LogInformation("📬 Stripe event received: {Type}", stripeEvent.Type);

        switch (stripeEvent.Type)
        {
            case "customer.subscription.created":
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                await HandleSubscriptionEventAsync(stripeEvent);
                break;
            case "invoice.payment_failed":
                await HandleInvoiceFailedAsync(stripeEvent);
                break;
            case "invoice.payment_succeeded":
                await HandleInvoiceSucceededAsync(stripeEvent);
                break;
            default:
                _logger.LogInformation("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                break;
        }

        return Ok();
    }

    private async Task HandleSubscriptionEventAsync(Event stripeEvent)
    {
        var subscription = stripeEvent.Data.Object as Subscription;
        if (subscription == null) return;

        var customerId = subscription.CustomerId;
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == customerId);
        if (user == null) return;

        user.StripeSubscriptionId = subscription.Id;
        user.SubscriptionStatus = subscription.Status switch
        {
            "active" => SubscriptionStatus.Active,
            "trialing" => SubscriptionStatus.Trial,
            "past_due" => SubscriptionStatus.PastDue,
            "canceled" => SubscriptionStatus.Canceled,
            "unpaid" => SubscriptionStatus.Suspended,
            _ => user.SubscriptionStatus
        };
        await _userManager.UpdateAsync(user);
    }

    private async Task HandleInvoiceFailedAsync(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;
        var customerId = invoice.CustomerId;
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == customerId);
        if (user == null) return;
        user.SubscriptionStatus = SubscriptionStatus.PastDue;
        await _userManager.UpdateAsync(user);
    }

    private async Task HandleInvoiceSucceededAsync(Event stripeEvent)
    {
        var invoice = stripeEvent.Data.Object as Invoice;
        if (invoice == null) return;
        var customerId = invoice.CustomerId;
        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == customerId);
        if (user == null) return;
        user.SubscriptionStatus = SubscriptionStatus.Active;
        await _userManager.UpdateAsync(user);
    }
}