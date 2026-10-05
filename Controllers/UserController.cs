using BankAPI.DTO;
using LoanAPI.DTO;
using LoanAPI.Entities;
using LoanAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace LoanAPI.Controllers;


[ApiController]
[Route("api/users")]
public class UserController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILogger<UserController> _logger;

    public UserController(AppDbContext db, ILogger<UserController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterUserDto dto)
    {
        // 1. Check if user already exists
        var existingUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == dto.Email.ToLower());
        if (existingUser != null)
        {
            return BadRequest(new { Message = "A user with this email already exists." });
        }

        // 2. Hash the password securely using BCrypt
        string passwordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password);

        // Use a database transaction to ensure the user and their initial wallet are created together (All or Nothing)
        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // 3. Create the User entity
            var user = new User
            {
                FullName = dto.FullName,
                Email = dto.Email.ToLower(),
                PasswordHash = passwordHash,
                Phone = dto.Phone,
                BankName = dto.BankName,
                AccountNumber = await AccountNumberGenerator.GenerateUniqueAccountNumberAsync(_db),
                DateCreated = DateTime.UtcNow // <--- Ensure UTC is used here too if explicitly set
            };

            _db.Users.Add(user);

            var account = new Account
            {
                UserId = user.Id,
                Balance = 0m, // Initial balance
                AccountName = user.FullName,
                AccountNumber = user.AccountNumber,
                CreatedAt = DateTime.UtcNow, // <--- Changed from DateTime.Now to DateTime.UtcNow
                UpdatedAt = DateTime.UtcNow, // <--- Best practice to use UtcNow here as well
                BankId = dto.BankId
            };

            _db.Accounts.Add(account);

            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("New user registered successfully with ID {UserId} and email {Email}.", user.Id, user.Email);

            return StatusCode(201, new
            {
                Message = "User registered successfully and wallet provisioned.",
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                BankName = dto.BankName,
                AccountNumber = user.AccountNumber
            });
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex, "Failed to register user with email {Email}", dto.Email);
            return StatusCode(500, new { Message = "An error occurred while processing your registration." });
        }
    }

    [HttpGet]
    public async Task<ActionResult<PagedResultDto<UserResponseDto>>> GetUsers(
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 10,
    [FromQuery] string? searchKeyword = null)
    {
        // 1. Guard clauses against invalid pagination parameters
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize < 1) pageSize = 10;
        if (pageSize > 50) pageSize = 50; // Performance cap

        // 2. Base query
        var query = _db.Users.AsQueryable();

        // Optional: Search filter by name or email if provided
        if (!string.IsNullOrWhiteSpace(searchKeyword))
        {
            var lowerKeyword = searchKeyword.ToLower();
            query = query.Where(u => u.FullName.ToLower().Contains(lowerKeyword) ||
                                     u.Email.ToLower().Contains(lowerKeyword));
        }

        // 3. Get total count for pagination metadata calculation
        var totalCount = await query.CountAsync();

        // 4. Apply pagination (Skip & Take) and order by newest first
        var users = await query
            .OrderByDescending(u => u.DateCreated)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new UserResponseDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email,
                Phone = u.Phone,
                IsActive = u.IsActive,
                DateCreated = u.DateCreated
            })
            .ToListAsync();

        // 5. Construct paged result response
        var pagedResult = new PagedResultDto<UserResponseDto>
        {
            Items = users,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        _logger.LogInformation("Retrieved page {PageNumber} of users containing {Count} records (Total: {TotalCount}).",
            pageNumber, users.Count, totalCount);

        return Ok(pagedResult);
    }

    [HttpGet("{userId:guid}/balance")]
    public async Task<ActionResult<UserBalanceDto>> GetUserBalance(Guid userId)
    {
        // 1. Fetch the user and include their related Account (and its Bank if needed)
        var user = await _db.Users
            .Include(u => u.Account)
                .ThenInclude(a => a!.Bank)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound(new { Message = "User not found." });
        }

        // 2. Map data to the UserBalanceDto
        var responseDto = new UserBalanceDto
        {
            UserId = user.Id,
            Balance = user.Account?.Balance ?? 0m,
            AccountNumber = user.AccountNumber,
            // Fallback to Account's Bank name if available, otherwise use user's stored BankName field
            //BankName = user.Account?.Bank?.Name ?? user.BankName
            BankName =  user.BankName
        };

        _logger.LogInformation("Retrieved balance details for user ID {UserId}.", userId);

        return Ok(responseDto);
    }
}
