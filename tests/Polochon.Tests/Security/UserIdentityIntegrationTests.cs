using System.Security.Claims;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Domain;
using Polochon.Abstractions.Messaging;
using Polochon.Abstractions.Modules;
using Polochon.Abstractions.Security;
using Polochon.Messaging;
using Polochon.Modules;
using Polochon.Security;
using Xunit;

namespace Polochon.Tests.Security
{
    /// <summary>
    /// Tests for the registration of <see cref="IUserIdentityProvider"/> in the host and in module
    /// containers, and for its use by a command validator: from the host caller, and from the
    /// inbox processor, which acts under <see cref="SystemIdentity"/>.
    /// </summary>
    public sealed class UserIdentityIntegrationTests
    {
        /// <summary>A command only managers, or the system, may send.</summary>
        public sealed record GuardedCommand : ICommand
        {
            /// <summary>Creates the command with the given payload.</summary>
            public GuardedCommand(string payload)
            {
                Payload = payload;
            }

            /// <summary>An arbitrary marker value.</summary>
            public string Payload { get; init; }
        }

        /// <summary>An integration event mapped to <see cref="GuardedCommand"/>.</summary>
        public sealed record GuardedEvent : IIntegrationEvent
        {
            /// <summary>Creates the event with the given payload.</summary>
            public GuardedEvent(string payload)
            {
                Payload = payload;
            }

            /// <summary>An arbitrary marker value.</summary>
            public string Payload { get; init; }

            /// <inheritdoc/>
            public Guid Id { get; init; } = Guid.NewGuid();

            /// <inheritdoc/>
            public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
        }

        /// <summary>Records who each handled command was sent by.</summary>
        public sealed class IdentityRecorder
        {
            /// <summary>The display names of the users whose commands were handled.</summary>
            public List<string?> HandledBy { get; } = [];
        }

        /// <summary>Allows <see cref="GuardedCommand"/> for managers and the system only.</summary>
        public sealed class GuardedCommandValidator : IMessageValidator<GuardedCommand, Unit>
        {
            private readonly IUserIdentityProvider user;

            /// <summary>Creates the validator.</summary>
            public GuardedCommandValidator(IUserIdentityProvider user)
            {
                this.user = user;
            }

            /// <inheritdoc/>
            public ValueTask ValidateAsync(GuardedCommand request, CancellationToken cancellationToken)
                => user.IsInRole("InventoryManager") || user.IsInRole(SystemIdentity.Role)
                    ? ValueTask.CompletedTask
                    : throw new MessageValidationException($"'{user.DisplayName}' may not send {nameof(GuardedCommand)}.");
        }

        /// <summary>Records the user the command was handled for.</summary>
        public sealed class GuardedCommandHandler : ICommandHandler<GuardedCommand, Unit>
        {
            private readonly IUserIdentityProvider user;
            private readonly IdentityRecorder recorder;

            /// <summary>Creates the handler.</summary>
            public GuardedCommandHandler(IUserIdentityProvider user, IdentityRecorder recorder)
            {
                this.user = user;
                this.recorder = recorder;
            }

            /// <inheritdoc/>
            public ValueTask<Unit> HandleAsync(GuardedCommand command, CancellationToken cancellationToken = default)
            {
                recorder.HandledBy.Add(user.DisplayName);
                return ValueTask.FromResult(Unit.Value);
            }
        }

        /// <summary>Translates <see cref="GuardedEvent"/> into <see cref="GuardedCommand"/>.</summary>
        public sealed class GuardedEventToCommandHandler : IntegrationEventToCommandHandler<GuardedEvent, GuardedCommand>
        {
            /// <summary>Creates the handler.</summary>
            public GuardedEventToCommandHandler(IPolochonDispatcher dispatcher)
                : base(dispatcher)
            {
            }

            /// <inheritdoc/>
            protected override GuardedCommand Map(GuardedEvent integrationEvent) => new(integrationEvent.Payload);
        }

        private sealed class FakeModule : ModuleBase
        {
            public FakeModule()
                : base("fake-identity", [typeof(UserIdentityIntegrationTests).Assembly])
            {
            }

            protected override void ConfigureAdditionalServices(IServiceCollection services)
            {
                services.AddSingleton<IdentityRecorder>();
            }
        }

        private sealed class FakeUserIdentityProvider : IUserIdentityProvider
        {
            public bool IsAuthenticated => false;

            public string? Email => null;

            public string? DisplayName => "fake";

            public ClaimsPrincipal UserPrincipal { get; } = new(new ClaimsIdentity());

            public bool IsInRole(string role) => false;
        }

