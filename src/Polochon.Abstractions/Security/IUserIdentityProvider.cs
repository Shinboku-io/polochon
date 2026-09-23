using System.Security.Claims;

namespace Polochon.Abstractions.Security
{
    /// <summary>
    /// Describes the user who sends a message, or on whose behalf it is sent. Meant to be
    /// injected into an <see cref="CQRS.IMessageValidator{TRequest, TResponse}"/> to authorize a
    /// command before its handler runs.
    /// </summary>
    /// <remarks>
    /// Polochon registers a default, singleton implementation in the host and in every module's
    /// isolated container. It reads the principal of the current async flow, so it must never
    /// capture a user at construction time. Messages processed by an inbox processor run under
    /// the <see cref="SystemIdentity"/> principal.
    /// </remarks>
    /// <example>
    /// <code>
    /// public ValueTask ValidateAsync(DeleteItem request, CancellationToken cancellationToken)
    ///     => user.IsInRole("InventoryManager") || user.IsInRole(SystemIdentity.Role)
    ///         ? ValueTask.CompletedTask
    ///         : throw new MessageValidationException("Not allowed to delete items.");
    /// </code>
    /// </example>
    public interface IUserIdentityProvider
    {
        /// <summary>
        /// Gets a value indicating whether the current user is authenticated.
        /// </summary>
        bool IsAuthenticated { get; }

        /// <summary>
        /// Gets the email address of the current user, or <see langword="null"/> when unknown.
        /// </summary>
        string? Email { get; }

        /// <summary>
        /// Gets the display name of the current user, or <see langword="null"/> when unknown.
        /// </summary>
        string? DisplayName { get; }

        /// <summary>
        /// Gets the principal of the current user. Never <see langword="null"/>: an
        /// unauthenticated principal is returned when no user is known.
        /// </summary>
        ClaimsPrincipal UserPrincipal { get; }

        /// <summary>
        /// Determines whether the current user belongs to the specified role.
        /// </summary>
        /// <param name="role">The name of the role to check.</param>
        /// <returns><see langword="true"/> if the current user is in <paramref name="role"/>; otherwise, <see langword="false"/>.</returns>
        bool IsInRole(string role);
    }
}
