using i26.Core.Pagination;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace i26.EntityFrameworkCore.Tests.Queries;

/// <summary>Pageable as well as queryable, so one model serves both halves of these tests.</summary>
public sealed class Invoice : ICursorPageable
{
    public Guid Id { get; set; }

    public int Number { get; set; }

    public decimal Amount { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class InvoiceDbContext(DbContextOptions<InvoiceDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite has no date type and refuses to order by DateTimeOffset; the binary converter is
        // the documented way around it and keeps the ordering intact.
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
    }
}
