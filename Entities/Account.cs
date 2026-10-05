namespace LoanAPI.Entities;

public class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Balance { get; set; } = 0m;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Guid UserId { get; set; }

    // Foreign Key linking to Bank
    public Guid BankId { get; set; }

    // Navigation property: Many accounts belong to one bank
    public Bank Bank { get; set; } = null!;
    public ICollection<Transactions> Transactions { get; set; } = [];
}
