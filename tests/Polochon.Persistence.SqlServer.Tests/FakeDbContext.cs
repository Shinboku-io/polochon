using Microsoft.EntityFrameworkCore;

namespace Polochon.Persistence.SqlServer.Tests
{
    /// <summary>
    /// Minimal EF Core context used to exercise <c>WithSqlServer()</c> swapping a module's
    /// default provider.
    /// </summary>
    public sealed class FakeDbContext : DbContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FakeDbContext"/> class.
        /// </summary>
        public FakeDbContext(DbContextOptions<FakeDbContext> options)
            : base(options)
        {
        }
    }
}
