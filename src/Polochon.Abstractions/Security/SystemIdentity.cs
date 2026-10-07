using System.Security.Claims;

namespace Polochon.Abstractions.Security
{
    /// <summary>
    /// The well-known identity under which Polochon itself acts, e.g. when an inbox processor
    /// turns an integration event into a command. That command is not sent by a user: the event
    /// was raised because an earlier, already authorized command mutated state. Validators can
    /// recognize this identity through <see cref="Role"/>.
    /// </summary>
    public static class SystemIdentity
    {
        /// <summary>
        /// The authentication type of the system identity.
        /// </summary>
        public const string AuthenticationType = "Polochon.System";

        /// <summary>
        /// The role carried by the system identity.
        /// </summary>
        public const string Role = "polochon:system";

        /// <summary>
        /// Creates a new, authenticated system principal carrying <see cref="Role"/>.
        /// </summary>
        /// <param name="name">The name of the system actor, e.g. <c>"Inventory inbox processor"</c>.</param>
        /// <returns>A new system principal.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null, empty or whitespace.</exception>
        public static ClaimsPrincipal CreatePrincipal(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, name), new Claim(ClaimTypes.Role, Role)],
                AuthenticationType);

            return new ClaimsPrincipal(identity);
        }
    }
}
