using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;

namespace LoanAPI.Controllers;

/// <summary>
/// Paystack sends the user's browser here after checkout:
///   GET /api/payments/callback?trxref=TXN_...&amp;reference=TXN_...
/// This endpoint only forwards the browser to the front-end result screen. It does not credit anything:
/// the wallet is credited by your webhook, and the front-end reads the real status from the transaction record.
///
/// Requires in appsettings.json (use your deployed front-end URL in production):
///   "Frontend": { "BaseUrl": "http://localhost:5173" }
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentCallbackController : ControllerBase
{
    // Paystack references are short and URL-safe; reject anything else before using it in a redirect
    private static readonly Regex ReferencePattern = new("^[A-Za-z0-9._=-]{1,100}$", RegexOptions.Compiled);

    private readonly IConfiguration _config;
    private readonly ILogger<PaymentCallbackController> _logger;

    public PaymentCallbackController(IConfiguration config, ILogger<PaymentCallbackController> logger)
    {
        _config = config;
        _logger = logger;
    }

    [HttpGet("callback")]
    public IActionResult Callback([FromQuery] string? reference, [FromQuery] string? trxref)
    {
        var value = string.IsNullOrWhiteSpace(reference) ? trxref : reference;
        if (string.IsNullOrWhiteSpace(value) || !ReferencePattern.IsMatch(value))
            return BadRequest("Invalid payment reference.");

        // The redirect target comes from configuration only (never from the request),
        // so this endpoint can't be used as an open redirect.
        var frontendBase = _config["Frontend:BaseUrl"]?.TrimEnd('/');
        if (!Uri.TryCreate(frontendBase, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogError("Frontend:BaseUrl is missing or invalid; cannot redirect payment callback for {Reference}", value);
            return Problem("Frontend:BaseUrl is not configured.");
        }

        return Redirect($"{frontendBase}/?reference={Uri.EscapeDataString(value)}");
    }
}