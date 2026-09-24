using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Modules;
using Polochon.Modules;
using Xunit;

namespace Polochon.Tests.Modules
{
    /// <summary>
    /// Tests that every query/command sent to a module is handled in a DI scope of its own, so
    /// scoped services (unit of work, DbContext...) are never shared between two messages - while a
    /// message a handler sends through its injected dispatcher stays in the sender's scope.
    /// </summary>
    public sealed class ModuleScopeTests : IAsyncLifetime
    {
        private readonly ScopeModule module = new();

        /// <summary>Initializes the module before each test.</summary>
        public async ValueTask InitializeAsync() => await module.InitializeAsync();

        /// <summary>Disposes the module after each test.</summary>
        public async ValueTask DisposeAsync() => await module.DisposeAsync();

        [Fact(DisplayName = "Each command gets its own scope")]
        public async Task EachCommandGetsItsOwnScope()
        {
            IModularModule module = this.module;

            var first = await module.SendCommandAsync(new ProbeScope());
            var second = await module.SendCommandAsync(new ProbeScope());

            Assert.NotEqual(first.ScopeId, second.ScopeId);
        }

        [Fact(DisplayName = "Each query gets its own scope")]
        public async Task EachQueryGetsItsOwnScope()
        {
            IModularModule module = this.module;

            var first = await module.SendQueryAsync(new ProbeQueryScope());
            var second = await module.SendQueryAsync(new ProbeQueryScope());

            Assert.NotEqual(first.ScopeId, second.ScopeId);
        }

        [Fact(DisplayName = "The scope is disposed once the command completes")]
        public async Task ScopeIsDisposedAfterCommand()
        {
            IModularModule module = this.module;

            var probe = await module.SendCommandAsync(new ProbeScope());

            Assert.True(probe.Disposed);
        }

        [Fact(DisplayName = "A command sent from a handler stays in the sender's scope")]
        public async Task NestedCommandSharesTheScope()
        {
            IModularModule module = this.module;

            var (outer, inner) = await module.SendCommandAsync(new ProbeNestedScope());

            Assert.Equal(outer.ScopeId, inner.ScopeId);
        }

        /// <summary>Scoped service recording which scope it belongs to.</summary>
        public sealed class ScopeProbe : IDisposable
        {
            public Guid ScopeId { get; } = Guid.NewGuid();

            public bool Disposed { get; private set; }

            public void Dispose() => Disposed = true;
        }

        public sealed record ProbeScope : ICommand<ScopeProbe>;

        public sealed record ProbeQueryScope : IQuery<ScopeProbe>;

        public sealed record ProbeNestedScope : ICommand<(ScopeProbe Outer, ScopeProbe Inner)>;

        public sealed class ProbeScopeHandler : ICommandHandler<ProbeScope, ScopeProbe>
        {
            private readonly ScopeProbe probe;

            public ProbeScopeHandler(ScopeProbe probe)
            {
                this.probe = probe;
            }

            public ValueTask<ScopeProbe> HandleAsync(ProbeScope command, CancellationToken cancellationToken = default)
                => ValueTask.FromResult(probe);
        }

        public sealed class ProbeQueryScopeHandler : IQueryHandler<ProbeQueryScope, ScopeProbe>
        {
            private readonly ScopeProbe probe;

            public ProbeQueryScopeHandler(ScopeProbe probe)
            {
                this.probe = probe;
            }

            public ValueTask<ScopeProbe> HandleAsync(ProbeQueryScope query, CancellationToken cancellationToken)
                => ValueTask.FromResult(probe);
        }

        public sealed class ProbeNestedScopeHandler : ICommandHandler<ProbeNestedScope, (ScopeProbe Outer, ScopeProbe Inner)>
        {
            private readonly ScopeProbe probe;
            private readonly IPolochonDispatcher dispatcher;

            public ProbeNestedScopeHandler(ScopeProbe probe, IPolochonDispatcher dispatcher)
            {
                this.probe = probe;
                this.dispatcher = dispatcher;
            }

            public async ValueTask<(ScopeProbe Outer, ScopeProbe Inner)> HandleAsync(ProbeNestedScope command, CancellationToken cancellationToken = default)
                => (probe, await dispatcher.SendCommandAsync(new ProbeScope(), cancellationToken));
        }

        private sealed class ScopeModule : ModuleBase
        {
            public ScopeModule()
                : base("ScopeModule", [typeof(ScopeModule).Assembly])
            {
            }

            protected override void ConfigureAdditionalServices(IServiceCollection services)
                => services.AddScoped<ScopeProbe>();
        }
    }
}
