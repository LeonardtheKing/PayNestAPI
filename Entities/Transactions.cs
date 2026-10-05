namespace LoanAPI.Entities;

public class Transactions
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string Reference { get; set; } = string.Empty; // Paystack transaction reference
    public string Narration { get; set; } = string.Empty; // Paystack transaction reference
    public decimal Amount { get; set; }
    public string Type { get; set; } = "Credit"; // Credit or Debit
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed
    public string Channel { get; set; } = string.Empty;
    public bool IsProcessed { get; set; } = false; // For webhook idempotency
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public Account Account { get; set; } = null!;
}
