using LoanAPI.DTO;
using LoanAPI.Entities; // Make sure this includes your enum namespace if defined there
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TransactionController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<TransactionController> _logger;

    public TransactionController(AppDbContext db, ILogger<TransactionController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Gets a paginated list of transactions with an optional filter for TransactionTypeEnum.
    /// </summary>
    /// <param name="pageNumber">Page number (default is 1)</param>
    /// <param name="pageSize">Number of items per page (default is 10, max 100)</param>
    /// <param name="type">Optional filter: MoneyIn (0) or MoneyOut (1)</param>
    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TransactionTypeEnum? type = null)
    {
        // Basic validation and bounds checking
        if (pageNumber <= 0) pageNumber = 1;
        if (pageSize <= 0) pageSize = 10;
        if (pageSize > 100) pageSize = 100; // Prevent over-fetching

        try
        {
            var query = _db.Transactions.AsQueryable();

            // Apply filter if 'type' query parameter is provided
            if (type.HasValue)
            {
                // Converts the enum value to its string equivalent (e.g., "MoneyIn" or "MoneyOut") 
                // Alternatively, map it to "Credit"/"Debit" if your database uses those terms.
                string typeFilter = type.Value.ToString();
                query = query.Where(t => t.Type == typeFilter);
            }

            // Get total count based on the filtered query
            var totalCount = await query.CountAsync();

            // Fetch the paginated subset ordered by latest first
            var transactions = await query
                .Include(t => t.Account) // Include account to fetch BankId
                .OrderByDescending(t => t.DateCreated)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new TransactionDto
                {
                    Id = t.Id,
                    BankId = t.Account != null ? t.Account.BankId : Guid.Empty,
                    Reference = t.Reference,
                    Amount = t.Amount,
                    Channel = t.Channel,
                    Type = t.Type,
                    Status = t.Status,
                    IsProcessed = t.IsProcessed,
                    DateCreated = t.DateCreated
                })
                .ToListAsync();

            var pagedResponse = new
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalCount > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0,
                Data = transactions
            };

            return Ok(pagedResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated transactions.");
            return StatusCode(500, new { Message = "An error occurred while fetching transactions." });
        }
    }
}