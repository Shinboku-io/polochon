using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polochon.Abstractions.CQRS;

namespace Polochon.Validation.FluentValidation
{
    /// <summary>
    /// Provides extension methods for validating Polochon messages with FluentValidation.
    /// </summary>
    public static class ServiceRegister
    {
        /// <summary>
        /// Registers every concrete <see cref="IValidator{T}"/> found in <paramref name="assembly"/>,
        /// and the adapter running them as the kernel's message validators. Call it from the
        /// module's service configuration, with the assembly holding its validators, e.g.
        /// <c>services.AddPolochonFluentValidation(typeof(CreateItemCommandValidator).Assembly)</c>.
        /// Without this call a module's FluentValidation validators are never run - nothing fails.
        /// </summary>
        /// <param name="services">The service collection to register with.</param>
        /// <param name="assembly">The assembly to scan for FluentValidation validators.</param>
        // TODO: add a Roslyn analyzer reporting a module assembly that declares an IValidator<T>
        // for an IMessage<> while its module never calls AddPolochonFluentValidation - today that
        // mistake silently disables the module's validation.
        public static IServiceCollection AddPolochonFluentValidation(this IServiceCollection services, Assembly assembly)
        {
            ArgumentNullException.ThrowIfNull(assembly);

            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition)
                {
                    continue;
                }

                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IValidator<>))
                    {
                        services.TryAddEnumerable(ServiceDescriptor.Scoped(iface, type));
                    }
                }
            }

            // Open generic: resolved for every message the dispatcher handles; it does nothing for
            // a message with no IValidator<T>. TryAdd so several calls (one per assembly) add it once.
            services.TryAddEnumerable(ServiceDescriptor.Scoped(typeof(IMessageValidator<,>), typeof(FluentValidationMessageValidator<,>)));

            return services;
        }
    }
}
