using Microsoft.EntityFrameworkCore;

namespace App.Shared;

/// <summary>
/// Host-level requests that do not require a resolved tenant context.
/// </summary>
public interface ITenantExemptRequest;

public interface ITenantQueryFilterConfigurator
{
    void Configure(ModelBuilder modelBuilder, AppDbContext dbContext);
}
