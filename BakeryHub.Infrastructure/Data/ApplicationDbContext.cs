using BakeryHub.Domain.Entities;
using BakeryHub.Domain.Interfaces;
using BakeryHub.Modules.Accounts.Domain.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BakeryHub.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IUnitOfWork
{
    private IDbContextTransaction? _currentTransaction;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantTheme> TenantThemes { get; set; }


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
        foreach (var assembly in loadedAssemblies)
        {
            builder.ApplyConfigurationsFromAssembly(assembly);
        }
    }

    private class DbTransaction : IDbTransaction
    {
        private readonly IDbContextTransaction _transaction;
        public DbTransaction(IDbContextTransaction transaction) => _transaction = transaction;
        public Task CommitAsync(CancellationToken cancellationToken = default) => _transaction.CommitAsync(cancellationToken);
        public Task RollbackAsync(CancellationToken cancellationToken = default) => _transaction.RollbackAsync(cancellationToken);
        public void Dispose() => _transaction.Dispose();
        public ValueTask DisposeAsync() => _transaction.DisposeAsync();
    }

    public async Task<IDbTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null) throw new InvalidOperationException("A transaction is already in progress.");
        _currentTransaction = await Database.BeginTransactionAsync(cancellationToken);
        return new DbTransaction(_currentTransaction);
    }

    public async Task ExecuteStrategyAsync(Func<Task> operation)
    {
        var strategy = Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(operation);
    }
}
