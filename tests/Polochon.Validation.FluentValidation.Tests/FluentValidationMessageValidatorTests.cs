using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Results;
using Polochon.Mediation;
using Polochon.Validation.FluentValidation;
using Xunit;

namespace Polochon.Validation.FluentValidation.Tests
{
    /// <summary>
    /// Tests for <see cref="FluentValidationMessageValidator{TRequest, TResponse}"/> and its
    /// registration: plain FluentValidation validators reject commands through the kernel, which
    /// reports the first failure as a <see cref="ResultCode"/>.
    /// </summary>
    public sealed class FluentValidationMessageValidatorTests
    {
        private static readonly ResultCode NameRequired = new() { Code = 1001, Status = "NAME_REQUIRED" };
        private static readonly ResultCode NameTooLong = new() { Code = 1002, Status = "NAME_TOO_LONG" };
        private static readonly ResultCode SizeNegative = new() { Code = 1003, Status = "SIZE_NEGATIVE" };

        [Fact(DisplayName = "Valid command reports 0/OK and reaches the handler")]
        public async Task ValidCommandReportsOk()
        {
            var command = new CreateThing { Name = "thing", Size = 1 };

            var result = await CreateDispatcher().SendCommandAsync(command);

            Assert.Equal(ResultCode.Ok, result.Result);
            Assert.True(command.Handled);
        }

        [Fact(DisplayName = "First failing rule is reported and the handler is skipped")]
        public async Task FirstFailureIsReported()
        {
            var command = new CreateThing { Name = string.Empty, Size = -1 };

            var result = await CreateDispatcher().SendCommandAsync(command);

            Assert.Equal(NameRequired, result.Result);
            Assert.False(command.Handled);
        }

        [Fact(DisplayName = "Every rule is evaluated and every failure kept on the exception")]
        public async Task EveryFailureIsKept()
        {
            var dispatcher = CreateDispatcher(withResultBehavior: false);

            var ex = await Assert.ThrowsAsync<MessageValidationException>(async () =>
                await dispatcher.SendCommandAsync(new CreateThing { Name = new string('x', 20), Size = -1 }));

            Assert.Equal(NameTooLong, ex.Error);
            Assert.Equal([NameTooLong, SizeNegative, ResultCode.ValidationFailed], ex.Errors.Select(e => e.Code));
            Assert.Equal(nameof(CreateThing.Name), ex.Errors[0].MemberName);
        }

        [Fact(DisplayName = "Failing rule without a code reports -2/VALIDATION_FAILED")]
        public async Task RuleWithoutCodeReportsValidationFailed()
        {
            var result = await CreateDispatcher().SendCommandAsync(new CreateThing { Name = "thing", Size = 1000 });

            Assert.Equal(ResultCode.ValidationFailed, result.Result);
        }

        [Fact(DisplayName = "Registering twice runs the validators once")]
        public void RegisteringTwiceIsIdempotent()
        {
            var services = new ServiceCollection();
            services.AddPolochonFluentValidation(typeof(CreateThingValidator).Assembly);
            services.AddPolochonFluentValidation(typeof(CreateThingValidator).Assembly);

            using var provider = services.BuildServiceProvider();

            Assert.Single(provider.GetServices<IMessageValidator<CreateThing, CommandResult>>());
            Assert.Equal(2, provider.GetServices<IValidator<CreateThing>>().Count());
        }

        private static IPolochonDispatcher CreateDispatcher(bool withResultBehavior = true)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDispatcher([typeof(CreateThingHandler)]);
            services.AddPolochonFluentValidation(typeof(CreateThingValidator).Assembly);

            if (withResultBehavior)
            {
                services.AddPipelineBehavior(typeof(CommandResultBehavior<,>));
            }

            return services.BuildServiceProvider().GetRequiredService<IPolochonDispatcher>();
        }

        private sealed class CreateThing : ICommand<CommandResult>
        {
            public string Name { get; init; } = string.Empty;

            public int Size { get; init; }

            public bool Handled { get; set; }
        }

        private sealed class CreateThingValidator : AbstractValidator<CreateThing>
        {
            public CreateThingValidator()
            {
                RuleFor(c => c.Name)
                    .NotEmpty().WithResultCode(NameRequired)
                    .MaximumLength(10).WithResultCode(NameTooLong);

                RuleFor(c => c.Size)
                    .GreaterThanOrEqualTo(0).WithResultCode(SizeNegative);
            }
        }

        // A second validator for the same command: its failures come after the first validator's.
        private sealed class CreateThingSizeLimitValidator : AbstractValidator<CreateThing>
        {
            public CreateThingSizeLimitValidator()
            {
                RuleFor(c => c.Size).Must(size => size < 100 && size != -1);
            }
        }

        private sealed class CreateThingHandler : ICommandHandler<CreateThing, CommandResult>
        {
            public ValueTask<CommandResult> HandleAsync(CreateThing command, CancellationToken cancellationToken = default)
            {
                command.Handled = true;
                return ValueTask.FromResult(CommandResult.Success());
            }
        }
    }
}
