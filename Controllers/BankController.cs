using LoanAPI.DTO;
using LoanAPI.Entities;
using LoanAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace LoanAPI.Controllers;

[ApiController]
[Route("api/bank")]
public class BankController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<BankController> _logger;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IPaystackVirtualAccountService _virtualAccountService;

    public BankController(
        AppDbContext db,
        ILogger<BankController> logger,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        IPaystackVirtualAccountService virtualAccountService)
    {
        _db = db;
        _logger = logger;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _virtualAccountService = virtualAccountService;
    }

    // 1. Transfer funds (Internal wallet-to-wallet OR External payout via Paystack)
    [HttpPost("transfer-to-beneficiary")]
    public async Task<IActionResult> TransferToBeneficiary([FromBody] BeneficiaryTransferRequestDto request)
    {
        if (request.Amount <= 0)
            return BadRequest(new { Message = "Amount must be greater than zero." });

        // 1. Sender's account
        var senderAccount = await _db.Accounts
            .FirstOrDefaultAsync(a => a.UserId == request.SenderUserId);

        if (senderAccount == null)
            return NotFound(new { Message = "Sender account not found." });

        // 2. Beneficiary's account, joined with its Bank
        var beneficiaryAccount = await _db.Accounts
            .Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.UserId == request.BeneficiaryId);

        if (beneficiaryAccount == null)
            return NotFound(new { Message = "Beneficiary account not found." });

        if (beneficiaryAccount.Id == senderAccount.Id)
            return BadRequest(new { Message = "You cannot transfer to your own account." });

        // 3. Balance check
        if (senderAccount.Balance < request.Amount)
            return BadRequest(new { Message = "Insufficient balance." });

        var reference = "TRF-" + Guid.NewGuid().ToString("N")[..10].ToUpper();
        using var dbTransaction = await _db.Database.BeginTransactionAsync();

        try
        {
            // 4. Reuse the beneficiary if this sender already saved this account
            var beneficiary = await _db.Beneficiaries.FirstOrDefaultAsync(b =>
                b.SenderId == request.SenderUserId &&
                b.AccountNumber == beneficiaryAccount.AccountNumber &&
                b.BankCode == beneficiaryAccount.Bank.Code);

            if (beneficiary == null)
            {
                beneficiary = new Beneficiary
                {
                    SenderId = request.SenderUserId,
                    UserId = beneficiaryAccount.UserId, 
                    AccountNumber = beneficiaryAccount.AccountNumber,
                    AccountName = beneficiaryAccount.AccountName,
                    BankName = beneficiaryAccount.Bank.Name,
                    BankCode = beneficiaryAccount.Bank.Code,
                    DateCreated = DateTime.UtcNow
                };

                _db.Beneficiaries.Add(beneficiary);
                _logger.LogInformation("Creating new beneficiary {AccountNumber} for user {SenderId}",
                    beneficiary.AccountNumber, request.SenderUserId);
            }

            var now = DateTime.UtcNow;

            // 5. Debit sender, credit receiver
            senderAccount.Balance -= request.Amount;
            senderAccount.UpdatedAt = now;

            beneficiaryAccount.Balance += request.Amount;
            beneficiaryAccount.UpdatedAt = now;

            // 6. Ledger entries
            _db.Transactions.Add(new Transactions
            {
                AccountId = senderAccount.Id,
                Reference = reference,
                Amount = request.Amount,
                Type = "Debit",
                Status = "Success",
                IsProcessed = true,
                Narration = request.Narration ?? $"Transfer to {beneficiary.AccountName} ({beneficiary.AccountNumber})",
                DateCreated = now,
                Channel = "bank_transfer"
            });

            _db.Transactions.Add(new Transactions
            {
                AccountId = beneficiaryAccount.Id,
                Reference = reference + "-CR",
                Amount = request.Amount,
                Type = "Credit",
                Status = "Success",
                IsProcessed = true,
                Narration = request.Narration ?? $"Transfer from {senderAccount.AccountName} ({senderAccount.AccountNumber})",
                DateCreated = now,
                Channel = "bank_transfer"
            });

            // Saves beneficiary (if new), both balances and both transactions together
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Processed transfer of {Amount} from {SenderId} to beneficiary {BeneficiaryId}",
                request.Amount, request.SenderUserId, beneficiary.Id);

            return Ok(new
            {
                Status = "Success",
                Message = "Transfer to beneficiary completed successfully.",
                Reference = reference,
                BeneficiaryId = beneficiary.Id
            });
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Error processing transfer to beneficiary for sender {SenderId}", request.SenderUserId);
            return StatusCode(500, new { Message = "An error occurred while processing the transfer." });
        }
    }
    // 2. Helper Method to Hit Paystack Transfer API
    private async Task<bool> InitiatePaystackTransferAsync(string recipientCode, decimal amountInNaira, string reference, string reason)
    {
        var secretKey = _config["Paystack:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
        {
            _logger.LogError("Paystack SecretKey is missing from configuration.");
            return false;
        }

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretKey);

        var transferPayload = new
        {
            source = "balance",
            amount = (int)(amountInNaira * 100), // Paystack expects amount in kobo
            recipient = recipientCode,
            reference = reference,
            reason = reason
        };

        var response = await client.PostAsJsonAsync("https://api.paystack.co/transfer", transferPayload);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Paystack transfer API failed: {Error}", errorContent);
            return false;
        }

        return true;
    }

    // 3. Get Account Balance, Bank Name, and Account Number by User ID
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetAccountDetails(Guid userId)
    {
        var account = await _db.Accounts
            .Include(a => a.Bank)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (account == null)
            return NotFound(new { Message = "Account not found for this user." });

        var response = new
        {
            AccountNumber = account.AccountNumber,
            AccountName = account.AccountName,
            AccountBalance = account.Balance,
            BankName = account.Bank?.Name ?? "Unknown Bank"
        };

        return Ok(response);
    }

    // 4. Get the list of all banks
    [HttpGet("banks")]
    public async Task<IActionResult> GetBanks()
    {
        try
        {
            var banks = await _db.Banks
                .Select(b => new
                {
                    Id = b.Id,
                    Name = b.Name,
                    Code = b.Code
                })
                .OrderBy(b => b.Name)
                .ToListAsync();

            return Ok(banks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving the list of banks.");
            return StatusCode(500, new { Message = "An error occurred while fetching banks." });
        }
    }


    [HttpPost("transfer-recipient")]
    public async Task<IActionResult> CreateTransferRecipient([FromBody] CreateTransferRecipientDto request)
    {
        if (string.IsNullOrWhiteSpace(request.AccountNumber) ||
            string.IsNullOrWhiteSpace(request.BankCode) ||
            string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { Message = "Name, AccountNumber, and BankCode are required." });
        }

        try
        {
            var recipientCode = await CreateTransferRecipientAsync(request.Name, request.AccountNumber, request.BankCode);

            if (string.IsNullOrEmpty(recipientCode))
            {
                return StatusCode(502, new { Message = "Failed to create transfer recipient with payment gateway." });
            }

            _logger.LogInformation("Successfully created Paystack transfer recipient code {RecipientCode} for account {AccountNumber}",
                recipientCode, request.AccountNumber);

            return Ok(new
            {
                Status = "Success",
                Message = "Transfer recipient created successfully.",
                RecipientCode = recipientCode
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while creating transfer recipient.");
            return StatusCode(500, new { Message = "An internal error occurred." });
        }
    }


    [HttpGet("paystack-banks")]
    public async Task<IActionResult> GetPaystackBanks()
    {
        try
        {
            var banks = await GetPaystackBanksAsync();

            if (banks == null || !banks.Any())
            {
                return StatusCode(502, new { Message = "Failed to retrieve banks from payment gateway. Please try again later." });
            }

            _logger.LogInformation("Successfully retrieved {Count} banks from Paystack.", banks.Count);

            return Ok(new
            {
                Status = "Success",
                Message = "Banks fetched successfully from Paystack.",
                Count = banks.Count,
                Data = banks
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while fetching banks from Paystack.");
            return StatusCode(500, new { Message = "An internal error occurred." });
        }
    }

    // 5. Create a Dedicated Virtual Account in Paystack Test Mode
    [HttpPost("create-test-virtual-account")]
    public async Task<IActionResult> CreateTestVirtualAccount([FromBody] CreateVirtualAccountRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FirstName) ||
            string.IsNullOrWhiteSpace(request.LastName) ||
            string.IsNullOrWhiteSpace(request.Phone))
        {
            return BadRequest(new { Message = "Email, FirstName, LastName, and Phone are required." });
        }

        try
        {
            var virtualAccount = await _virtualAccountService.CreateTestVirtualAccountAsync(
                request.Email,
                request.FirstName,
                request.LastName,
                request.Phone
            );

            if (virtualAccount == null)
            {
                return StatusCode(502, new { Message = "Failed to create virtual account with payment gateway." });
            }

            _logger.LogInformation("Successfully generated test virtual account {AccountNumber} for customer {Email}",
                virtualAccount.AccountNumber, request.Email);

            return Ok(new
            {
                Status = "Success",
                Message = "Test virtual account created successfully.",
                Data = virtualAccount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while creating test virtual account for {Email}", request.Email);
            return StatusCode(500, new { Message = "An internal error occurred." });
        }
    }


    public async Task<string?> CreateTransferRecipientAsync(string name, string accountNumber, string bankCode)
    {
        var secretKey = _config["Paystack:SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
        {
            _logger.LogError("Paystack SecretKey is missing from configuration.");
            return null;
        }

        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretKey);

        var payload = new
        {
            type = "nuban", // Set to nuban for Nigerian bank accounts
            name = name,
            account_number = accountNumber,
            bank_code = bankCode,
            currency = "NGN"
        };

        var response = await client.PostAsJsonAsync("https://api.paystack.co/transferrecipient", payload);
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("Failed to create Paystack transfer recipient: {Error}", errorContent);
            return null;
        }

        // Parse the JSON response to extract the recipient_code
        var responseObj = await response.Content.ReadFromJsonAsync<JsonElement>();
        if (responseObj.TryGetProperty("data", out var dataProp) &&
            dataProp.TryGetProperty("recipient_code", out var codeProp))
        {
            return codeProp.GetString();
        }

        return null;
    }


    public async Task<List<BankDto>> GetPaystackBanksAsync()
    {
        var secretKey = _config["Paystack:SecretKey"];
        using var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", secretKey);

        var response = await client.GetAsync("https://api.paystack.co/bank?country=nigeria");
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch banks from Paystack.");
            return new List<BankDto>();
        }

        var result = await response.Content.ReadFromJsonAsync<PaystackBankResponse>();
        return result?.Data ?? new List<BankDto>();
    }

    // Supporting helper classes for mapping
    public class PaystackBankResponse
    {
        public bool Status { get; set; }
        public List<BankDto> Data { get; set; } = new();
    }

    public class BankDto
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }
}