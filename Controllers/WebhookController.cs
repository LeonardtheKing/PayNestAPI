using LoanAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Sentry;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LoanAPI.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhookController> _logger;
    private readonly IHub _sentryHub;
    private readonly ITransactionProcessingService _transactionProcessor;

    public WebhookController(
        AppDbContext db,
        IConfiguration config,
        ILogger<WebhookController> logger,
        IHub sentryHub,
        ITransactionProcessingService transactionProcessor)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _sentryHub = sentryHub;
        _transactionProcessor = transactionProcessor;
    }

    [HttpPost("paystack")]
    public async Task<IActionResult> HandlePaystackWebhook()
    {
        // Read raw bytes — signature verification must operate on the exact
        // bytes Paystack sent, not a decode/re-encode round trip.
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms);
        var rawBytes = ms.ToArray();
        var jsonBody = Encoding.UTF8.GetString(rawBytes);

        if (!Request.Headers.TryGetValue("x-paystack-signature", out var signatureHeader) || string.IsNullOrEmpty(signatureHeader))
        {
            _logger.LogWarning("Webhook received without x-paystack-signature header.");
            _sentryHub.CaptureMessage("Paystack webhook missing signature header", SentryLevel.Warning);
            return BadRequest(new { Message = "Missing signature header." });
        }

        var secretKey = _config["Paystack:SecretKey"] ?? string.Empty;
        if (string.IsNullOrEmpty(secretKey))
        {
            _logger.LogError("Paystack:SecretKey is not configured.");
            _sentryHub.CaptureMessage("Paystack webhook secret key missing from configuration", SentryLevel.Error);
            return StatusCode(500, new { Message = "Webhook not configured correctly." });
        }

        if (!VerifyPaystackSignature(rawBytes, signatureHeader!, secretKey))
        {
            _logger.LogWarning("Invalid Paystack webhook signature detected.");
            _sentryHub.CaptureMessage("Invalid Paystack webhook signature", SentryLevel.Warning);
            return Unauthorized(new { Message = "Invalid signature." });
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(jsonBody);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse webhook JSON payload.");
            _sentryHub.CaptureException(ex, scope => scope.SetTag("webhook.stage", "parse"));
            return BadRequest(new { Message = "Invalid JSON format." });
        }

        using (doc)
        {
            var root = doc.RootElement;

            if (!root.TryGetProperty("event", out var eventProp))
            {
                _logger.LogWarning("Webhook payload missing 'event' property.");
                _sentryHub.CaptureMessage("Paystack webhook missing 'event' property", SentryLevel.Warning);
                return BadRequest(new { Message = "Missing 'event' field." });
            }

            string eventType = eventProp.GetString() ?? string.Empty;
            _sentryHub.ConfigureScope(scope => scope.SetTag("paystack.event", eventType));
            _logger.LogInformation("Received Paystack webhook event: {EventType}", eventType);

            // Always acknowledge unhandled event types with 200 — Paystack will
            // keep retrying a non-2xx response, and we don't want retries for
            // events we intentionally don't act on (e.g. subscription.create).
            if (eventType != "charge.success")
            {
                return Ok(new { Status = "Ignored", Event = eventType });
            }

            return await HandleChargeSuccess(root, eventType);
        }
    }

    private async Task<IActionResult> HandleChargeSuccess(JsonElement root, string eventType)
    {
        if (!root.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("reference", out var referenceProp) ||
            !data.TryGetProperty("amount", out var amountProp))
        {
            return BadRequest(new { Message = "Malformed charge.success payload." });
        }

        // Extract the payment channel (defaults to "unknown" if missing)
        string channel = data.TryGetProperty("channel", out var channelProp)
            ? channelProp.GetString() ?? "unknown"
            : "unknown";

        if (!data.TryGetProperty("metadata", out var metadata) ||
            !metadata.TryGetProperty("user_id", out var userIdProp) ||
            !Guid.TryParse(userIdProp.GetString(), out var userId))
        {
            return BadRequest(new { Message = "Missing or invalid user reference in payload." });
        }

        string reference = referenceProp.GetString() ?? string.Empty;
        decimal amountInKobo = amountProp.GetDecimal();
        decimal amountInNaira = amountInKobo / 100m;

        try
        {
            // Pass the channel into your processing service method
            var (created, transaction) = await _transactionProcessor.ProcessCreditAsync(userId, reference, amountInNaira, channel);

            if (!created)
                return Ok(new { Message = "Event already processed." });

            return Ok(new { Status = "Success" });
        }
        catch (Exception ex)
        {
            // error handling...
            return StatusCode(500, new { Message = "Database error processing webhook." });
        }
    }

    /// <summary>
    /// Handles metadata whether Paystack sends it as a JSON object or a JSON-encoded string.
    /// </summary>
    private Guid? ExtractUserId(JsonElement metadata)
    {
        string? userIdString = null;

        if (metadata.ValueKind == JsonValueKind.Object)
        {
            if (metadata.TryGetProperty("user_id", out var userIdProp))
                userIdString = userIdProp.GetString();
        }
        else if (metadata.ValueKind == JsonValueKind.String)
        {
            var metadataStr = metadata.GetString();
            if (!string.IsNullOrEmpty(metadataStr))
            {
                try
                {
                    using var metaDoc = JsonDocument.Parse(metadataStr);
                    if (metaDoc.RootElement.TryGetProperty("user_id", out var metaUserIdProp))
                        userIdString = metaUserIdProp.GetString();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse metadata string as JSON.");
                }
            }
        }

        return Guid.TryParse(userIdString, out var userId) ? userId : null;
    }

    /// <summary>
    /// Verifies the HMAC-SHA512 signature using constant-time comparison to
    /// prevent timing attacks. Operates on raw request bytes, not a
    /// decoded/re-encoded string, to avoid encoding mismatches.
    /// </summary>
    private bool VerifyPaystackSignature(byte[] rawBody, string signatureHeader, string secretKey)
    {
        try
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);

            using var hmac = new HMACSHA512(keyBytes);
            var computedHash = hmac.ComputeHash(rawBody);

            byte[] providedHash;
            try
            {
                providedHash = Convert.FromHexString(signatureHeader);
            }
            catch (FormatException)
            {
                // signatureHeader isn't valid hex — definitely not a match
                return false;
            }

            if (providedHash.Length != computedHash.Length)
                return false;

            return CryptographicOperations.FixedTimeEquals(computedHash, providedHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error computing Paystack signature.");
            _sentryHub.CaptureException(ex, scope => scope.SetTag("webhook.stage", "signature_verify"));
            return false;
        }
    }
}