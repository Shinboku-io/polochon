using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.Modules;
using Polochon.Modules;
using Xunit;

namespace Polochon.Persistence.SqlServer.Tests
{
    /// <summary>
    /// Tests for <see cref="ServiceRegister"/>'s <c>WithSqlServer()</c> overloads.
    /// </summary>
    public sealed class ServiceRegisterTests
    {
        private const string ConnectionString = "Server=(local);Database=Fake;TrustServerCertificate=True;";

        private sealed class ScopedMarker
        {
        }

        /// <summary>
        /// Tests that WithSqlServer() actually replaces the module's default in-memory provider,
        /// rather than being a silent no-op (the central risk this whole package defends against,
        /// since EF Core's AddDbContextFactory uses TryAdd-style registration internally).
        /// </summary>
        [Fact]
        public async Task WithSqlServer_ReplacesTheModulesDefaultProvider()
        {
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();
            _ = builder.WithSqlServer<FakeModule, FakeDbContext>(ConnectionString);

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            var factory = module.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            using var context = factory.CreateDbContext();

            Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", context.Database.ProviderName);
        }

        /// <summary>
        /// Tests that the connection string and SQL Server-specific options passed to
        /// WithSqlServer() are the ones actually applied.
        /// </summary>
        [Fact]
        public async Task WithSqlServer_AppliesTheGivenConnectionStringAndOptions()
        {
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();
            _ = builder.WithSqlServer<FakeModule, FakeDbContext>(ConnectionString, sql => sql.CommandTimeout(42));

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            var factory = module.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            using var context = factory.CreateDbContext();

            // SqlConnectionStringBuilder normalizes keywords on round-trip (e.g. "Server=" becomes
            // "Data Source="), so compare parsed values rather than the raw strings.
            var expected = new SqlConnectionStringBuilder(ConnectionString);
            var actual = new SqlConnectionStringBuilder(context.Database.GetConnectionString());
            Assert.Equal(expected.DataSource, actual.DataSource);
            Assert.Equal(expected.InitialCatalog, actual.InitialCatalog);
            Assert.Equal(42, context.Database.GetCommandTimeout());
        }

        /// <summary>
        /// Tests that WithSqlServer() registers the factory as genuinely Scoped: the same instance
        /// within one scope, a different instance across scopes. This is the property the "must be
        /// registered scoped too" fix exists for - EF Core's own default is Singleton.
        /// </summary>
        [Fact]
        public async Task WithSqlServer_RegistersTheFactory_AsScoped()
        {
            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>();
            _ = builder.WithSqlServer<FakeModule, FakeDbContext>(ConnectionString);

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            using var scopeA = module.ServiceProvider.CreateScope();
            var factoryA1 = scopeA.ServiceProvider.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            var factoryA2 = scopeA.ServiceProvider.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            Assert.Same(factoryA1, factoryA2);

            using var scopeB = module.ServiceProvider.CreateScope();
            var factoryB1 = scopeB.ServiceProvider.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            Assert.NotSame(factoryA1, factoryB1);
        }

        /// <summary>
        /// Tests that the connection-string factory overload receives the actual ambient scope's
        /// provider (not some disconnected/root provider) - only possible because the registration
        /// is Scoped, not the EF default Singleton, which would only ever see the root provider.
        /// </summary>
        [Fact]
        public async Task WithSqlServer_ConnectionStringFactory_ReceivesTheAmbientScopedProvider()
        {
            ScopedMarker? markerSeenByFactory = null;

            var services = new ServiceCollection();
            var builder = services.AddModule<FakeModule>()
                .ConfigureModule((moduleServices, _, _) => moduleServices.AddScoped<ScopedMarker>());
            _ = builder.WithSqlServer<FakeModule, FakeDbContext>(sp =>
            {
                markerSeenByFactory = sp.GetRequiredService<ScopedMarker>();
                return ConnectionString;
            });

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            using var scope = module.ServiceProvider.CreateScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            using var context = factory.CreateDbContext();
            var markerFromSameScope = scope.ServiceProvider.GetRequiredService<ScopedMarker>();

            Assert.NotNull(markerSeenByFactory);
            Assert.Same(markerFromSameScope, markerSeenByFactory);
        }

        /// <summary>Tests that a module never calling WithSqlServer() is unaffected.</summary>
        [Fact]
        public async Task Module_NotCallingWithSqlServer_StaysOnItsDefaultProvider()
        {
            var services = new ServiceCollection();
            _ = services.AddModule<FakeModule>();

            using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            var factory = module.GetRequiredService<IDbContextFactory<FakeDbContext>>();
            using var context = factory.CreateDbContext();

            Assert.Equal("Microsoft.EntityFrameworkCore.InMemory", context.Database.ProviderName);
        }

        /// <summary>Tests that WithSqlServer(connectionString) throws on a null/empty connection string.</summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void WithSqlServer_WithInvalidConnectionString_Throws(string? connectionString)
        {
            var builder = new ServiceCollection().AddModule<FakeModule>();

            // ThrowIfNullOrWhiteSpace throws ArgumentNullException for null, ArgumentException for
            // whitespace/empty - both are fine here, just not some other exception type.
            _ = Assert.ThrowsAny<ArgumentException>(() => builder.WithSqlServer<FakeModule, FakeDbContext>(connectionString!));
        }

        /// <summary>Tests that the connection-string-factory overload throws on a null factory.</summary>
        [Fact]
        public void WithSqlServer_WithNullConnectionStringFactory_Throws()
        {
            var builder = new ServiceCollection().AddModule<FakeModule>();

            _ = Assert.Throws<ArgumentNullException>(
                () => builder.WithSqlServer<FakeModule, FakeDbContext>((Func<IServiceProvider, string>)null!));
        }

        /// <summary>Tests that WithSqlServer() throws on a null builder.</summary>
        [Fact]
        public void WithSqlServer_WithNullBuilder_Throws()
        {
            IModularModuleBuilder<FakeModule> builder = null!;

            _ = Assert.Throws<ArgumentNullException>(() => builder.WithSqlServer<FakeModule, FakeDbContext>(ConnectionString));
        }

        /// <summary>Tests that WithSqlServer() returns the same builder instance for chaining.</summary>
        [Fact]
        public void WithSqlServer_ReturnsSameBuilderInstance()
        {
            var builder = new ServiceCollection().AddModule<FakeModule>();

            var result = builder.WithSqlServer<FakeModule, FakeDbContext>(ConnectionString);

            Assert.Same(builder, result);
        }
    }
}
