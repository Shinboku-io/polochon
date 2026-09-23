using Microsoft.Extensions.DependencyInjection;

namespace Polochon.Abstractions.Modules
{
    /// <summary>
    /// Fluent builder returned by module-registration extension methods (e.g. <c>AddModule&lt;TModule&gt;()</c>),
    /// used to layer additional configuration onto a module's isolated service container. Polochon
    /// extension packages (e.g. Polochon.Serilog) expose their own configuration as extension methods
    /// on this interface, mirroring the pattern used by
    /// <c>builder.Services.AddRazorComponents().AddInteractiveServerComponents()</c>.
    /// </summary>
    public interface IModularModuleBuilder : IModularModuleBuilder<IModularModule>
    {
    }

    /// <summary>
    /// Strongly-typed variant of <see cref="IModularModuleBuilder"/>, returned by <c>AddModule&lt;TModule&gt;()</c>
    /// for a specific concrete module type. Lets Polochon extension packages (or callers) configure a
    /// module using its actual type - reading module-specific properties or calling module-specific
    /// methods - instead of only its <c>Name</c>.
    /// </summary>
    /// <remarks>
    /// <typeparamref name="TModule"/> is covariant: <c>TModule</c> only ever appears as the type argument
    /// of an <see cref="Action{T1, T2}"/> parameter, and since <c>Action&lt;,&gt;</c> is itself contravariant
    /// in its type arguments, the two contravariances cancel out (the same reasoning that makes
    /// <c>IObservable&lt;out T&gt;.Subscribe(IObserver&lt;T&gt;)</c> sound with a contravariant
    /// <c>IObserver&lt;in T&gt;</c> parameter). This lets an <c>IModularModuleBuilder&lt;InventoryModule&gt;</c>
    /// be used directly wherever an <c>IModularModuleBuilder&lt;IModularModule&gt;</c> (i.e. the non-generic
    /// <see cref="IModularModuleBuilder"/>) is expected, with no adapter needed.
    /// </remarks>
    /// <typeparam name="TModule">The concrete module type this builder was created for.</typeparam>
    public interface IModularModuleBuilder<out TModule>
        where TModule : IModularModule
    {
        /// <summary>
        /// Queues a callback that configures the module's own isolated service collection, given the
        /// module instance itself. Callbacks run, in registration order, after the module's
        /// <c>ConfigureAdditionalServices</c> override and before the module's service provider is built.
        /// </summary>
        /// <param name="configure">A callback that configures the module's isolated service collection, given the module instance.</param>
        IModularModuleBuilder<TModule> ConfigureModule(Action<IServiceCollection, TModule> configure);

        /// <summary>
        /// Registers services directly into the host's own service collection - not this module's
        /// isolated container. For extensions that need to add a host-level component tied to this
        /// specific module, such as a hosted service (e.g. <c>WithInboxProcessing</c>). Runs
        /// immediately, unlike <see cref="ConfigureModule"/>: the host's service collection is
        /// available synchronously during registration, so there is nothing to queue.
        /// </summary>
        /// <param name="configure">A callback that configures the host's service collection.</param>
        IModularModuleBuilder<TModule> ConfigureHostServices(Action<IServiceCollection> configure);
    }
}
