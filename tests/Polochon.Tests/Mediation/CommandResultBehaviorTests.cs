using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Results;
using Polochon.Mediation;
using Xunit;

namespace Polochon.Tests.Mediation
{
    /// <summary>
    /// Tests for <see cref="CommandResultBehavior{TRequest, TResponse}"/>: every failure of a command
    /// returning a <see cref="CommandResult"/> reaches its caller as a <see cref="ResultCode"/>.
    /// </summary>
    public sealed class CommandResultBehaviorTests
    {
        private static readonly ResultCode RuleCode = new() { Code = 1001, Status = "RULE_BROKEN" };

        [Fact(DisplayName = "Successful command reports 0/OK")]
        public async Task SuccessReportsOk()
        {
            var dispatcher = CreateDispatcher();

            var result = await dispatcher.SendCommandAsync(new ResultCommand());

            Assert.True(result.IsSuccess);
            Assert.Equal(ResultCode.Ok, result.Result);
        }

        [Fact(DisplayName = "BusinessRuleException reports its own code")]
        public async Task BusinessRuleExceptionReportsItsCode()
        {
            var dispatcher = CreateDispatcher();

            var result = await dispatcher.SendCommandAsync(new ResultCommand { ToThrow = new BusinessRuleException(RuleCode) });

            Assert.False(result.IsSuccess);
            Assert.Equal(RuleCode, result.Result);
        }

        [Fact(DisplayName = "Coded validation failure reports its code and skips the handler")]
        public async Task CodedValidationFailureReportsItsCode()
        {
            var dispatcher = CreateDispatcher();
            var command = new ResultCommand { RejectWith = new MessageValidationException(RuleCode, "rejected") };

            var result = await dispatcher.SendCommandAsync(command);

            Assert.Equal(RuleCode, result.Result);
            Assert.False(command.Handled);
        }

        [Fact(DisplayName = "Uncoded validation failure reports -2/VALIDATION_FAILED")]
        public async Task UncodedValidationFailureReportsValidationFailed()
        {
            var dispatcher = CreateDispatcher();

            var result = await dispatcher.SendCommandAsync(new ResultCommand { RejectWith = new MessageValidationException("rejected") });

            Assert.Equal(ResultCode.ValidationFailed, result.Result);
        }

        [Fact(DisplayName = "Unexpected exception reports -1/UNEXPECTED_ERROR")]
        public async Task UnexpectedExceptionReportsUnexpectedError()
        {
            var dispatcher = CreateDispatcher();

            var result = await dispatcher.SendCommandAsync(new ResultCommand { ToThrow = new InvalidOperationException("boom") });

            Assert.Equal(ResultCode.UnexpectedError, result.Result);
        }

        [Fact(DisplayName = "Cancellation still propagates")]
        public async Task CancellationPropagates()
        {
            var dispatcher = CreateDispatcher();
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await dispatcher.SendCommandAsync(new ResultCommand { ToThrow = new OperationCanceledException(cts.Token) }, cts.Token));
        }

        [Fact(DisplayName = "Command with another response type still throws")]
        public async Task OtherResponseTypePassesThrough()
        {
            var dispatcher = CreateDispatcher();

            _ = await Assert.ThrowsAsync<BusinessRuleException>(async () =>
                await dispatcher.SendCommandAsync(new PlainCommand()));
        }

        private static IPolochonDispatcher CreateDispatcher()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDispatcher([typeof(ResultCommandHandler), typeof(ResultCommandValidator), typeof(PlainCommandHandler)]);
            services.AddPipelineBehavior(typeof(CommandResultBehavior<,>));

            return services.BuildServiceProvider().GetRequiredService<IPolochonDispatcher>();
        }

        private sealed class ResultCommand : ICommand<CommandResult>
        {
            public Exception? ToThrow { get; init; }

            public MessageValidationException? RejectWith { get; init; }

            public bool Handled { get; set; }
        }

        private sealed class ResultCommandValidator : IMessageValidator<ResultCommand, CommandResult>
        {
            public ValueTask ValidateAsync(ResultCommand request, CancellationToken cancellationToken)
                => request.RejectWith is null ? ValueTask.CompletedTask : throw request.RejectWith;
        }

        private sealed class ResultCommandHandler : ICommandHandler<ResultCommand, CommandResult>
        {
            public ValueTask<CommandResult> HandleAsync(ResultCommand command, CancellationToken cancellationToken = default)
            {
                command.Handled = true;

                return command.ToThrow is null
                    ? ValueTask.FromResult(CommandResult.Success())
                    : throw command.ToThrow;
            }
        }

        private sealed class PlainCommand : ICommand<string>
        {
        }

        private sealed class PlainCommandHandler : ICommandHandler<PlainCommand, string>
        {
            public ValueTask<string> HandleAsync(PlainCommand command, CancellationToken cancellationToken = default)
                => throw new BusinessRuleException(RuleCode);
        }
    }
}
