using Microsoft.Extensions.Logging;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Results;

namespace Polochon.Mediation
{
    /// <summary>
    /// Turns exceptions into a failed <see cref="CommandResult"/> for messages returning one, so
    /// their caller always gets a <see cref="ResultCode"/> back:
    /// <list type="bullet">
    ///   <item><see cref="BusinessRuleException"/> reports its own code.</item>
    ///   <item><see cref="MessageValidationException"/> reports its code, or <see cref="ResultCode.ValidationFailed"/>, and every validation failure in <see cref="CommandResult.Errors"/>.</item>
    ///   <item>Any other exception is logged and reported as <see cref="ResultCode.UnexpectedError"/>.</item>
    /// </list>
    /// Cancellation still propagates. Messages with any other response type pass through untouched.
    /// Register it first so it runs outermost, around the other behaviors, the validators and the
    /// handler (and so around the unit of work the handler commits).
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public sealed class CommandResultBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        private readonly ILogger<CommandResultBehavior<TRequest, TResponse>> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CommandResultBehavior{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="logger">Logs the exceptions reported as <see cref="ResultCode.UnexpectedError"/>.</param>
        public CommandResultBehavior(ILogger<CommandResultBehavior<TRequest, TResponse>> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async ValueTask<TResponse> HandleAsync(
            TRequest request,
            MessageHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            if (typeof(TResponse) != typeof(CommandResult))
            {
                return await next();
            }

            try
            {
                return await next();
            }
            catch (BusinessRuleException ex)
            {
                return Failure(ex.Error);
            }
            catch (MessageValidationException ex)
            {
                return Failure(ex.Error ?? ResultCode.ValidationFailed, ex.Errors);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error while handling {MessageType}.", typeof(TRequest).Name);
                return Failure(ResultCode.UnexpectedError);
            }
        }

        // Only reached when TResponse is CommandResult (checked above).
        private static TResponse Failure(ResultCode error) => Failure(error, []);

        private static TResponse Failure(ResultCode error, IReadOnlyList<ValidationError> errors) => (TResponse)(object)CommandResult.Failure(error, errors);
    }
}
