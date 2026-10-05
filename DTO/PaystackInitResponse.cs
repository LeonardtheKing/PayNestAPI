using LoanAPI.Controllers;
using System.Text.Json.Serialization;

namespace LoanAPI.DTO;

// Supporting Response DTOs
public class PaystackInitResponse
{
    [JsonPropertyName("status")]
    public bool Status { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public PaystackInitData Data { get; set; } = new();
}
