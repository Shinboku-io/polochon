using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    internal interface IMessageHandlerWrapper;

    internal interface IMessageHandlerWrapper<TResponse> : IMessageHandlerWrapper
    {
        ValueTask<TResponse> HandleAsync(
            IMessage<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    internal sealed class MessageHandlerWrapper<TRequest, TResponse> : IMessageHandlerWrapper<TResponse>
        where TRequest : IMessage<TResponse>
    {
        public ValueTask<TResponse> HandleAsync(
            IMessage<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken)
        {
            var typed = (TRequest)request;
            var handler = provider.GetRequiredService<IMessageHandler<TRequest, TResponse>>();
            var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();

            // Build the pipeline: handler at the core, behaviors wrapped outside in registration order.
            // Iterating in reverse means the first registered behavior runs outermost.
            MessageHandlerDelegate<TResponse> pipeline = () => handler.HandleAsync(typed, cancellationToken);

            foreach (var behavior in behaviors.Reverse())
            {
                var next = pipeline;
                var current = behavior;
                pipeline = () => current.HandleAsync(typed, next, cancellationToken);
            }

            return pipeline();
        }
    }
}