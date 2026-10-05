namespace LoanAPI.DTO;

public class TransactionDto
{
    public Guid Id { get; set; }
    public Guid BankId { get; set; }
    public string Reference { get; set; } = "";
    public decimal Amount { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Type { get; set; } = "";
    public string Status { get; set; } = "";
    public bool IsProcessed { get; set; }
    public DateTime DateCreated { get; set; }
    // no back-reference to Wallet
}
