using LoanAPI.DTO;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LoanAPI.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentController> _logger;
    private readonly AppDbContext _db;

    public PaymentController(
        IHttpClientFactory httpClientFactory,
        IConfiguration config,
        ILogger<PaymentController> logger,
        AppDbContext db)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _logger = logger;
        _db = db;
    }

    [HttpPost("initialize")]
    public async Task<IActionResult> InitializePayment([FromBody] InitializePaymentDto dto)
    {
        // 1. Verify user exists
        var userExists = _db.Users.Any(u => u.Id == dto.UserId);
        if (!userExists)
            return NotFound("User not found.");

        // 2. Paystack expects amounts in the lowest denomination (kobo for NGN -> multiply by 100)
        long amountInKobo = (long)(dto.Amount * 100);
        string reference = $"TXN_{Guid.NewGuid().ToString().Substring(0, 10).ToUpper()}";

        var paystackPayload = new
        {
            email = dto.Email,
            amount = amountInKobo,
            reference = reference,
            callback_url = _config["Paystack:CallbackUrl"], // Optional: Where user redirects after payment
            metadata = new
            {
                user_id = dto.UserId.ToString()
            }
        };

        // 3. Setup HttpClient configured with Paystack Secret Key
        var client = _httpClientFactory.CreateClient();
        var secretKey = _config["Paystack:SecretKey"]; // e.g., sk_test_...

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secretKey);

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(paystackPayload),
            Encoding.UTF8,
            "application/json"
        );

        try
        {
            // 4. Call Paystack Initialize endpoint
            var response = await client.PostAsync("https://api.paystack.co/transaction/initialize", jsonContent);
            var responseString = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Paystack initialization failed: {Response}", responseString);
                return BadRequest(new { Message = "Failed to initialize payment gateway.", Details = responseString });
            }

            var paystackResponse = JsonSerializer.Deserialize<PaystackInitResponse>(responseString);

            if (paystackResponse == null || !paystackResponse.Status)
            {
                return BadRequest(new { Message = "Could not parse Paystack response." });
            }

            // 5. (Optional) Log a pending transaction in your database table for tracking
            // This links the reference back to the user before they even finish paying.

            return Ok(new
            {
                Message = "Payment initialized successfully.",
                AuthorizationUrl = paystackResponse.Data.AuthorizationUrl,
                Reference = reference
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An exception occurred while connecting to Paystack.");
            return StatusCode(500, "Internal server error communicating with payment gateway.");
        }
    }
}

public class PaystackInitData
{
    [JsonPropertyName("authorization_url")]
    public string AuthorizationUrl { get; set; } = string.Empty;

    [JsonPropertyName("access_code")]
    public string AccessCode { get; set; } = string.Empty;

    [JsonPropertyName("reference")]
    public string Reference { get; set; } = string.Empty;
}