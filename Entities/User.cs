namespace LoanAPI.Entities;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;

    // Add this property if it wasn't included in your snippet
    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Account? Account { get; set; }
    public ICollection<Beneficiary> Beneficiaries { get; set; } = new List<Beneficiary>();
}