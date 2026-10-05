namespace LoanAPI.Entities;

public class Bank
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public ICollection<Account> Accounts { get; set; } = new List<Account>();
}
