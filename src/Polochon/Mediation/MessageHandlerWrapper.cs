using Microsoft.Extensions.DependencyInjection;
using Polochon.Abstractions.CQRS;

namespace Polochon.Mediation
{
    internal abstract class MessageHandlerBase;

    internal abstract class MessageHandlerBase<TResponse> : MessageHandlerBase
    {
        public abstract ValueTask<TResponse> HandleAsync(
            IMessage<TResponse> request,
            IServiceProvider provider,
            CancellationToken cancellationToken);
    }

    internal sealed class MessageHandlerWrapper<TRequest, TResponse> : MessageHandlerBase<TResponse>
        where TRequest : IMessage<TResponse>
    {
        public override ValueTask<TResponse> HandleAsync(
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