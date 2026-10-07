using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polochon.Abstractions.Modules;

namespace Polochon.Persistence.SqlServer
{
    /// <summary>
    /// Lets a module swap its default EF Core provider for SQL Server. Module-level only - unlike
    /// <c>Polochon.Serilog</c>'s host-level <c>WithSerilog()</c>, there is no base, host-level
    /// persistence registration to swap: only modules own <see cref="DbContext"/>s.
    /// </summary>
    public static class ServiceRegister
    {
        // A dedicated internal service provider for the SQL Server provider's own EF Core
        // machinery, built once and shared by every WithSqlServer() call in this process - the
        // EF Core-recommended way to use a provider without registering its internal services
        // into the ambient application container. Without this, a module's own default (e.g.
        // UseInMemoryDatabase, registered into that same ambient container) and this override
        // would both leave their provider's services behind in it, and EF Core refuses to pick
        // between two registered providers ("Only a single database provider can be registered
        // in a service provider").
        private static readonly IServiceProvider SqlServerInternalServices =
            new ServiceCollection().AddEntityFrameworkSqlServer().BuildServiceProvider();

        /// <summary>
        /// Swaps <typeparamref name="TContext"/>'s default EF Core provider for SQL Server, using a
        /// fixed connection string. Queues a callback (via <see cref="IModularModuleBuilder{TModule}.ConfigureModule"/>)
        /// that runs after the module's own <c>ConfigureAdditionalServices</c> default, so it always wins.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <typeparam name="TContext">The EF Core context type to reconfigure.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="configureSqlServer">Optional SQL Server-specific options (retry policy, migrations assembly, ...).</param>
        public static IModularModuleBuilder<TModule> WithSqlServer<TModule, TContext>(
            this IModularModuleBuilder<TModule> builder,
            string connectionString,
            Action<SqlServerDbContextOptionsBuilder>? configureSqlServer = null)
            where TModule : IModularModule
            where TContext : DbContext
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

            return builder.WithSqlServer<TModule, TContext>(_ => connectionString, configureSqlServer);
        }

        /// <summary>
        /// Swaps <typeparamref name="TContext"/>'s default EF Core provider for SQL Server, resolving
        /// the connection string from the module's own service provider at options-build time (e.g.
        /// from configuration, or a per-tenant resolver). Safe to depend on scoped services inside
        /// <paramref name="connectionStringFactory"/> specifically because the registration this
        /// produces is always <see cref="ServiceLifetime.Scoped"/>, not the EF default (Singleton) -
        /// a Singleton factory's options callback would only ever see the root provider.
        /// </summary>
        /// <typeparam name="TModule">The module type this builder was created for.</typeparam>
        /// <typeparam name="TContext">The EF Core context type to reconfigure.</typeparam>
        /// <param name="builder">The module builder to configure.</param>
        /// <param name="connectionStringFactory">Resolves the connection string given the module's service provider.</param>
        /// <param name="configureSqlServer">Optional SQL Server-specific options (retry policy, migrations assembly, ...).</param>
        public static IModularModuleBuilder<TModule> WithSqlServer<TModule, TContext>(
            this IModularModuleBuilder<TModule> builder,
            Func<IServiceProvider, string> connectionStringFactory,
            Action<SqlServerDbContextOptionsBuilder>? configureSqlServer = null)
            where TModule : IModularModule
            where TContext : DbContext
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(connectionStringFactory);

            return builder.ConfigureModule((services, _, _) =>
            {
                // AddDbContextFactory<TContext>() registers DbContextOptions<TContext>, the
                // non-generic DbContextOptions forwarder, IDbContextFactory<TContext>, and
                // TContext itself (as a scoped service) via TryAdd - so calling it again here,
                // after the module's own default (e.g. UseInMemoryDatabase) already ran, would
                // silently do nothing unless what's already there is removed first. RemoveAll on
                // the non-generic DbContextOptions assumes one DbContext type per module, true of
                // every module in this kernel today.
                services.RemoveAll<TContext>();
                services.RemoveAll<DbContextOptions<TContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextFactory<TContext>>();

                services.AddDbContextFactory<TContext>(
                    (sp, optionsBuilder) => optionsBuilder
                        .UseInternalServiceProvider(SqlServerInternalServices)
                        .UseSqlServer(connectionStringFactory(sp), configureSqlServer),
                    ServiceLifetime.Scoped);
            });
        }
    }
}
