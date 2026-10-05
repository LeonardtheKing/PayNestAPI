namespace LoanAPI.DTO;

using System.ComponentModel.DataAnnotations;

public class LoanApplicationDto
{
    [Required(ErrorMessage = "UserId is required.")]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(1, 50000, ErrorMessage = "Loan amount must be between 1 and 50,000.")]
    public decimal Amount { get; set; }
}