        private static ClaimsPrincipal CreateUser(string name, params string[] roles)
            => new(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, name), .. roles.Select(role => new Claim(ClaimTypes.Role, role))],
                "Test"));

        /// <summary>The host gets the default provider as a singleton.</summary>
        [Fact(DisplayName = "AddPolochon registers the default provider as a singleton")]
        public void AddPolochonRegistersDefaultProviderAsSingleton()
        {
            var services = new ServiceCollection();
            _ = services.AddPolochon();

            var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IUserIdentityProvider));
            Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
            Assert.Equal(typeof(ClaimsUserIdentityProvider), descriptor.ImplementationType);
        }

        /// <summary>A provider registered by the host before <c>AddPolochon()</c> is kept.</summary>
        [Fact(DisplayName = "AddPolochonUserIdentity keeps an existing registration")]
        public void AddPolochonUserIdentityKeepsExistingRegistration()
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<IUserIdentityProvider, FakeUserIdentityProvider>();
            _ = services.AddPolochonUserIdentity();

            using var provider = services.BuildServiceProvider();

            _ = Assert.IsType<FakeUserIdentityProvider>(provider.GetRequiredService<IUserIdentityProvider>());
        }

        /// <summary>Every module container gets the default provider.</summary>
        [Fact(DisplayName = "Module container resolves the default provider")]
        public async Task ModuleContainerResolvesDefaultProvider()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();

            _ = Assert.IsType<ClaimsUserIdentityProvider>(module.GetRequiredService<IUserIdentityProvider>());
        }

        /// <summary><c>WithUserIdentity</c> replaces the provider in the module container only.</summary>
        [Fact(DisplayName = "WithUserIdentity replaces the provider in the module container")]
        public async Task WithUserIdentityReplacesProviderInModule()
        {
            var services = new ServiceCollection();
            _ = services.AddPolochonUserIdentity();
            _ = services.AddModule<FakeModule>().WithUserIdentity(_ => new FakeUserIdentityProvider());

            await using var provider = services.BuildServiceProvider();
            var module = (FakeModule)provider.GetRequiredService<IModularModule>();
            await module.InitializeAsync();

            _ = Assert.IsType<FakeUserIdentityProvider>(module.GetRequiredService<IUserIdentityProvider>());
            _ = Assert.IsType<ClaimsUserIdentityProvider>(provider.GetRequiredService<IUserIdentityProvider>());
        }

        /// <summary><c>WithUserIdentity</c> returns the same builder instance for chaining.</summary>
        [Fact(DisplayName = "WithUserIdentity returns the same builder instance")]
        public void WithUserIdentityReturnsSameBuilderInstance()
        {
            var builder = new ServiceCollection().AddModule<FakeModule>();

            Assert.Same(builder, builder.WithUserIdentity(_ => new FakeUserIdentityProvider()));
        }

        /// <summary><c>WithUserIdentity</c> rejects a null factory.</summary>
        [Fact(DisplayName = "WithUserIdentity throws when factory is null")]
        public void WithUserIdentityWhenFactoryNullThrows()
        {
            var builder = new ServiceCollection().AddModule<FakeModule>();

            _ = Assert.Throws<ArgumentNullException>(() => builder.WithUserIdentity(null!));
        }

        /// <summary>
        /// The user set by the host caller flows into the module's isolated container, where the
        /// validator reads it: a new user on each call, despite the singleton provider.
        /// </summary>
        [Fact(DisplayName = "Validator in module allows the user set by the host caller")]
        public async Task ValidatorInModuleAllowsHostCaller()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<IdentityRecorder>();

            using (AmbientUserContext.Use(CreateUser("manager", "InventoryManager")))
            {
                await module.SendCommandAsync(new GuardedCommand("allowed"));
            }

            Assert.Equal(["manager"], recorder.HandledBy);
        }

        /// <summary>
        /// A user the validator rejects gets a <see cref="MessageValidationException"/>, and the
        /// handler is not called.
        /// </summary>
        [Fact(DisplayName = "Validator in module rejects an unauthorized host caller")]
        public async Task ValidatorInModuleRejectsUnauthorizedHostCaller()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<IdentityRecorder>();

            using (AmbientUserContext.Use(CreateUser("teacher", "Teacher")))
            {
                var exception = await Assert.ThrowsAsync<MessageValidationException>(
                    async () => await module.SendCommandAsync(new GuardedCommand("blocked")));

                Assert.Contains("'teacher'", exception.Message, StringComparison.Ordinal);
            }

            Assert.Empty(recorder.HandledBy);
        }

        /// <summary>Without an ambient user, the caller is anonymous and is rejected.</summary>
        [Fact(DisplayName = "Validator in module rejects an anonymous caller")]
        public async Task ValidatorInModuleRejectsAnonymousCaller()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<IdentityRecorder>();

            _ = await Assert.ThrowsAsync<MessageValidationException>(
                async () => await module.SendCommandAsync(new GuardedCommand("anonymous")));

            Assert.Empty(recorder.HandledBy);
        }

        /// <summary>
        /// A command mapped from an integration event by the inbox processor is sent under the
        /// system identity, so it passes a validator that trusts the system.
        /// </summary>
        [Fact(DisplayName = "Inbox processor sends mapped commands under the system identity")]
        public async Task InboxProcessorSendsMappedCommandsUnderSystemIdentity()
        {
            await using var module = new FakeModule();
            await module.InitializeAsync();
            var recorder = module.GetRequiredService<IdentityRecorder>();

            var inboxWriter = module.GetRequiredService<IInboxWriter>();
            await inboxWriter.PublishMessageAsync(new GuardedEvent("via-inbox"), CancellationToken.None);

            var processor = new InboxProcessor(module, TimeSpan.FromMilliseconds(20), NullLogger<InboxProcessor>.Instance);
            await processor.StartAsync(CancellationToken.None);
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(5);
                while (recorder.HandledBy.Count == 0 && DateTime.UtcNow < deadline)
                {
                    await Task.Delay(10);
                }

                Assert.Equal(["fake-identity inbox processor"], recorder.HandledBy);
            }
            finally
            {
                await processor.StopAsync(CancellationToken.None);
            }
        }
    }
}
