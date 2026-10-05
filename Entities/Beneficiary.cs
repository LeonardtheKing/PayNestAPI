namespace LoanAPI.Entities;

public class Beneficiary
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Foreign Key linking the beneficiary to a specific User
    public Guid SenderId { get; set; }
    public Guid UserId { get; set; }

    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;

    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    // Navigation property back to User
    public User User { get; set; } = null!;
}
