using Microsoft.EntityFrameworkCore;

namespace LoanAPI.Services;

public static class AccountNumberGenerator
{
    /// <summary>
    /// Generates a unique 10-digit account number as a string and verifies it doesn't exist in the database.
    /// </summary>
    public static async Task<string> GenerateUniqueAccountNumberAsync(AppDbContext db)
    {
        string accountNumber;
        bool exists;

        do
        {
            // Generate a random 10-digit string
            var chars = new char[10];
            for (int i = 0; i < 10; i++)
            {
                chars[i] = (char)('0' + Random.Shared.Next(0, 10));
            }
            accountNumber = new string(chars);

            // Check if this account number already exists in the database
            exists = await db.Accounts.AnyAsync(a => a.AccountNumber == accountNumber);

        } while (exists); // If it exists, loop again to generate a new one

        return accountNumber;
    }
}