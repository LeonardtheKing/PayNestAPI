namespace LoanAPI.Services;

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

public interface IPaystackVirtualAccountService
{
    Task<VirtualAccountData?> CreateTestVirtualAccountAsync(
        string email, string firstName, string lastName, string phone,
        CancellationToken ct = default);
}

public class PaystackVirtualAccountService : IPaystackVirtualAccountService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<PaystackVirtualAccountService> _logger;

    public PaystackVirtualAccountService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<PaystackVirtualAccountService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        var secretKey = configuration["Paystack:SecretKey"]
            ?? throw new InvalidOperationException("Paystack:SecretKey is not configured.");

        _httpClient.BaseAddress ??= new Uri("https://api.paystack.co/");

        if (_httpClient.DefaultRequestHeaders.Authorization == null)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", secretKey);
        }

        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("LoanApi/1.0 (+dotnet)");
        }

        if (_httpClient.DefaultRequestHeaders.Accept.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }

    public async Task<VirtualAccountData?> CreateTestVirtualAccountAsync(
        string email, string firstName, string lastName, string phone,
        CancellationToken ct = default)
    {
        var customerCode = await CreateCustomerAsync(email, firstName, lastName, phone, ct);
        if (string.IsNullOrEmpty(customerCode))
        {
            _logger.LogWarning("Failed to create or retrieve Paystack customer for {Email}", email);
            return null;
        }

        var payload = new
        {
            customer = customerCode,
            preferred_bank = "test-bank" // Required for Test Mode DVA generation
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("dedicated_account", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("dedicated_account", response, body);
            return null;
        }

        var result = JsonSerializer.Deserialize<PaystackApiResponse<VirtualAccountData>>(body, JsonOptions);
        if (result?.Status != true)
        {
            _logger.LogWarning("Paystack dedicated_account returned status=false: {Message}", result?.Message);
            return null;
        }

        return result.Data;
    }

    private async Task<string?> CreateCustomerAsync(
        string email, string firstName, string lastName, string phone, CancellationToken ct)
    {
        var payload = new
        {
            email,
            first_name = firstName,
            last_name = lastName,
            phone
        };

        using var content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync("customer", content, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            LogFailure("customer", response, body);
            return null;
        }

        var result = JsonSerializer.Deserialize<PaystackApiResponse<CustomerData>>(body, JsonOptions);
        return result?.Status == true ? result.Data?.CustomerCode : null;
    }

    private void LogFailure(string endpoint, HttpResponseMessage response, string body)
    {
        var isCloudflareBlock =
            response.Headers.Contains("CF-Ray") &&
            body.Contains("<html", StringComparison.OrdinalIgnoreCase);

        _logger.LogError(
            "Paystack {Endpoint} failed. Status={Status} CloudflareBlock={Blocked} Body={Body}",
            endpoint, (int)response.StatusCode, isCloudflareBlock,
            body.Length > 500 ? body[..500] : body);
    }
}

// DTOs for deserialization
public class PaystackApiResponse<T>
{
    public bool Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
}

public class CustomerData
{
    [JsonPropertyName("customer_code")]
    public string CustomerCode { get; set; } = string.Empty;
}

public class VirtualAccountData
{
    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = string.Empty;

    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("bank")]
    public BankInfo Bank { get; set; } = new();
}

public class BankInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;
}