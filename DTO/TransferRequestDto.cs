namespace LoanAPI.DTO;

public class TransferRequestDto
{
    public Guid SenderUserId { get; set; }
    public string RecipientAccountNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Narration{ get; set; } = string.Empty;
    public string BankName{ get; set; } = string.Empty;
    public string RecipientCode { get; set; } = string.Empty;
}