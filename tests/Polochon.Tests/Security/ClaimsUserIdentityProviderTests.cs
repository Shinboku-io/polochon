using System.Security.Claims;
using Polochon.Abstractions.Security;
using Polochon.Security;
using Xunit;

namespace Polochon.Tests.Security
{
    /// <summary>
    /// Tests for <see cref="ClaimsUserIdentityProvider"/> and <see cref="SystemIdentity"/>.
    /// </summary>
    public sealed class ClaimsUserIdentityProviderTests
    {
        private readonly ClaimsUserIdentityProvider provider = new();

        private static IDisposable UseClaims(params Claim[] claims)
            => AmbientUserContext.Use(new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")));

        /// <summary>Without an ambient principal, the user is anonymous and nothing is known.</summary>
        [Fact(DisplayName = "Without ambient principal the user is anonymous")]
        public void WithoutAmbientPrincipalUserIsAnonymous()
        {
            Assert.NotNull(provider.UserPrincipal);
            Assert.False(provider.IsAuthenticated);
            Assert.Null(provider.Email);
            Assert.Null(provider.DisplayName);
            Assert.False(provider.IsInRole("Admin"));
        }

        /// <summary>The ambient principal is exposed as is.</summary>
        [Fact(DisplayName = "UserPrincipal returns the ambient principal")]
        public void UserPrincipalReturnsAmbientPrincipal()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity("Test"));

            using (AmbientUserContext.Use(principal))
            {
                Assert.Same(principal, provider.UserPrincipal);
                Assert.True(provider.IsAuthenticated);
            }
        }

        /// <summary>Email is read from each supported claim type.</summary>
        [Theory(DisplayName = "Email is read from supported claim types")]
        [InlineData(ClaimTypes.Email)]
        [InlineData("email")]
        [InlineData("preferred_username")]
        public void EmailIsReadFromSupportedClaimTypes(string claimType)
        {
            using (UseClaims(new Claim(claimType, "alice@babelcorp.test")))
            {
                Assert.Equal("alice@babelcorp.test", provider.Email);
            }
        }

        /// <summary>The WS-* email claim wins over the OIDC fallbacks.</summary>
        [Fact(DisplayName = "Email prefers the WS-* email claim")]
        public void EmailPrefersWsEmailClaim()
        {
            using (UseClaims(new Claim("preferred_username", "upn@babelcorp.test"), new Claim(ClaimTypes.Email, "alice@babelcorp.test")))
            {
                Assert.Equal("alice@babelcorp.test", provider.Email);
            }
        }

        /// <summary>The OIDC <c>name</c> claim wins over the WS-* name claim.</summary>
        [Fact(DisplayName = "DisplayName prefers the OIDC name claim")]
        public void DisplayNamePrefersOidcNameClaim()
        {
            using (UseClaims(new Claim(ClaimTypes.Name, "alice"), new Claim("name", "Alice Liddell")))
            {
                Assert.Equal("Alice Liddell", provider.DisplayName);
            }
        }

        /// <summary>Without a <c>name</c> claim, the identity name is used.</summary>
        [Fact(DisplayName = "DisplayName falls back to the identity name")]
        public void DisplayNameFallsBackToIdentityName()
        {
            using (UseClaims(new Claim(ClaimTypes.Name, "alice")))
            {
                Assert.Equal("alice", provider.DisplayName);
            }
        }

        /// <summary>Roles are checked against the ambient principal.</summary>
        [Fact(DisplayName = "IsInRole checks the ambient principal roles")]
        public void IsInRoleChecksAmbientPrincipalRoles()
        {
            using (UseClaims(new Claim(ClaimTypes.Role, "InventoryManager")))
            {
                Assert.True(provider.IsInRole("InventoryManager"));
                Assert.False(provider.IsInRole("Admin"));
            }
        }

        /// <summary>A derived provider can supply the principal from elsewhere.</summary>
        [Fact(DisplayName = "Derived provider can override the principal source")]
        public void DerivedProviderCanOverridePrincipalSource()
        {
            var derived = new FixedUserIdentityProvider(new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "wasm@babelcorp.test")], "Test")));

            Assert.True(derived.IsAuthenticated);
            Assert.Equal("wasm@babelcorp.test", derived.Email);
        }

        /// <summary>The system principal is authenticated, named and carries the system role.</summary>
        [Fact(DisplayName = "System principal is authenticated with the system role")]
        public void SystemPrincipalIsAuthenticatedWithSystemRole()
        {
            using (AmbientUserContext.Use(SystemIdentity.CreatePrincipal("processor")))
            {
                Assert.True(provider.IsAuthenticated);
                Assert.True(provider.IsInRole(SystemIdentity.Role));
                Assert.Equal("processor", provider.DisplayName);
                Assert.Null(provider.Email);
                Assert.Equal(SystemIdentity.AuthenticationType, provider.UserPrincipal.Identity!.AuthenticationType);
            }
        }

        /// <summary>A system principal needs a name.</summary>
        [Theory(DisplayName = "System principal requires a name")]
        [InlineData("")]
        [InlineData(" ")]
        public void SystemPrincipalRequiresName(string name)
        {
            _ = Assert.Throws<ArgumentException>(() => SystemIdentity.CreatePrincipal(name));
        }

        private sealed class FixedUserIdentityProvider : ClaimsUserIdentityProvider
        {
            private readonly ClaimsPrincipal principal;

            public FixedUserIdentityProvider(ClaimsPrincipal principal)
            {
                this.principal = principal;
            }

            public override ClaimsPrincipal UserPrincipal => principal;
        }
    }
}
