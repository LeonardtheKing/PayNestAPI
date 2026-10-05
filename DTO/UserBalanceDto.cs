namespace BankAPI.DTO;

public class UserBalanceDto
{
    public Guid UserId { get; set; }
    public decimal Balance { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
}
