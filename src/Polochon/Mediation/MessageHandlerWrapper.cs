using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    /// <summary>
    /// Marker interface for message handler wrappers.
    /// </summary>
    internal interface IMessageHandlerWrapper;

    /// <summary>
    /// Generic message handler wrapper interface.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    internal interface IMessageHandlerWrapper<TResponse> : IMessageHandlerWrapper
    {
        /// <summary>
        /// Handles the message asynchronously.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="provider">The service provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        ValueTask<TResponse> HandleAsync(
            IMessage<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Wrapper for message handlers that applies pipeline behaviors and validators.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    internal sealed class QueryHandlerWrapper<TRequest, TResponse> : IMessageHandlerWrapper<TResponse>
        where TRequest : IQuery<TResponse>
    {
        /// <summary>
        /// Handles the message asynchronously, applying all registered pipeline behaviors
        /// and validators. Validators run after pipeline behaviors but before the actual handler.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="provider">The service provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async ValueTask<TResponse> HandleAsync(
            IMessage<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            var typed = (TRequest)request;
            var handler = provider.GetRequiredService<IQueryHandler<TRequest, TResponse>>();
            var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var validators = provider.GetServices<IMessageValidator<TRequest, TResponse>>();

            // Start with the handler as the innermost step
            MessageHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(typed, cancellationToken);

            // Wrap validators around the handler (validators run just before the handler)
            // Iterating without reverse means the first registered validator runs closest to the handler
            // A rejecting validator throws MessageValidationException, which propagates to the caller
            foreach (var validator in validators)
            {
                var next = pipeline;
                var current = validator;
                pipeline = async () =>
                {
                    await current.ValidateAsync(typed, cancellationToken);
                    return await next();
                };
            }

            // Wrap behaviors around the validators (behaviors run before validators)
            // Iterating in reverse means the first registered behavior runs outermost
            foreach (var behavior in behaviors.Reverse())
            {
                var next = pipeline;
                var current = behavior;
                pipeline = () => current.HandleAsync(typed, next, cancellationToken);
            }

            return await pipeline();
        }
    }

    /// <summary>
    /// Wrapper for message handlers that applies pipeline behaviors and validators.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    internal sealed class CommandHandlerWrapper<TRequest, TResponse> : IMessageHandlerWrapper<TResponse>
        where TRequest : ICommand<TResponse>
    {
        /// <summary>
        /// Handles the message asynchronously, applying all registered pipeline behaviors
        /// and validators. Validators run after pipeline behaviors but before the actual handler.
        /// </summary>
        /// <param name="request">The request message.</param>
        /// <param name="provider">The service provider.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async ValueTask<TResponse> HandleAsync(
            IMessage<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            var typed = (TRequest)request;
            var handler = provider.GetRequiredService<ICommandHandler<TRequest, TResponse>>();
            var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
            var validators = provider.GetServices<IMessageValidator<TRequest, TResponse>>();

            // Start with the handler as the innermost step
            MessageHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(typed, cancellationToken);

            // Wrap validators around the handler (validators run just before the handler)
            // Iterating in reverse means the first registered validator runs closest to the handler
            // A rejecting validator throws MessageValidationException, which propagates to the caller
            foreach (var validator in validators.Reverse())
            {
                var next = pipeline;
                var current = validator;
                pipeline = async () =>
                {
                    await current.ValidateAsync(typed, cancellationToken);
                    return await next();
                };
            }

            // Wrap behaviors around the validators (behaviors run before validators)
            // Iterating in reverse means the first registered behavior runs outermost
            foreach (var behavior in behaviors.Reverse())
            {
                var next = pipeline;
                var current = behavior;
                pipeline = () => current.HandleAsync(typed, next, cancellationToken);
            }

            return await pipeline();
        }
    }
}