using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Morty.Infrastructure.Data;

namespace Morty.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating DbContext instances during migrations
/// </summary>
public class MortyDbContextFactory : IDesignTimeDbContextFactory<MortyDbContext>
{
    public MortyDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MortyDbContext>();
        optionsBuilder.UseSqlite("Data Source=morty.db");
        return new MortyDbContext(optionsBuilder.Options);
    }
}
