using LoanAPI.Services;
using System.Text.Json.Serialization;

namespace LoanAPI.DTO;

// Add this definition to your DTO file or namespace
public class VirtualAccountResponse
{
    [JsonPropertyName("bank")]
    public BankInfo Bank { get; set; } = new();

    [JsonPropertyName("account_name")]
    public string AccountName { get; set; } = string.Empty;

    [JsonPropertyName("account_number")]
    public string AccountNumber { get; set; } = string.Empty;

    [JsonPropertyName("assigned")]
    public bool Assigned { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "NGN";

    [JsonPropertyName("metadata")]
    public object? Metadata { get; set; }

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [JsonPropertyName("assignment")]
    public AccountAssignment? Assignment { get; set; }
}

public class AccountAssignment
{
    [JsonPropertyName("integration")]
    public long Integration { get; set; }

    [JsonPropertyName("assignee_id")]
    public long AssigneeId { get; set; }

    [JsonPropertyName("assignee_type")]
    public string AssigneeType { get; set; } = string.Empty;

    [JsonPropertyName("expired")]
    public bool Expired { get; set; }

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = string.Empty;

    [JsonPropertyName("assigned_at")]
    public DateTime AssignedAt { get; set; }
}