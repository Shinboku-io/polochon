using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Modules;

namespace Polochon.Persistence.SqlServer.Tests
{
    /// <summary>
    /// Minimal <see cref="ModuleBase"/> whose default persistence registration (in-memory,
    /// Scoped) stands in for a real module's own default - the thing <c>WithSqlServer()</c> must
    /// be proven to override.
    /// </summary>
    public sealed class FakeModule : ModuleBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FakeModule"/> class.
        /// </summary>
        public FakeModule()
            : base("fake", [typeof(FakeModule).Assembly])
        {
        }

        /// <inheritdoc/>
        protected override void ConfigureAdditionalServices(IServiceCollection services)
        {
            services.AddDbContextFactory<FakeDbContext>(options => options.UseInMemoryDatabase("fake"), ServiceLifetime.Scoped);
        }
    }
}
