using LoanAPI.Entities;

namespace LoanAPI.Services;

public interface ITransactionProcessingService
{
    /// <summary>
    /// Existing method — credits a wallet for an already-tracked transaction.
    /// Used by simulate-deposit, where the transaction row is created up front.
    /// </summary>
    Task<(bool Created, Transactions? Transaction)> ProcessCreditAsync(Guid userId, string reference, decimal amount, string channel);

    /// <summary>
    /// Creates a new transaction and credits the user's wallet, for webhooks
    /// where the transaction doesn't exist locally until the charge succeeds.
    /// Idempotent by reference — a duplicate webhook delivery for the same
    /// reference is a no-op on the second call.
    /// </summary>
    Task<(bool Created, Transactions? Transaction)> ProcessChargeSuccessAsync(Guid userId, string reference, decimal amount);
}