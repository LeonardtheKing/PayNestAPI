using System.ComponentModel.DataAnnotations;

namespace LoanAPI.DTO;

public class InitializePaymentDto
{
    [Required(ErrorMessage = "UserId is required.")]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Amount is required.")]
    [Range(100, 1000000, ErrorMessage = "Amount must be at least 100.")]
    public decimal Amount { get; set; } // Value in standard currency (e.g., Naira)
}
