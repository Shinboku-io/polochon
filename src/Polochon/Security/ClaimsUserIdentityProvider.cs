using System.Security.Claims;
using Polochon.Abstractions.Security;

namespace Polochon.Security
{
    /// <summary>
    /// Default <see cref="IUserIdentityProvider"/>: exposes the principal of
    /// <see cref="AmbientUserContext"/>, or an unauthenticated principal when none is set.
    /// </summary>
    /// <remarks>
    /// Stateless, so safe as a singleton. That matters: module dispatchers currently resolve
    /// their services from the module's root provider, so a per-request implementation would
    /// keep the first user it saw. Platform integrations that cannot set the ambient principal
    /// (e.g. Blazor WebAssembly) can derive from this class and override <see cref="UserPrincipal"/>.
    /// </remarks>
    public class ClaimsUserIdentityProvider : IUserIdentityProvider
    {
        /// <inheritdoc/>
        public virtual ClaimsPrincipal UserPrincipal => AmbientUserContext.Current ?? new ClaimsPrincipal(new ClaimsIdentity());

        /// <inheritdoc/>
        public bool IsAuthenticated => UserPrincipal.Identity?.IsAuthenticated == true;

        /// <inheritdoc/>
        /// <remarks>
        /// Looks up both the WS-* claim type used by ASP.NET Core claim mapping and the short OIDC
        /// names used by tokens read without that mapping.
        /// </remarks>
        public string? Email => FindFirstValue(UserPrincipal, ClaimTypes.Email, "email", "preferred_username");

        /// <inheritdoc/>
        public string? DisplayName => FindFirstValue(UserPrincipal, "name", ClaimTypes.Name) ?? UserPrincipal.Identity?.Name;

        /// <inheritdoc/>
        public bool IsInRole(string role) => UserPrincipal.IsInRole(role);

        private static string? FindFirstValue(ClaimsPrincipal principal, params string[] claimTypes)
        {
            foreach (var claimType in claimTypes)
            {
                var value = principal.FindFirst(claimType)?.Value;
                if (!string.IsNullOrEmpty(value))
                {
                    return value;
                }
            }

            return null;
        }
    }
}
