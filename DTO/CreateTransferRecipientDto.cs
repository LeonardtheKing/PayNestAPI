namespace LoanAPI.DTO;

public class CreateTransferRecipientDto
{
    public string Name { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty; // e.g., "058" for GTB
}
