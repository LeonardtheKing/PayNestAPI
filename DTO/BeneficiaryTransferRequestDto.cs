namespace LoanAPI.DTO;

public class BeneficiaryTransferRequestDto
{
    public Guid SenderUserId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public Guid BeneficiaryId { get; set; } 
    public decimal Amount { get; set; }
    public string Narration { get; set; }
    public string BankCode { get; set; }
}
