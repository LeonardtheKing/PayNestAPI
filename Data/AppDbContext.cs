using LoanAPI.Entities;
using Microsoft.EntityFrameworkCore;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Transactions> Transactions => Set<Transactions>();
    public DbSet<Bank> Banks => Set<Bank>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Beneficiary> Beneficiaries => Set<Beneficiary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Transactions>()
            .Property(t => t.Amount)
            .HasColumnType("decimal(18,2)");

    }
}