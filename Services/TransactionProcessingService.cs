using LoanAPI.Entities;
using Microsoft.EntityFrameworkCore;

namespace LoanAPI.Services;

public class TransactionProcessingService : ITransactionProcessingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<TransactionProcessingService> _logger;

    public TransactionProcessingService(AppDbContext db, ILogger<TransactionProcessingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<(bool Created, Transactions? Transaction)> ProcessCreditAsync(Guid userId, string reference, decimal amount,
        string channel)
    {
        var existing = await _db.Transactions.FirstOrDefaultAsync(t => t.Reference == reference);
        if (existing != null)
        {
            _logger.LogInformation("Reference {Reference} already recorded, skipping duplicate webhook.", reference);
            return (false, existing);
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(w => w.UserId == userId);
        if (account == null)
            throw new InvalidOperationException($"No account found for user {userId}.");

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var transaction = new Transactions
            {
                AccountId = account.Id,
                Reference = reference,
                Amount = amount,
                Type = "Credit",
                Status = "Success",
                Channel = channel, // <--- Save channel here (e.g., "card", "bank_transfer", "ussd")
                IsProcessed = true
            };

            account.Balance += amount;
            account.UpdatedAt = DateTime.UtcNow;

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Created transaction {Reference} via {Channel} and credited account {AccountId} with {Amount}.",
                reference, channel, account.Id, amount);

            return (true, transaction);
        }
        catch(Exception ex)
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }

    public async Task<(bool Created, Transactions? Transaction)> ProcessChargeSuccessAsync(Guid userId, string reference, decimal amount)
    {
        // Idempotency guard: Paystack retries webhook delivery, so a reference
        // we've already recorded must be a no-op, not a double credit.
        var existing = await _db.Transactions.FirstOrDefaultAsync(t => t.Reference == reference);
        if (existing != null)
        {
            _logger.LogInformation("Reference {Reference} already recorded, skipping duplicate webhook.", reference);
            return (false, existing);
        }

        var account = await _db.Accounts.FirstOrDefaultAsync(w => w.UserId == userId);
        if (account == null)
            throw new InvalidOperationException($"No account found for user {userId}.");

        using var dbTransaction = await _db.Database.BeginTransactionAsync();
        try
        {
            var transaction = new Transactions
            {
                AccountId = account.Id ,
                Reference = reference,
                Amount = amount,
                Type = "Credit",
                Status = "Success",
                IsProcessed = true,
                Account = account
            };

            account.Balance += amount;
            account.UpdatedAt = DateTime.UtcNow;

            _db.Transactions.Add(transaction);
            await _db.SaveChangesAsync();
            await dbTransaction.CommitAsync();

            _logger.LogInformation("Created transaction {Reference} and credited account {AccountId} with {Amount}. New balance: {Balance}",
                reference, account.Id, amount, account.Balance);

            return (true, transaction);
        }
        catch
        {
            await dbTransaction.RollbackAsync();
            throw;
        }
    }
}