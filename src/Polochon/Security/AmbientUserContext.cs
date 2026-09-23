using System.Security.Claims;

namespace Polochon.Security
{
    /// <summary>
    /// Holds the principal of the current async flow. Host integrations (ASP.NET Core middleware,
    /// a Blazor circuit handler, a test) set it at their entry point. It then flows through
    /// awaits into module containers, which cannot resolve host services.
    /// </summary>
    /// <example>
    /// <code>
    /// app.Use(async (context, next) =>
    /// {
    ///     using (AmbientUserContext.Use(context.User))
    ///     {
    ///         await next(context);
    ///     }
    /// });
    /// </code>
    /// </example>
    public static class AmbientUserContext
    {
        private static readonly AsyncLocal<ClaimsPrincipal?> current = new();

        /// <summary>
        /// Gets the principal of the current async flow, or <see langword="null"/> if none was set.
        /// </summary>
        public static ClaimsPrincipal? Current => current.Value;

        /// <summary>
        /// Sets <paramref name="principal"/> as the current principal until the returned scope is
        /// disposed, which restores the previous one.
        /// </summary>
        /// <remarks>
        /// Deliberately synchronous: a value set inside an async method does not flow back to
        /// its caller, so it would be lost as soon as this method returned.
        /// </remarks>
        /// <param name="principal">The principal to set.</param>
        /// <returns>A scope that restores the previous principal when disposed.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is <see langword="null"/>.</exception>
        public static IDisposable Use(ClaimsPrincipal principal)
        {
            ArgumentNullException.ThrowIfNull(principal);

            var previous = current.Value;
            current.Value = principal;
            return new Restorer(previous);
        }

        private sealed class Restorer : IDisposable
        {
            private readonly ClaimsPrincipal? previous;
            private bool disposed;

            public Restorer(ClaimsPrincipal? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                {
                    return;
                }

                current.Value = previous;
                disposed = true;
            }
        }
    }
}
